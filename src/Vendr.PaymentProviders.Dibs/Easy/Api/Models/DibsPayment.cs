using Newtonsoft.Json;
using System;

namespace Vendr.PaymentProviders.Dibs.Easy.Api.Models
{
    public class DibsPaymentDetails
    {
        [JsonProperty("payment")]
        public DibsPayment Payment { get; set; }
    }

    public class DibsPayment
    {
        [JsonProperty("checkout")]
        public DibsPaymentCheckout Checkout { get; set; }

        [JsonProperty("created")]
        public DateTime Created { get; set; }

        [JsonProperty("paymentId")]
        public string PaymentId { get; set; }
    }

    public class DibsPaymentCheckout
    {
        [JsonProperty("url")]
        public string Url { get; set; }
    }
}
