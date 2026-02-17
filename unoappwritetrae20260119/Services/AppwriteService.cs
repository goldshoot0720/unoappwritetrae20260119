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

        private const string Endpoint = "https://sgp.cloud.appwrite.io/v1";
        private const string ProjectId = "698212e50017eada99c8";
        private const string DatabaseId = "69821743002139037da1";
        private const string CollectionId = "687250d70020221fb26c"; // Subscription collection — 需要更新為新 collection ID
        private const string ApiKey = "standard_eaf36e517e4588326b3b6da14079c26ddb1a7953ddf71aa89930ac1994c0d0f5a784cbc77bb9a88c55d354138993faeec4af356e7fb9a0565968fb09a3a9dde604b0372e9e0a7aeaceb78e6bf0a22f6f3ccbcbb30e019f449c1524f191e259983d2a9fdaf337a438cad779a15e92a65755c3d1defeaf3692ce0b9513c6e07a41";

        public AppwriteService()
        {
            _client = new Client()
                .SetEndpoint(Endpoint)
                .SetProject(ProjectId)
                .SetKey(ApiKey);

            _databases = new Databases(_client);
        }

        public async Task<List<Subscription>> GetSubscriptionsAsync()
        {
            var subscriptions = new List<Subscription>();
            int offset = 0;
            const int pageSize = 100;

            try
            {
                while (true)
                {
                    var response = await _databases.ListDocuments(
                        databaseId: DatabaseId,
                        collectionId: CollectionId,
                        queries: new List<string>
                        {
                            Query.Limit(pageSize),
                            Query.Offset(offset)
                        }
                    );

                    if (response.Documents == null || response.Documents.Count == 0)
                        break;

                    foreach (var doc in response.Documents)
                    {
                        var json = JsonConvert.SerializeObject(doc.Data);
                        var sub = JsonConvert.DeserializeObject<Subscription>(json);

                        if (sub != null)
                        {
                            sub.Id = doc.Id;
                            sub.CreatedAt = doc.CreatedAt;
                            sub.UpdatedAt = doc.UpdatedAt;
                            subscriptions.Add(sub);
                        }
                    }

                    // If we got fewer than pageSize, we've reached the end
                    if (response.Documents.Count < pageSize)
                        break;

                    offset += pageSize;
                }

                System.Diagnostics.Debug.WriteLine($"Fetched {subscriptions.Count} subscriptions total");
                return subscriptions;
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error fetching subscriptions: {ex.Message}");
                return subscriptions; // Return whatever we fetched so far
            }
        }
    }
}
