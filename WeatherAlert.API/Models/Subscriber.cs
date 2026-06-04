using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace WeatherAlert.API.Models;

// Represents one subscriber document stored in MongoDB.
// Each property maps directly to a field in the MongoDB document.
public class Subscriber
{
    // BsonRepresentation converts between C# string and MongoDB ObjectId type
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;

    // Location coordinates used to fetch weather from Open-Meteo
    public float Latitude { get; set; }
    public float Longitude { get; set; }

    // Which alert types this subscriber wants e.g. ["Rainfall", "Tornado"]
    public List<string> AlertPreferences { get; set; } = [];

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;
}

// Request body shape for POST /api/subscribers
// Kept separate from Subscriber so the API never accepts an Id from outside
public class SubscriberRequest
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public float Latitude { get; set; }
    public float Longitude { get; set; }
    public List<string> AlertPreferences { get; set; } = [];
}