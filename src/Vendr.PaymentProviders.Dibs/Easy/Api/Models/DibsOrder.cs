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
}
