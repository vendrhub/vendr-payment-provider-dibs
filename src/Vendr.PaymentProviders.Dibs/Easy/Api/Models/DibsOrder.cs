using Newtonsoft.Json;

namespace Vendr.PaymentProviders.Dibs.Easy.Api.Models
{
    public class DibsOrder
    {
        [JsonProperty("reference")]
        public string Reference { get; set; }

        [JsonProperty("currency")]
        public string Currency { get; set; }

        [JsonProperty("amount")]
        public int Amount { get; set; }

        [JsonProperty("items")]
        public DibsOrderItem[] Items { get; set; }
    }

    public class DibsOrderItem
    {
        [JsonProperty("reference")]
        public string Reference { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("quantity")]
        public int Quantity { get; set; }

        [JsonProperty("unit")]
        public string Unit { get; set; }

        [JsonProperty("unitPrice")]
        public int UnitPrice { get; set; }

        [JsonProperty("taxRate")]
        public int TaxRate { get; set; }

        [JsonProperty("grossTotalAmount")]
        public int GrossTotalAmount { get; set; }

        [JsonProperty("netTotalAmount")]
        public int NetTotalAmount { get; set; }
    }
}
