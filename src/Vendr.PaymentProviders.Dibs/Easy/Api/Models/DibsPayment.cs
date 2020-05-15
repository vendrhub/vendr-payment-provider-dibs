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

        [JsonProperty("orderDetails")]
        public DibsOrderDetails OrderDetails { get; set; }

        [JsonProperty("summary")]
        public DibsSummary Summary { get; set; }
    }

    public class DibsPaymentCheckout
    {
        [JsonProperty("url")]
        public string Url { get; set; }
    }

    public class DibsOrderDetails
    {
        [JsonProperty("amount")]
        public int Amount { get; set; }

        [JsonProperty("currency")]
        public string Currency { get; set; }

        [JsonProperty("reference")]
        public string Reference { get; set; }
    }

    public class DibsSummary
    {
        [JsonProperty("cancelledAmount")]
        public int CancelledAmount { get; set; }

        [JsonProperty("chargedAmount")]
        public int ChargedAmount { get; set; }

        [JsonProperty("refundedAmount")]
        public int RefundedAmount { get; set; }

        [JsonProperty("reservedAmount")]
        public int ReservedAmount { get; set; }
    }
}
