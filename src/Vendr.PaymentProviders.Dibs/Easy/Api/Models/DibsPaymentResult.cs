using Newtonsoft.Json;

namespace Vendr.PaymentProviders.Dibs.Easy.Api.Models
{
    public class DibsPaymentResult
    {
        [JsonProperty("paymentId")]
        public string PaymentId { get; set; }
    }
}
