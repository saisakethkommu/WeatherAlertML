namespace WeatherAlert.AI.Models;

// The weather reading we send to Azure AI Foundry for analysis
public class WeatherReading
{
    public float Temperature { get; set; }
    public float Humidity { get; set; }
    public float Precipitation { get; set; }
    public float WindSpeed { get; set; }
    public float SurfacePressure { get; set; }
    public float CloudCover { get; set; }
    public float WindGusts { get; set; }
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
}

// The structured response we ask GPT to return
public class AIWeatherAnalysis
{
    // Overall summary of current conditions
    public string Summary { get; set; } = string.Empty;

    // Individual alert assessments per weather type
    public AIAlertAssessment Rainfall { get; set; } = new();
    public AIAlertAssessment Humidity { get; set; } = new();
    public AIAlertAssessment Tornado { get; set; } = new();

    // General recommendation for the subscriber
    public string Recommendation { get; set; } = string.Empty;
}

// What GPT says about one specific alert type
public class AIAlertAssessment
{
    // true = GPT thinks an alert is warranted
    public bool IsAlert { get; set; }

    // LOW, MEDIUM, HIGH
    public string RiskLevel { get; set; } = string.Empty;

    // Plain English explanation of why
    public string Explanation { get; set; } = string.Empty;
}