using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using WeatherAlert.ML.Models;

namespace WeatherAlert.API.Services;

// .NET10: Primary constructor
public class LiveWeatherService(HttpClient http, ILogger<LiveWeatherService> logger)
{
    public async Task<WeatherInput> GetCurrentWeatherAsync(
        float latitude, float longitude)
    {
        var url = $"https://api.open-meteo.com/v1/forecast" +
                  $"?latitude={latitude}&longitude={longitude}" +
                  $"&hourly=temperature_2m,relative_humidity_2m,precipitation," +
                  $"wind_speed_10m,surface_pressure,cloud_cover,wind_gusts_10m" +
                  $"&forecast_days=1&timezone=UTC";

        logger.LogInformation("Fetching live weather from: {Url}", url);

        try
        {
            // Read raw string first so we can log it for debugging
            var raw = await http.GetStringAsync(url);

            logger.LogInformation("Raw JSON (first 500 chars): {Json}",
                raw.Length > 500 ? raw[..500] : raw);

            var options = new System.Text.Json.JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var response = System.Text.Json.JsonSerializer
                .Deserialize<OpenMeteoForecastResponse>(raw, options);

            logger.LogInformation(
                "Raw response — Temp list count: {TempCount}, " +
                "Humidity list count: {HumCount}",
                response?.Hourly?.Temperature_2m?.Count ?? -1,
                response?.Hourly?.Relative_Humidity_2m?.Count ?? -1);

            if (response?.Hourly == null)
            {
                logger.LogWarning("Open-Meteo returned null hourly data");
                return new WeatherInput();
            }

            var input = new WeatherInput
            {
                Temperature = FirstOrDefault(response.Hourly.Temperature_2m),
                Humidity = FirstOrDefault(response.Hourly.Relative_Humidity_2m),
                Precipitation = FirstOrDefault(response.Hourly.Precipitation),
                WindSpeed = FirstOrDefault(response.Hourly.Wind_Speed_10m),
                SurfacePressure = FirstOrDefault(response.Hourly.Surface_Pressure),
                CloudCover = FirstOrDefault(response.Hourly.Cloud_Cover),
                WindGusts = FirstOrDefault(response.Hourly.Wind_Gusts_10m)
            };

            logger.LogInformation(
                "Weather fetched — Temp: {Temp}, Humidity: {Humidity}, " +
                "Precipitation: {Precip}, WindSpeed: {Wind}",
                input.Temperature, input.Humidity,
                input.Precipitation, input.WindSpeed);

            return input;
        }
        catch (Exception ex)
        {
            logger.LogError("Failed to fetch weather: {Error}", ex.Message);
            return new WeatherInput();
        }
    }

    private static float FirstOrDefault(List<float>? list)
        => list?.FirstOrDefault() ?? 0f;
}

public class OpenMeteoForecastResponse
{
    // No JsonPropertyName needed — PropertyNameCaseInsensitive handles it
    public OpenMeteoForecastHourly? Hourly { get; set; }
}

public class OpenMeteoForecastHourly
{
    public List<float>? Temperature_2m { get; set; }
    public List<float>? Relative_Humidity_2m { get; set; }
    public List<float>? Precipitation { get; set; }
    public List<float>? Wind_Speed_10m { get; set; }
    public List<float>? Surface_Pressure { get; set; }
    public List<float>? Cloud_Cover { get; set; }
    public List<float>? Wind_Gusts_10m { get; set; }
}