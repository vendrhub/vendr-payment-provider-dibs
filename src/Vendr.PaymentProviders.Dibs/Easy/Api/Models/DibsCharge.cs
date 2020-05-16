using Newtonsoft.Json;

namespace Vendr.PaymentProviders.Dibs.Easy.Api.Models
{
    public class DibsCharge
    {
        [JsonProperty("chargeId")]
        public string ChargeId { get; set; }

        [JsonProperty("invoice")]
        public DibsInvoice Invoice { get; set; }
    }

    public class DibsInvoice
    {
        [JsonProperty("invoiceNumber")]
        public string InvoiceNumber { get; set; }
    }
}
