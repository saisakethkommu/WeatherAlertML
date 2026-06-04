using Microsoft.AspNetCore.Mvc;
using WeatherAlert.AI.Models;
using WeatherAlert.AI.Services;
using WeatherAlert.API.Services;

namespace WeatherAlert.API.Controllers;

// .NET10: Primary constructor
[ApiController]
[Route("api/[controller]")]
public class AIController(
    WeatherAIService aiService,
    SubscriberRepository repo,
    LiveWeatherService weather,
    EmailAlertService email) : ControllerBase
{
    // POST api/ai/analyse-and-send
    // Fetches live weather for each subscriber, sends to Azure AI Foundry
    // for analysis, then emails the AI generated report to the subscriber.
    // This runs alongside the ML.NET approach so both can be compared.
    [HttpPost("analyse-and-send")]
    public async Task<IActionResult> AnalyseAndSend()
    {
        var subscribers = await repo.GetAllAsync();
        if (subscribers.Count == 0)
            return Ok(new { message = "No subscribers found." });

        // .NET10: Collection expression
        List<object> summary = [];

        foreach (var subscriber in subscribers.Where(s => s.IsActive))
        {
            // Step 1 — fetch live weather for this subscriber's location
            var weatherInput = await weather.GetCurrentWeatherAsync(
                subscriber.Latitude, subscriber.Longitude);

            // Step 2 — build a WeatherReading to send to Azure AI Foundry
            var reading = new WeatherReading
            {
                Temperature = weatherInput.Temperature,
                Humidity = weatherInput.Humidity,
                Precipitation = weatherInput.Precipitation,
                WindSpeed = weatherInput.WindSpeed,
                SurfacePressure = weatherInput.SurfacePressure,
                CloudCover = weatherInput.CloudCover,
                WindGusts = weatherInput.WindGusts,
                City = subscriber.City,
                State = subscriber.State
            };

            // Step 3 — send to Azure AI Foundry for analysis
            var analysis = await aiService.AnalyseWeatherAsync(reading);

            // Step 4 — send email with AI analysis
            await SendAIAlertEmailAsync(subscriber.Email, subscriber.Name,
                subscriber.City, subscriber.State, reading, analysis);

            summary.Add(new
            {
                subscriber = subscriber.Name,
                email = subscriber.Email,
                location = $"{subscriber.City}, {subscriber.State}",
                weatherInput,
                analysis
            });
        }

        return Ok(new { processed = subscribers.Count, results = summary });
    }

    // GET api/ai/preview/{subscriberId}
    // Returns AI analysis for one subscriber without sending email.
    // Useful for showing the AI approach in the Blazor UI.
    [HttpGet("preview/{subscriberId}")]
    public async Task<IActionResult> PreviewAIAnalysis(string subscriberId)
    {
        var subscriber = await repo.GetByIdAsync(subscriberId);
        if (subscriber == null)
            return NotFound(new { message = "Subscriber not found." });

        var weatherInput = await weather.GetCurrentWeatherAsync(
            subscriber.Latitude, subscriber.Longitude);

        var reading = new WeatherReading
        {
            Temperature = weatherInput.Temperature,
            Humidity = weatherInput.Humidity,
            Precipitation = weatherInput.Precipitation,
            WindSpeed = weatherInput.WindSpeed,
            SurfacePressure = weatherInput.SurfacePressure,
            CloudCover = weatherInput.CloudCover,
            WindGusts = weatherInput.WindGusts,
            City = subscriber.City,
            State = subscriber.State
        };

        var analysis = await aiService.AnalyseWeatherAsync(reading);

        return Ok(new
        {
            subscriber = subscriber.Name,
            location = $"{subscriber.City}, {subscriber.State}",
            weatherInput,
            analysis
        });
    }

    // Builds and sends an AI powered alert email
    private async Task SendAIAlertEmailAsync(
        string recipientEmail,
        string recipientName,
        string city,
        string state,
        WeatherReading reading,
        AIWeatherAnalysis analysis)
    {
        // AI approach always sends a full weather briefing
        // regardless of alert status — this is the key difference
        // from the ML.NET approach which only emails on alerts
        //var hasAlert = analysis.Rainfall.IsAlert ||
        //               analysis.Humidity.IsAlert ||
        //               analysis.Tornado.IsAlert;

        //if (!hasAlert) return;

        var subject = $"🤖 AI Weather Analysis for {city}, {state}";
        var body = BuildAIEmailBody(recipientName, city, state,
                                        reading, analysis);

        // Reuse the existing email service infrastructure
        await email.SendRawEmailAsync(recipientEmail, subject, body);
    }

    private static string BuildAIEmailBody(
        string name, string city, string state,
        WeatherReading reading, AIWeatherAnalysis analysis)
    {
        string RiskColor(string level) => level switch
        {
            "HIGH" => "#d32f2f",
            "MEDIUM" => "#f57c00",
            _ => "#388e3c"
        };

        return $"""
            <html>
            <body style='font-family:Arial,sans-serif;'>
                <h2 style='color:#1565c0'>🤖 AI Weather Analysis Report</h2>
                <p>Hello <strong>{name}</strong>,</p>
                <p>Here is your AI-powered weather analysis for
                   <strong>{city}, {state}</strong>:</p>

                <div style='background:#f5f5f5;padding:16px;border-radius:8px;margin-bottom:16px'>
                    <strong>Current Conditions:</strong><br/>
                    🌡️ {reading.Temperature}°C &nbsp;
                    💧 {reading.Humidity}% humidity &nbsp;
                    🌧️ {reading.Precipitation}mm &nbsp;
                    💨 {reading.WindSpeed}km/h winds &nbsp;
                    🌬️ {reading.WindGusts}km/h gusts
                </div>

                <p><strong>AI Summary:</strong> {analysis.Summary}</p>

                <table style='border-collapse:collapse;width:100%'>
                    <thead>
                        <tr style='background:#1565c0;color:white'>
                            <th style='padding:8px'>Alert Type</th>
                            <th style='padding:8px'>Risk Level</th>
                            <th style='padding:8px'>Alert</th>
                            <th style='padding:8px'>AI Explanation</th>
                        </tr>
                    </thead>
                    <tbody>
                        <tr>
                            <td style='padding:8px;border:1px solid #ddd'>Rainfall</td>
                            <td style='padding:8px;border:1px solid #ddd;
                                color:{RiskColor(analysis.Rainfall.RiskLevel)}'>
                                {analysis.Rainfall.RiskLevel}</td>
                            <td style='padding:8px;border:1px solid #ddd'>
                                {(analysis.Rainfall.IsAlert ? "⚠️ ALERT" : "✅ Normal")}</td>
                            <td style='padding:8px;border:1px solid #ddd'>
                                {analysis.Rainfall.Explanation}</td>
                        </tr>
                        <tr>
                            <td style='padding:8px;border:1px solid #ddd'>Humidity</td>
                            <td style='padding:8px;border:1px solid #ddd;
                                color:{RiskColor(analysis.Humidity.RiskLevel)}'>
                                {analysis.Humidity.RiskLevel}</td>
                            <td style='padding:8px;border:1px solid #ddd'>
                                {(analysis.Humidity.IsAlert ? "⚠️ ALERT" : "✅ Normal")}</td>
                            <td style='padding:8px;border:1px solid #ddd'>
                                {analysis.Humidity.Explanation}</td>
                        </tr>
                        <tr>
                            <td style='padding:8px;border:1px solid #ddd'>Tornado</td>
                            <td style='padding:8px;border:1px solid #ddd;
                                color:{RiskColor(analysis.Tornado.RiskLevel)}'>
                                {analysis.Tornado.RiskLevel}</td>
                            <td style='padding:8px;border:1px solid #ddd'>
                                {(analysis.Tornado.IsAlert ? "⚠️ ALERT" : "✅ Normal")}</td>
                            <td style='padding:8px;border:1px solid #ddd'>
                                {analysis.Tornado.Explanation}</td>
                        </tr>
                    </tbody>
                </table>

                <div style='margin-top:16px;background:#e3f2fd;padding:16px;border-radius:8px'>
                    <strong>💡 AI Recommendation:</strong><br/>
                    {analysis.Recommendation}
                </div>

                <p style='margin-top:20px;color:#666;font-size:12px'>
                    This analysis was generated by Azure AI Foundry (GPT-4o-mini)
                    based on live weather data from Open-Meteo.
                </p>
            </body>
            </html>
            """;
    }
}