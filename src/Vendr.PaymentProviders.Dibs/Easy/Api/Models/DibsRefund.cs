using Newtonsoft.Json;

namespace Vendr.PaymentProviders.Dibs.Easy.Api.Models
{
    public class DibsRefund
    {
        [JsonProperty("refundId")]
        public string RefundId { get; set; }
    }
}
