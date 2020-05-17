using Newtonsoft.Json;

namespace Vendr.PaymentProviders.Dibs.Easy.Api.Models
{
    public class DibsPaymentBase
    {
        [JsonProperty("checkout")]
        public DibsPaymentCheckout Checkout { get; set; }
    }

    public class DibsPaymentCheckout
    {
        [JsonProperty("url")]
        public string Url { get; set; }
    }
}
