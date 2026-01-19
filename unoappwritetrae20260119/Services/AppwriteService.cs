using Appwrite;
using Appwrite.Services;
using Appwrite.Models;
using System.Collections.Generic;
using System.Threading.Tasks;
using unoappwritetrae20260119.Models;
using Newtonsoft.Json;
using System.Linq;

namespace unoappwritetrae20260119.Services
{
    public class AppwriteService
    {
        private Client _client;
        private Databases _databases;

        private const string Endpoint = "https://fra.cloud.appwrite.io/v1";
        private const string ProjectId = "680c76af0037a7d23e44";
        private const string DatabaseId = "680c778b000f055f6409";
        private const string CollectionId = "687250d70020221fb26c"; // Subscription collection

        public AppwriteService()
        {
            _client = new Client()
                .SetEndpoint(Endpoint)
                .SetProject(ProjectId);

            _databases = new Databases(_client);
        }

        public async Task<List<Subscription>> GetSubscriptionsAsync()
        {
            try
            {
                var response = await _databases.ListDocuments(
                    databaseId: DatabaseId,
                    collectionId: CollectionId
                );

                var subscriptions = new List<Subscription>();
                foreach (var doc in response.Documents)
                {
                    // Convert the Data dictionary to JSON and then back to Subscription
                    // This handles the custom fields
                    var json = JsonConvert.SerializeObject(doc.Data);
                    var sub = JsonConvert.DeserializeObject<Subscription>(json);
                    
                    if (sub != null)
                    {
                        // Map system fields
                        sub.Id = doc.Id;
                        sub.CreatedAt = doc.CreatedAt;
                        sub.UpdatedAt = doc.UpdatedAt;
                        
                        subscriptions.Add(sub);
                    }
                }
                return subscriptions;
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error fetching subscriptions: {ex.Message}");
                return new List<Subscription>();
            }
        }
    }
}
