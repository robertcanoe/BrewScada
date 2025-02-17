using MongoDB.Driver;
using System;

namespace BrewScada
{
    public class MongoDBConnection
    {
        private readonly IMongoDatabase _database;

        public MongoDBConnection(string connectionString, string databaseName)
        {
            try
            {
                var client = new MongoClient(connectionString);
                _database = client.GetDatabase(databaseName);
            }
            catch (MongoConfigurationException ex)
            {
                Console.WriteLine($"Error configuring MongoDB: {ex.Message}");
                throw;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred while connecting to MongoDB: {ex.Message}");
                throw;
            }
        }

        public IMongoCollection<T> GetCollection<T>(string collectionName)
        {
            return _database.GetCollection<T>(collectionName);
        }

        public IMongoCollection<Counter> Counters => _database.GetCollection<Counter>("Counters");
    }
}