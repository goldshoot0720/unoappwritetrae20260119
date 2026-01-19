using System;
using Newtonsoft.Json;

namespace unoappwritetrae20260119.Models
{
    public class Subscription
    {
        [JsonProperty("$id")]
        public string? Id { get; set; }

        [JsonProperty("name")]
        public string? Name { get; set; }

        [JsonProperty("site")]
        public string? Site { get; set; }

        [JsonProperty("price")]
        public int Price { get; set; }

        [JsonProperty("nextdate")]
        public string? NextDate { get; set; } // Keeping as string for now to avoid parsing issues, or DateTime

        [JsonProperty("note")]
        public string? Note { get; set; }

        [JsonProperty("account")]
        public string? Account { get; set; }

        [JsonProperty("$createdAt")]
        public string? CreatedAt { get; set; }

        [JsonProperty("$updatedAt")]
        public string? UpdatedAt { get; set; }
    }
}
