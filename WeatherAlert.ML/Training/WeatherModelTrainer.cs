using Microsoft.ML;
using Microsoft.ML.Data;
using WeatherAlert.ML.Data;
using WeatherAlert.ML.Models;

namespace WeatherAlert.ML.Training;

// Action<string, string> is a callback the API uses to report progress
// back to the UI without the ML project needing to know about the API
// .NET10: Primary constructor
public class WeatherModelTrainer(
    OpenMeteoHistoricalFetcher fetcher,
    string modelsDirectory,
    Action<string, string>? onProgress = null)
{
    private readonly MLContext _mlContext = new(seed: 42);

    // Logs a message back to whoever is listening (the API progress service)
    private void Log(string message, string level = "Info")
        => onProgress?.Invoke(message, level);

    public async Task TrainAllModelsAsync(float latitude, float longitude)
    {
        Log("📍 Training location: " +
            $"Lat {latitude:F4}, Lon {longitude:F4}", "Info");

        foreach (AlertType alertType in Enum.GetValues<AlertType>())
        {
            Log($"", "Info");
            Log($"━━━ {alertType} Alert Models ━━━", "Info");

            // Step 1 — fetch historical data
            Log($"📡 Fetching 90 days of historical weather " +
                $"from Open-Meteo for {alertType}...", "Info");

            var rows = await fetcher.FetchTrainingDataAsync(
                latitude, longitude, alertType);

            // .NET10: bool label — no more 0/1 float comparison
            var positiveCount = rows.Count(r => r.Label);
            var negativeCount = rows.Count(r => !r.Label);

            Log($"✅ {rows.Count} hourly rows fetched", "Success");
            Log($"📊 Label breakdown — " +
                $"Alert: {positiveCount} rows, " +
                $"Normal: {negativeCount} rows", "Info");

            if (positiveCount < 10)
            {
                Log($"⚠️ Very few alert examples for {alertType} " +
                    $"— model may have low accuracy for this type", "Warning");
            }

            // Step 2 — load into ML.NET
            Log($"🔄 Loading data into ML.NET pipeline...", "Info");
            var data = _mlContext.Data.LoadFromEnumerable(rows);
            var split = _mlContext.Data.TrainTestSplit(data, testFraction: 0.2);
            Log($"📐 Split — " +
                $"Training: 80% ({(int)(rows.Count * 0.8)} rows), " +
                $"Testing: 20% ({(int)(rows.Count * 0.2)} rows)", "Info");

            // Step 3 — train both models
            TrainAndSave(split, alertType, "DecisionTree",
                BuildDecisionTreePipeline());
            TrainAndSave(split, alertType, "RandomForest",
                BuildRandomForestPipeline());
        }
    }

    private void TrainAndSave(
        DataOperationsCatalog.TrainTestData split,
        AlertType alertType,
        string modelName,
        IEstimator<ITransformer> pipeline)
    {
        Log($"🌲 Training {modelName} for {alertType}...", "Info");

        var model = pipeline.Fit(split.TrainSet);
        var predictions = model.Transform(split.TestSet);

        // Only evaluate if there are positive examples in the test set
        // AUC is undefined when all test labels are the same class
        try
        {
            var metrics = _mlContext.BinaryClassification.Evaluate(predictions);
            Log($"📈 {modelName} results:", "Info");
            Log($"   Accuracy : {metrics.Accuracy:P1}", "Success");
            Log($"   AUC      : {metrics.AreaUnderRocCurve:F3} (1.0 = perfect)", "Success");
            Log($"   F1 Score : {metrics.F1Score:F3} (balance of precision vs recall)", "Success");
        }
        catch (Exception)
        {
            Log($"⚠️ Metrics skipped for {modelName}/{alertType} " +
                $"— not enough positive examples in test set to evaluate. " +
                $"Model still saved and usable.", "Warning");
        }

        var path = ModelPath(alertType, modelName);
        _mlContext.Model.Save(model, split.TrainSet.Schema, path);
        Log($"💾 Model saved → {alertType}_{modelName}.zip", "Success");
    }

    private IEstimator<ITransformer> BuildDecisionTreePipeline()
    {
        Log("🔧 Building Decision Tree pipeline...", "Info");

        return _mlContext.Transforms.NormalizeMeanVariance("Temperature")
            .Append(_mlContext.Transforms.NormalizeMeanVariance("Humidity"))
            .Append(_mlContext.Transforms.NormalizeMeanVariance("Precipitation"))
            .Append(_mlContext.Transforms.NormalizeMeanVariance("WindSpeed"))
            .Append(_mlContext.Transforms.NormalizeMeanVariance("SurfacePressure"))
            .Append(_mlContext.Transforms.NormalizeMeanVariance("CloudCover"))
            .Append(_mlContext.Transforms.NormalizeMeanVariance("WindGusts"))
            .Append(_mlContext.Transforms.Concatenate("Features",
                "Temperature", "Humidity", "Precipitation",
                "WindSpeed", "SurfacePressure", "CloudCover", "WindGusts"))
            .Append(_mlContext.BinaryClassification.Trainers.FastTree(
                labelColumnName: "Label",
                featureColumnName: "Features",
                numberOfTrees: 50,
                numberOfLeaves: 20,
                minimumExampleCountPerLeaf: 1,
                learningRate: 0.1));
    }

    private IEstimator<ITransformer> BuildRandomForestPipeline()
    {
        Log("🔧 Building Random Forest pipeline...", "Info");

        return _mlContext.Transforms.NormalizeMeanVariance("Temperature")
            .Append(_mlContext.Transforms.NormalizeMeanVariance("Humidity"))
            .Append(_mlContext.Transforms.NormalizeMeanVariance("Precipitation"))
            .Append(_mlContext.Transforms.NormalizeMeanVariance("WindSpeed"))
            .Append(_mlContext.Transforms.NormalizeMeanVariance("SurfacePressure"))
            .Append(_mlContext.Transforms.NormalizeMeanVariance("CloudCover"))
            .Append(_mlContext.Transforms.NormalizeMeanVariance("WindGusts"))
            .Append(_mlContext.Transforms.Concatenate("Features",
                "Temperature", "Humidity", "Precipitation",
                "WindSpeed", "SurfacePressure", "CloudCover", "WindGusts"))
            .Append(_mlContext.BinaryClassification.Trainers.FastForest(
                labelColumnName: "Label",
                featureColumnName: "Features",
                numberOfTrees: 100,
                numberOfLeaves: 20,
                minimumExampleCountPerLeaf: 1));
    }

    public string ModelPath(AlertType alertType, string modelName)
        => Path.Combine(modelsDirectory, $"{alertType}_{modelName}.zip");
}