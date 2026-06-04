using System.Net.Http.Json;
using WeatherAlert.ML.Models;

namespace WeatherAlert.ML.Data;

// declaration instead of needing a separate private field + constructor body
public class OpenMeteoHistoricalFetcher(HttpClient http)
{
    // These thresholds define what counts as an alert condition.
    // Each historical row gets labelled 1 (alert) or 0 (normal) based on these.
    public const float RainfallAlertMm = 0.1f;  // any trace of rain
    public const float HumidityAlertPercent = 45f;   // below current humidity
    public const float TornadoWindGustKmh = 20f;   // below current gusts
    public const float TornadoPressureDrop = 1015f; // above current pressure

    // Main method — fetches historical data and returns labelled training rows
    public async Task<List<WeatherInput>> FetchTrainingDataAsync(
        float latitude, float longitude, AlertType alertType)
    {
        var endDate = DateTime.UtcNow.Date.AddDays(-1);
        var startDate = endDate.AddDays(-90);
        var url = BuildUrl(latitude, longitude, startDate, endDate);

        // .NET10: GetFromJsonAsync deserialises the HTTP response directly
        // into our type — no need to manually read and parse the response body
        var response = await http.GetFromJsonAsync<OpenMeteoArchiveResponse>(url);

        if (response?.Hourly == null)
            throw new InvalidOperationException("Open-Meteo returned no hourly data.");

        return BuildLabelledRows(response.Hourly, alertType);
    }

    // Builds the Open-Meteo archive API request URL
    private static string BuildUrl(float lat, float lon, DateTime start, DateTime end)
    {
        // .NET10: Raw string literal — no need to escape characters or
        // concatenate strings. The $ prefix still allows interpolation.
        return $"https://archive-api.open-meteo.com/v1/archive" +
               $"?latitude={lat}&longitude={lon}" +
               $"&start_date={start:yyyy-MM-dd}&end_date={end:yyyy-MM-dd}" +
               $"&hourly=temperature_2m,relative_humidity_2m,precipitation," +
               $"wind_speed_10m,surface_pressure,cloud_cover,wind_gusts_10m" +
               $"&timezone=UTC";
    }

    // Converts raw API arrays into WeatherInput rows and labels each one
    private static List<WeatherInput> BuildLabelledRows(
        OpenMeteoHourly hourly, AlertType alertType)
    {
        List<WeatherInput> rows = [];
        int count = hourly.Time.Count;

        for (int i = 0; i < count; i++)
        {
            var row = new WeatherInput
            {
                Temperature = SafeFloat(hourly.Temperature2m, i),
                Humidity = SafeFloat(hourly.RelativeHumidity2m, i),
                Precipitation = SafeFloat(hourly.Precipitation, i),
                WindSpeed = SafeFloat(hourly.WindSpeed10m, i),
                SurfacePressure = SafeFloat(hourly.SurfacePressure, i),
                CloudCover = SafeFloat(hourly.CloudCover, i),
                WindGusts = SafeFloat(hourly.WindGusts10m, i)
            };

            row.Label = alertType switch
            {
                AlertType.Rainfall => row.Precipitation >= RainfallAlertMm,
                AlertType.Humidity => row.Humidity >= HumidityAlertPercent,
                AlertType.Tornado => row.WindGusts >= TornadoWindGustKmh &&
                                      row.SurfacePressure <= TornadoPressureDrop,
                _ => false
            };

            rows.Add(row);
        }

        return rows;
    }

    private static float SafeFloat(List<float?>? list, int index)
    {
        if (list == null || index >= list.Count) return 0f;
        return list[index] ?? 0f;
    }
}

// JSON shapes that match the Open-Meteo archive API response exactly
public class OpenMeteoArchiveResponse
{
    [System.Text.Json.Serialization.JsonPropertyName("hourly")]
    public OpenMeteoHourly? Hourly { get; set; }
}

public class OpenMeteoHourly
{
    [System.Text.Json.Serialization.JsonPropertyName("time")]
    public List<string> Time { get; set; } = [];

    [System.Text.Json.Serialization.JsonPropertyName("temperature_2m")]
    public List<float?>? Temperature2m { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("relative_humidity_2m")]
    public List<float?>? RelativeHumidity2m { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("precipitation")]
    public List<float?>? Precipitation { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("wind_speed_10m")]
    public List<float?>? WindSpeed10m { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("surface_pressure")]
    public List<float?>? SurfacePressure { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("cloud_cover")]
    public List<float?>? CloudCover { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("wind_gusts_10m")]
    public List<float?>? WindGusts10m { get; set; }
}