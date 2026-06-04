using Microsoft.AspNetCore.Mvc;
using WeatherAlert.API.Services;
using WeatherAlert.ML.Prediction;

namespace WeatherAlert.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AlertsController(
    SubscriberRepository repo,
    LiveWeatherService weather,
    WeatherPredictor predictor,
    EmailAlertService email) : ControllerBase
{
    // POST api/alerts/send-to-all
    // This is the endpoint the Blazor "Send Alerts" button calls.
    // For each subscriber:
    //   1. Fetch live weather for their location
    //   2. Run both ML models — Decision Tree + Random Forest
    //   3. Send email if any alert was triggered
    [HttpPost("send-to-all")]
    public async Task<IActionResult> SendAlertsToAll()
    {
        if (!predictor.ModelsReady)
            return BadRequest(new
            {
                message = "ML models not trained yet. " +
                          "Please train first via POST /api/ml/train."
            });

        var subscribers = await repo.GetAllAsync();
        if (subscribers.Count == 0)
            return Ok(new { message = "No subscribers found." });

        // .NET10: Collection expression
        List<object> summary = [];

        foreach (var subscriber in subscribers.Where(s => s.IsActive))
        {
            // Step 1 — get live weather for this subscriber's location
            var weatherInput = await weather.GetCurrentWeatherAsync(
                subscriber.Latitude, subscriber.Longitude);

            // Step 2 — run both ML models against the live weather data
            var predictions = predictor.Predict(weatherInput);

            // Step 3 — filter to only the alert types this subscriber wants
            var relevantPredictions = predictions
                .Where(p => subscriber.AlertPreferences
                    .Contains(p.AlertType.ToString()))
                .ToList();

            // Step 4 — send email if any alert was triggered
            await email.SendAlertAsync(
                subscriber.Email,
                subscriber.Name,
                relevantPredictions,
                subscriber.City,
                subscriber.State);

            // Build a summary entry for the Blazor UI to display
            summary.Add(new
            {
                subscriber = subscriber.Name,
                email = subscriber.Email,
                location = $"{subscriber.City}, {subscriber.State}",
                weatherInput,
                predictions = relevantPredictions.Select(p => new
                {
                    p.AlertType,
                    p.ModelUsed,
                    p.IsAlert,
                    p.Confidence,
                    p.Message
                })
            });
        }

        return Ok(new { processed = subscribers.Count, results = summary });
    }

    // GET api/alerts/preview/{subscriberId}
    // Returns prediction results for one subscriber without sending an email.
    // Used by the Blazor UI to show live ML results before committing to send.
    [HttpGet("preview/{subscriberId}")]
    public async Task<IActionResult> PreviewAlerts(string subscriberId)
    {
        if (!predictor.ModelsReady)
            return BadRequest(new { message = "ML models not trained yet." });

        var subscriber = await repo.GetByIdAsync(subscriberId);
        if (subscriber == null)
            return NotFound(new { message = "Subscriber not found." });

        var weatherInput = await weather.GetCurrentWeatherAsync(
            subscriber.Latitude, subscriber.Longitude);

        var predictions = predictor.Predict(weatherInput);

        return Ok(new
        {
            subscriber = subscriber.Name,
            location = $"{subscriber.City}, {subscriber.State}",
            weatherInput,
            predictions
        });
    }
}