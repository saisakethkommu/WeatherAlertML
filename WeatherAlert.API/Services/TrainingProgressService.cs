namespace WeatherAlert.API.Services;

// Holds training progress messages in memory so the Blazor UI can poll them.
// This is a simple in-memory approach — perfect for a demo.
public class TrainingProgressService
{
    // .NET10: Collection expression
    private readonly List<TrainingLogEntry> _logs = [];
    private bool _isTraining = false;
    private bool _isComplete = false;

    public bool IsTraining => _isTraining;
    public bool IsComplete => _isComplete;

    // Returns a snapshot of all logs so far
    public List<TrainingLogEntry> GetLogs() => _logs.ToList();

    // Called by the trainer to add a new progress message
    public void AddLog(string message, LogLevel level = LogLevel.Info)
    {
        _logs.Add(new TrainingLogEntry
        {
            Message = message,
            Level = level.ToString(),
            Timestamp = DateTime.UtcNow
        });
    }

    public void StartTraining()
    {
        _logs.Clear();
        _isTraining = true;
        _isComplete = false;
        AddLog("🚀 Training started...", LogLevel.Info);
    }

    public void CompleteTraining()
    {
        _isTraining = false;
        _isComplete = true;
        AddLog("✅ All models trained and saved successfully!", LogLevel.Success);
    }

    public void FailTraining(string error)
    {
        _isTraining = false;
        _isComplete = false;
        AddLog($"❌ Training failed: {error}", LogLevel.Error);
    }
}

// Represents one log entry shown in the UI
public class TrainingLogEntry
{
    public string Message { get; set; } = string.Empty;
    // Store as string so Blazor can use it directly in CSS class names
    public string Level { get; set; } = "Info";
    public DateTime Timestamp { get; set; }
}

// Log levels control the colour shown in the UI
public enum LogLevel
{
    Info,
    Success,
    Warning,
    Error
}