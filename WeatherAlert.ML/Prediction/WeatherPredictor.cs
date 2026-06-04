using Microsoft.ML;
using WeatherAlert.ML.Models;

namespace WeatherAlert.ML.Prediction;

public class WeatherPredictor(string modelsDirectory)
{
    private readonly MLContext _mlContext = new(seed: 42);

    // Stores one prediction engine per model.
    // Key format is "AlertType_ModelName" e.g. "Rainfall_DecisionTree"
    private readonly Dictionary<string, PredictionEngine<WeatherInput, WeatherPrediction>> _engines = [];

    // Called once at API startup — loads all saved .zip model files into memory
    public void LoadModels()
    {
        foreach (AlertType alertType in Enum.GetValues<AlertType>())
        {
            foreach (var modelName in new[] { "DecisionTree", "RandomForest" })
            {
                var path = Path.Combine(modelsDirectory, $"{alertType}_{modelName}.zip");

                if (!File.Exists(path))
                {
                    Console.WriteLine($"[ML] Model not found (needs training): {path}");
                    continue;
                }

                // Load the saved model from disk
                var model = _mlContext.Model.Load(path, out _);

                // PredictionEngine is ML.NET's single-row prediction API.
                // We create one per model and keep it in the dictionary
                // so we don't reload from disk on every prediction call.
                var engine = _mlContext.Model
                    .CreatePredictionEngine<WeatherInput, WeatherPrediction>(model);

                _engines[EngineKey(alertType, modelName)] = engine;
                Console.WriteLine($"[ML] Loaded: {alertType} / {modelName}");
            }
        }
    }

    // Runs live weather input through ALL loaded models and returns every result.
    // This means the Blazor UI can show Decision Tree vs Random Forest side by side.
    public List<AlertResult> Predict(WeatherInput input)
    {
        List<AlertResult> results = [];

        foreach (AlertType alertType in Enum.GetValues<AlertType>())
        {
            foreach (var modelName in new[] { "DecisionTree", "RandomForest" })
            {
                var key = EngineKey(alertType, modelName);

                if (!_engines.TryGetValue(key, out var engine))
                    continue;

                // This is the actual prediction call —
                // the trained model analyses the input and returns a result
                var prediction = engine.Predict(input);

                results.Add(new AlertResult
                {
                    AlertType = alertType,
                    IsAlert = prediction.PredictedLabel,
                    Confidence = prediction.Probability,
                    ModelUsed = modelName,
                    Message = BuildMessage(alertType, prediction, modelName)
                });
            }
        }

        return results;
    }

    // True only when at least one model has been loaded successfully
    public bool ModelsReady => _engines.Count > 0;

    private static string BuildMessage(AlertType alertType, WeatherPrediction p, string model)
    {
        if (!p.PredictedLabel)
            return $"[{model}] No {alertType} alert. " +
                   $"Confidence normal: {(1 - p.Probability):P0}";

        return alertType switch
        {
            AlertType.Rainfall => $"[{model}] Heavy rainfall alert! " +
                                  $"Confidence: {p.Probability:P0}",
            AlertType.Humidity => $"[{model}] High humidity alert! " +
                                  $"Confidence: {p.Probability:P0}",
            AlertType.Tornado => $"[{model}] Tornado risk detected! " +
                                  $"Confidence: {p.Probability:P0}",
            _ => $"[{model}] Alert detected. " +
                                  $"Confidence: {p.Probability:P0}"
        };
    }

    private static string EngineKey(AlertType alertType, string modelName)
        => $"{alertType}_{modelName}";
}