using MongoDB.Driver;
using WeatherAlert.API.Models;

namespace WeatherAlert.API.Services;

// Handles all MongoDB operations for the subscribers collection
public class SubscriberRepository(IConfiguration config)
{
	// MongoDB client and collection set up once when the class is first created
	private readonly IMongoCollection<Subscriber> _collection = new MongoClient(
		config["MongoDB:ConnectionString"])
		.GetDatabase(config["MongoDB:DatabaseName"])
		.GetCollection<Subscriber>("subscribers");

	// Get every subscriber in the collection
	public async Task<List<Subscriber>> GetAllAsync()
		=> await _collection.Find(_ => true).ToListAsync();

	// Get a single subscriber by their MongoDB ObjectId
	public async Task<Subscriber?> GetByIdAsync(string id)
		=> await _collection.Find(s => s.Id == id).FirstOrDefaultAsync();

	// Insert a new subscriber document into MongoDB
	public async Task CreateAsync(Subscriber subscriber)
		=> await _collection.InsertOneAsync(subscriber);

	// Remove a subscriber by their MongoDB ObjectId
	public async Task DeleteAsync(string id)
		=> await _collection.DeleteOneAsync(s => s.Id == id);
}