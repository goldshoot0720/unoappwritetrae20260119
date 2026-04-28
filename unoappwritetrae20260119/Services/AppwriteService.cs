using Appwrite;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using unoappwritetrae20260119.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Linq;
using System;

namespace unoappwritetrae20260119.Services
{
    public class AppwriteService
    {
        private static readonly HttpClient HttpClient = new();

        private const string DefaultEndpoint = "https://sgp.cloud.appwrite.io/v1";
        private const string DefaultProjectId = "698212e50017eada99c8";
        private const string DefaultDatabaseId = "69821743002139037da1";
        private const string DefaultTableId = "69d927310016a98cc2db";

        private readonly string _endpoint;
        private readonly string _projectId;
        private readonly string _databaseId;
        private readonly string _tableId;
        private readonly string _apiKey;

        public AppwriteService()
        {
            _endpoint = GetSetting("NEXT_PUBLIC_APPWRITE_ENDPOINT", DefaultEndpoint).TrimEnd('/');
            _projectId = GetSetting("NEXT_PUBLIC_APPWRITE_PROJECT_ID", DefaultProjectId);
            _databaseId = GetSetting("APPWRITE_DATABASE_ID", DefaultDatabaseId);
            _tableId = GetSetting("APPWRITE_TABLE_ID", GetSetting("APPWRITE_COLLECTION_ID", DefaultTableId));
            _apiKey = GetSetting("APPWRITE_API_KEY", Environment.GetEnvironmentVariable("NEXT_PUBLIC_APPWRITE_API_KEY") ?? string.Empty);
        }

        private static string GetSetting(string key, string fallback)
        {
            var value = Environment.GetEnvironmentVariable(key);
            return string.IsNullOrWhiteSpace(value) ? fallback : value;
        }

        public async Task<List<Subscription>> GetSubscriptionsAsync()
        {
            var subscriptions = new List<Subscription>();
            string? lastRowId = null;

            try
            {
                while (true)
                {
                    var rows = await ListRowsPageAsync(lastRowId);

                    if (rows.Count == 0)
                        break;

                    foreach (var row in rows)
                    {
                        try
                        {
                            var json = row.ToString(Formatting.None);
                            var rowId = row.Value<string>("$id");
                            System.Diagnostics.Debug.WriteLine($"Row {rowId}: {json}");
                            var settings = new JsonSerializerSettings
                            {
                                MissingMemberHandling = MissingMemberHandling.Ignore,
                                NullValueHandling = NullValueHandling.Ignore,
                                Error = (sender, args) =>
                                {
                                    System.Diagnostics.Debug.WriteLine($"JSON parse error for row {rowId}: {args.ErrorContext.Error.Message}");
                                    args.ErrorContext.Handled = true; // Skip this field, don't fail
                                }
                            };
                            var sub = JsonConvert.DeserializeObject<Subscription>(json, settings);

                            if (sub != null)
                            {
                                sub.Id = rowId;
                                sub.CreatedAt = row.Value<string>("$createdAt");
                                sub.UpdatedAt = row.Value<string>("$updatedAt");
                                subscriptions.Add(sub);
                            }
                            else
                            {
                                System.Diagnostics.Debug.WriteLine($"WARNING: Row {rowId} deserialized to null. Raw data: {json}");
                            }
                        }
                        catch (System.Exception docEx)
                        {
                            System.Diagnostics.Debug.WriteLine($"ERROR deserializing row: {docEx.Message}");
                        }
                    }

                    lastRowId = rows.Last().Value<string>("$id");

                    if (rows.Count < 100)
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

        private async Task<List<JObject>> ListRowsPageAsync(string? cursorAfter)
        {
            var queries = new List<string>
            {
                JsonConvert.SerializeObject(new { method = "limit", values = new object[] { 100 } })
            };

            if (!string.IsNullOrWhiteSpace(cursorAfter))
            {
                queries.Add(JsonConvert.SerializeObject(new { method = "cursorAfter", values = new object[] { cursorAfter } }));
            }

            var queryString = string.Join("&", queries.Select((query, index) => $"queries%5B{index}%5D={Uri.EscapeDataString(query)}"));
            var uri = $"{_endpoint}/tablesdb/{Uri.EscapeDataString(_databaseId)}/tables/{Uri.EscapeDataString(_tableId)}/rows?{queryString}";

            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.TryAddWithoutValidation("X-Appwrite-Project", _projectId);
            request.Headers.TryAddWithoutValidation("X-Appwrite-Response-Format", "1.8.0");

            if (!string.IsNullOrWhiteSpace(_apiKey))
            {
                request.Headers.TryAddWithoutValidation("X-Appwrite-Key", _apiKey);
            }

            using var response = await HttpClient.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"TablesDB rows request failed ({(int)response.StatusCode}): {body}");
            }

            var payload = JObject.Parse(body);
            return payload["rows"]?.OfType<JObject>().ToList() ?? new List<JObject>();
        }
    }
}
