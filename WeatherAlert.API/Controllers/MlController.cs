using Microsoft.AspNetCore.Mvc;
using WeatherAlert.API.Services;
using WeatherAlert.ML.Training;

namespace WeatherAlert.API.Controllers;

// .NET10: Primary constructor
[ApiController]
[Route("api/[controller]")]
public class MlController(
    WeatherModelTrainer trainer,
    TrainingProgressService progress,
    IConfiguration config) : ControllerBase
{
    // POST api/ml/train
    // Starts model training in the background and returns immediately.
    // The UI polls GET api/ml/progress to watch training happen live.
    [HttpPost("train")]
    public IActionResult Train(
        [FromQuery] float? latitude,
        [FromQuery] float? longitude)
    {
        if (progress.IsTraining)
            return BadRequest(new { message = "Training already in progress." });

        var lat = latitude ?? float.Parse(config["ML:DefaultTrainingLatitude"]!);
        var lon = longitude ?? float.Parse(config["ML:DefaultTrainingLongitude"]!);

        progress.StartTraining();

        // Fire and forget — training runs in background
        _ = Task.Run(async () =>
        {
            try
            {
                await trainer.TrainAllModelsAsync(lat, lon);
                progress.CompleteTraining();
            }
            catch (Exception ex)
            {
                progress.FailTraining(ex.Message);
            }
        });

        return Accepted(new
        {
            message = "Training started. Poll GET /api/ml/progress to watch live.",
            latitude = lat,
            longitude = lon
        });
    }

    // GET api/ml/progress
    // Returns all training log entries so far.
    // The Blazor UI calls this every 2 seconds to update the live log panel.
    [HttpGet("progress")]
    public IActionResult GetProgress()
    {
        return Ok(new
        {
            isTraining = progress.IsTraining,
            isComplete = progress.IsComplete,
            logs = progress.GetLogs()
        });
    }

    // GET api/ml/status
    // Quick check whether models are trained and ready
    [HttpGet("status")]
    public IActionResult Status()
    {
        return Ok(new
        {
            isTraining = progress.IsTraining,
            isComplete = progress.IsComplete,
            message = progress.IsComplete
                ? "Models trained and ready."
                : "Models not yet trained. Use POST /api/ml/train to start."
        });
    }
}