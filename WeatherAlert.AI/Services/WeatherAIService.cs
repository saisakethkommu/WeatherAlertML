using System.Diagnostics.CodeAnalysis;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using WeatherAlert.AI.Models;

namespace WeatherAlert.AI.Services;

// .NET10: Primary constructor
// Connects to Groq API to analyse weather conditions using Llama 3.
// Groq is free, extremely fast, and perfect for structured JSON responses.
public class WeatherAIService(IConfiguration config, IHttpClientFactory httpClientFactory)
{
    public async Task<AIWeatherAnalysis> AnalyseWeatherAsync(WeatherReading reading)
    {
        var apiKey = config["GroqAI:ApiKey"]!;
        var model = config["GroqAI:Model"] ?? "llama-3.1-8b-instant";

        // Groq uses the same API format as OpenAI — makes switching easy
        // This is the endpoint for Groq's OpenAI-compatible chat completions API.
        var url = "https://api.groq.com/openai/v1/chat/completions";

        var prompt = BuildPrompt(reading);

        // Request body follows OpenAI chat completions format
        var requestBody = new
        {
            model,
            messages = new[]
            {
                new
                {
                    role    = "system",
                    content = "You are a meteorologist assistant. Analyse weather " +
                              "conditions and respond ONLY with valid JSON — " +
                              "no markdown, no code blocks, just raw JSON."
                },
                new
                {
                    role    = "user",
                    content = prompt
                }
            },
            temperature = 0.1,
            max_tokens = 1024
        };

        var http = httpClientFactory.CreateClient();

        // Groq requires Bearer token authentication
        http.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

        var response = await http.PostAsJsonAsync(url, requestBody);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            return FallbackAnalysis($"Groq API error: {error}");
        }

        var raw = await response.Content.ReadAsStringAsync();
        var content = ExtractTextFromGroqResponse(raw);

        return ParseResponse(content);
    }

    private static string BuildPrompt(WeatherReading r)
    {
        var conditions =
            $"Analyse these current weather conditions for {r.City}, {r.State}:\n\n" +
            $"Temperature     : {r.Temperature}°C\n" +
            $"Humidity        : {r.Humidity}%\n" +
            $"Precipitation   : {r.Precipitation}mm\n" +
            $"Wind Speed      : {r.WindSpeed} km/h\n" +
            $"Wind Gusts      : {r.WindGusts} km/h\n" +
            $"Surface Pressure: {r.SurfacePressure} hPa\n" +
            $"Cloud Cover     : {r.CloudCover}%\n\n";

        var jsonTemplate =
            "Respond with ONLY this exact JSON structure, no markdown, no code blocks:\n" +
            "{\n" +
            "  \"summary\": \"brief overall conditions summary\",\n" +
            "  \"rainfall\": {\n" +
            "    \"isAlert\": false,\n" +
            "    \"riskLevel\": \"LOW\",\n" +
            "    \"explanation\": \"why\"\n" +
            "  },\n" +
            "  \"humidity\": {\n" +
            "    \"isAlert\": false,\n" +
            "    \"riskLevel\": \"LOW\",\n" +
            "    \"explanation\": \"why\"\n" +
            "  },\n" +
            "  \"tornado\": {\n" +
            "    \"isAlert\": false,\n" +
            "    \"riskLevel\": \"LOW\",\n" +
            "    \"explanation\": \"why\"\n" +
            "  },\n" +
            "  \"recommendation\": \"practical advice for the subscriber\"\n" +
            "}";

        return conditions + jsonTemplate;
    }

    // Extracts text from Groq's OpenAI-compatible response format
    private static string ExtractTextFromGroqResponse(string raw)
    {
        try
        {
            var doc = JsonDocument.Parse(raw);
            var content = doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString() ?? string.Empty;

            // Strip markdown code blocks if model wraps response in them
            return content
                .Replace("```json", "")
                .Replace("```", "")
                .Trim();
        }
        catch (Exception ex)
        {
            return $"{{\"summary\": \"Failed to extract response: {ex.Message}\"}}";
        }
    }

    private static AIWeatherAnalysis ParseResponse(string content)
    {
        try
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var result = JsonSerializer.Deserialize<AIWeatherAnalysis>(content, options);
            return result ?? FallbackAnalysis("Could not parse AI response.");
        }
        catch (Exception ex)
        {
            return FallbackAnalysis($"Parse error: {ex.Message}. Raw: {content}");
        }
    }

    private static AIWeatherAnalysis FallbackAnalysis(string reason)
    {
        return new AIWeatherAnalysis
        {
            Summary = $"AI analysis unavailable: {reason}",
            Recommendation = "Please check local weather services for current conditions.",
            Rainfall = new AIAlertAssessment { RiskLevel = "UNKNOWN" },
            Humidity = new AIAlertAssessment { RiskLevel = "UNKNOWN" },
            Tornado = new AIAlertAssessment { RiskLevel = "UNKNOWN" }
        };
    }
}