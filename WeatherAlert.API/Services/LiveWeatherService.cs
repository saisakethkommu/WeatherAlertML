using WeatherAlert.ML.Models;

namespace WeatherAlert.API.Services;

// Fetches current weather conditions from Open-Meteo's free forecast API.
// The returned WeatherInput is fed directly into the ML prediction engine.
public class LiveWeatherService(HttpClient http)
{
    public async Task<WeatherInput> GetCurrentWeatherAsync(
        float latitude, float longitude)
    {
        // Open-Meteo forecast API — no API key required
        var url = $"https://api.open-meteo.com/v1/forecast" +
                  $"?latitude={latitude}&longitude={longitude}" +
                  $"&hourly=temperature_2m,relative_humidity_2m,precipitation," +
                  $"wind_speed_10m,surface_pressure,cloud_cover,wind_gusts_10m" +
                  $"&forecast_days=1&timezone=UTC";

        var response = await http.GetFromJsonAsync<OpenMeteoForecastResponse>(url)
            ?? throw new InvalidOperationException("Open-Meteo returned no data.");

        // Take the first hour of today's forecast as current conditions
        return new WeatherInput
        {
            Temperature = FirstOrDefault(response.Hourly.Temperature2m),
            Humidity = FirstOrDefault(response.Hourly.RelativeHumidity2m),
            Precipitation = FirstOrDefault(response.Hourly.Precipitation),
            WindSpeed = FirstOrDefault(response.Hourly.WindSpeed10m),
            SurfacePressure = FirstOrDefault(response.Hourly.SurfacePressure),
            CloudCover = FirstOrDefault(response.Hourly.CloudCover),
            WindGusts = FirstOrDefault(response.Hourly.WindGusts10m)
        };
    }

    private static float FirstOrDefault(List<float>? list)
        => list?.FirstOrDefault() ?? 0f;
}

// JSON shapes matching the Open-Meteo forecast API response
public class OpenMeteoForecastResponse
{
    public OpenMeteoForecastHourly Hourly { get; set; } = new();
}

public class OpenMeteoForecastHourly
{
    public List<float>? Temperature2m { get; set; }
    public List<float>? RelativeHumidity2m { get; set; }
    public List<float>? Precipitation { get; set; }
    public List<float>? WindSpeed10m { get; set; }
    public List<float>? SurfacePressure { get; set; }
    public List<float>? CloudCover { get; set; }
    public List<float>? WindGusts10m { get; set; }
}