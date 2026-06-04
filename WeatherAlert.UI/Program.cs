using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using WeatherAlert.UI;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Default HttpClient points to Blazor's own host
builder.Services.AddScoped(sp => new HttpClient
{
    BaseAddress = new Uri(builder.HostEnvironment.BaseAddress)
});

// Named HttpClient points to the API
builder.Services.AddHttpClient("WeatherAlertAPI", client =>
{
    client.BaseAddress = new Uri("https://localhost:44354");
});

await builder.Build().RunAsync();