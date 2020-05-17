using Newtonsoft.Json;

namespace Vendr.PaymentProviders.Dibs.Easy.Api.Models
{
    public class DibsCancel
    {
        [JsonProperty("amount")]
        public int Amount { get; set; }

        [JsonProperty("orderItems")]
        public DibsOrderItem[] OrderItems { get; set; }
    }
}
