using WeatherAlert.API.Services;
using WeatherAlert.ML.Data;
using WeatherAlert.ML.Prediction;
using WeatherAlert.ML.Training;

var builder = WebApplication.CreateBuilder(args);

// -------------------------------------------------------------------------
// SERVICES
// -------------------------------------------------------------------------

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // .NET10: Serialize enums as strings instead of numbers
        // so the Blazor UI receives "Rainfall" instead of 0
        options.JsonSerializerOptions.Converters.Add(
            new System.Text.Json.Serialization.JsonStringEnumConverter());
    });
builder.Services.AddOpenApi();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// CORS — allows the Blazor frontend to call the API
builder.Services.AddCors(options =>
    options.AddPolicy("BlazorPolicy", policy =>
        policy.WithOrigins(
                "https://localhost:7082",
                "http://localhost:7082",
                "https://localhost:7001",
                "http://localhost:5001")
              .AllowAnyHeader()
              .AllowAnyMethod()));

// HTTP clients
builder.Services.AddHttpClient<LiveWeatherService>();
builder.Services.AddHttpClient<OpenMeteoHistoricalFetcher>();

// MongoDB subscriber repository
builder.Services.AddSingleton<SubscriberRepository>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    return new SubscriberRepository(config);
});

// Email service
builder.Services.AddSingleton<EmailAlertService>();

// Training progress service — singleton so both the trainer
// and the progress endpoint share the same instance
builder.Services.AddSingleton<TrainingProgressService>();

// ML services
var modelsDir = builder.Configuration["ML:ModelsDirectory"] ?? "ml-models";

builder.Services.AddSingleton<WeatherPredictor>(_ =>
{
    var predictor = new WeatherPredictor(modelsDir);
    predictor.LoadModels();
    return predictor;
});

// .NET10: Trainer now receives a progress callback so it can
// report each training step back to the UI in real time
builder.Services.AddSingleton<WeatherModelTrainer>(sp =>
{
    var fetcher = sp.GetRequiredService<OpenMeteoHistoricalFetcher>();
    var progress = sp.GetRequiredService<TrainingProgressService>();

    return new WeatherModelTrainer(fetcher, modelsDir, (message, level) =>
    {
        // Convert string level to our LogLevel enum and log it
        var logLevel = level switch
        {
            "Success" => WeatherAlert.API.Services.LogLevel.Success,
            "Warning" => WeatherAlert.API.Services.LogLevel.Warning,
            "Error" => WeatherAlert.API.Services.LogLevel.Error,
            _ => WeatherAlert.API.Services.LogLevel.Info
        };
        progress.AddLog(message, logLevel);
    });
});

// -------------------------------------------------------------------------
// PIPELINE
// -------------------------------------------------------------------------

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("BlazorPolicy");
app.UseAuthorization();
app.MapControllers();

app.Run();