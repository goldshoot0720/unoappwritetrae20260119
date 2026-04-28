using Appwrite;
using Appwrite.Services;
using Appwrite.Models;
using System.Collections.Generic;
using System.Threading.Tasks;
using unoappwritetrae20260119.Models;
using Newtonsoft.Json;
using System.Linq;
using System;

namespace unoappwritetrae20260119.Services
{
    public class AppwriteService
    {
        private Client _client;
        private Databases _databases;

        private const string DefaultEndpoint = "https://sgp.cloud.appwrite.io/v1";
        private const string DefaultProjectId = "698212e50017eada99c8";
        private const string DefaultDatabaseId = "69821743002139037da1";
        private const string DefaultCollectionId = "69b24465002d43df9b00";

        private readonly string _databaseId;
        private readonly string _collectionId;

        public AppwriteService()
        {
            var endpoint = GetSetting("NEXT_PUBLIC_APPWRITE_ENDPOINT", DefaultEndpoint);
            var projectId = GetSetting("NEXT_PUBLIC_APPWRITE_PROJECT_ID", DefaultProjectId);
            _databaseId = GetSetting("APPWRITE_DATABASE_ID", DefaultDatabaseId);
            _collectionId = GetSetting("APPWRITE_COLLECTION_ID", DefaultCollectionId);
            var apiKey = GetSetting("APPWRITE_API_KEY", Environment.GetEnvironmentVariable("NEXT_PUBLIC_APPWRITE_API_KEY") ?? string.Empty);

            _client = new Client()
                .SetEndpoint(endpoint)
                .SetProject(projectId);

            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                _client.SetKey(apiKey);
            }

            _databases = new Databases(_client);
        }

        private static string GetSetting(string key, string fallback)
        {
            var value = Environment.GetEnvironmentVariable(key);
            return string.IsNullOrWhiteSpace(value) ? fallback : value;
        }

        public async Task<List<Subscription>> GetSubscriptionsAsync()
        {
            var subscriptions = new List<Subscription>();
            string? lastDocId = null;

            try
            {
                while (true)
                {
                    var queryList = new List<string> { Query.Limit(100) };
                    if (lastDocId != null)
                    {
                        queryList.Add(Query.CursorAfter(lastDocId));
                    }

                    var response = await _databases.ListDocuments(
                        databaseId: _databaseId,
                        collectionId: _collectionId,
                        queries: queryList
                    );

                    if (response.Documents == null || response.Documents.Count == 0)
                        break;

                    foreach (var doc in response.Documents)
                    {
                        try
                        {
                            var json = JsonConvert.SerializeObject(doc.Data);
                            System.Diagnostics.Debug.WriteLine($"Document {doc.Id}: {json}");
                            var settings = new JsonSerializerSettings
                            {
                                MissingMemberHandling = MissingMemberHandling.Ignore,
                                NullValueHandling = NullValueHandling.Ignore,
                                Error = (sender, args) =>
                                {
                                    System.Diagnostics.Debug.WriteLine($"JSON parse error for doc {doc.Id}: {args.ErrorContext.Error.Message}");
                                    args.ErrorContext.Handled = true; // Skip this field, don't fail
                                }
                            };
                            var sub = JsonConvert.DeserializeObject<Subscription>(json, settings);

                            if (sub != null)
                            {
                                sub.Id = doc.Id;
                                sub.CreatedAt = doc.CreatedAt;
                                sub.UpdatedAt = doc.UpdatedAt;
                                subscriptions.Add(sub);
                            }
                            else
                            {
                                System.Diagnostics.Debug.WriteLine($"WARNING: Document {doc.Id} deserialized to null. Raw data: {json}");
                            }
                        }
                        catch (System.Exception docEx)
                        {
                            System.Diagnostics.Debug.WriteLine($"ERROR deserializing document {doc.Id}: {docEx.Message}");
                        }
                    }

                    lastDocId = response.Documents[response.Documents.Count - 1].Id;

                    // If we got fewer than 100, we've reached the end
                    if (response.Documents.Count < 100)
                        break;
                }

                System.Diagnostics.Debug.WriteLine($"Fetched {subscriptions.Count} subscriptions total");
                return subscriptions;
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error fetching subscriptions: {ex.Message}");
                // Re-throw so caller can display the error
                throw;
            }
        }
    }
}
