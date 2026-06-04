using Microsoft.AspNetCore.Mvc;
using WeatherAlert.API.Models;
using WeatherAlert.API.Services;

namespace WeatherAlert.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SubscribersController(SubscriberRepository repo) : ControllerBase
{
    // GET api/subscribers
    // Returns all subscribers stored in MongoDB
    [HttpGet]
    public async Task<IActionResult> GetAll()
        => Ok(await repo.GetAllAsync());

    // POST api/subscribers
    // Creates a new subscriber from the form data and saves to MongoDB
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SubscriberRequest request)
    {
        // Map the request to a Subscriber document
        var subscriber = new Subscriber
        {
            Name = request.Name,
            Email = request.Email,
            City = request.City,
            State = request.State,
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            AlertPreferences = request.AlertPreferences
        };

        await repo.CreateAsync(subscriber);

        return CreatedAtAction(nameof(GetAll), new { id = subscriber.Id }, subscriber);
    }

    // DELETE api/subscribers/{id}
    // Removes a subscriber from MongoDB by their ObjectId
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        await repo.DeleteAsync(id);
        return NoContent();
    }
}