namespace WeatherAlert.ML.Models
{
    // The input data fed into the ML model — one row of weather readings
    public class WeatherInput
    {
        public float Temperature { get; set; }
        public float Humidity { get; set; }
        public float Precipitation { get; set; }
        public float WindSpeed { get; set; }
        public float SurfacePressure { get; set; }
        public float CloudCover { get; set; }
        public float WindGusts { get; set; }

        // 0 = no alert, 1 = alert — only used during training
        public bool Label { get; set; }
    }

    // What the ML model outputs after making a prediction
    public class WeatherPrediction
    {
        public bool PredictedLabel { get; set; }   // true = alert
        public float Probability { get; set; }      // 0.0 to 1.0
        public float Score { get; set; }
    }

    // The three weather conditions we are predicting alerts for
    public enum AlertType
    {
        Rainfall,
        Humidity,
        Tornado
    }

    // The final result returned to the API after prediction
    public class AlertResult
    {
        public AlertType AlertType { get; set; }
        public bool IsAlert { get; set; }
        public float Confidence { get; set; }
        public string Message { get; set; } = string.Empty;
        public string ModelUsed { get; set; } = string.Empty;
    }
}
