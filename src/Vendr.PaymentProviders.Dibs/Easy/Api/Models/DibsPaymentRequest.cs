using Newtonsoft.Json;
using System.Collections.Generic;

namespace Vendr.PaymentProviders.Dibs.Easy.Api.Models
{
    public class DibsPaymentRequest
    {
        [JsonProperty("order")]
        public DibsOrder Order { get; set; }

        [JsonProperty("consumer")]
        public DibsConsumer Consumer { get; set; }

        [JsonProperty("checkout")]
        public DibsCheckout Checkout { get; set; }

        [JsonProperty("notifications")]
        public DibsNotifications Notifications { get; set; }

        [JsonProperty("paymentMethods")]
        public DibsPaymentMethod[] PaymentMethods { get; set; }
    }

    public class DibsConsumer
    {
        [JsonProperty("reference")]
        public string Reference { get; set; }

        [JsonProperty("email")]
        public string Email { get; set; }
    }

    public class DibsCheckout
    {
        [JsonProperty("charge")]
        public bool Charge { get; set; }

        [JsonProperty("publicDevice")]
        public bool PublicDevice { get; set; }

        [JsonProperty("integrationType")]
        public string IntegrationType { get; set; }

        [JsonProperty("cancelUrl")]
        public string CancelUrl { get; set; }

        [JsonProperty("returnUrl")]
        public string ReturnUrl { get; set; }

        [JsonProperty("termsUrl")]
        public string TermsUrl { get; set; }

        [JsonProperty("appearance")]
        public DibsAppearance Appearance { get; set; }

        [JsonProperty("merchantHandlesConsumerData")]
        public bool MerchantHandlesConsumerData { get; set; }
    }

    public class DibsNotifications
    {
        [JsonProperty("webhooks")]
        public DibsWebhook[] Webhooks { get; set; }
        
    }

    public class DibsWebhook
    {
        [JsonProperty("eventName")]
        public string EventName { get; set; }

        [JsonProperty("url")]
        public string Url { get; set; }

        [JsonProperty("authorization")]
        public string Authorization { get; set; }

        [JsonProperty("headers")]
        public Dictionary<string, string> Headers { get; set; }
    }

    public class DibsAppearance
    {
        [JsonProperty("displayOptions")]
        public DibsDisplayOptions DisplayOptions { get; set; }

        [JsonProperty("textOptions")]
        public DibsTextOptions TextOptions { get; set; }
    }

    public class DibsDisplayOptions
    {
        [JsonProperty("showMerchantName")]
        public bool ShowMerchantName { get; set; }

        [JsonProperty("showOrderSummary")]
        public bool ShowOrderSummary { get; set; }
    }

    public class DibsTextOptions
    {
        [JsonProperty("completePaymentButtonText")]
        public string CompletePaymentButtonText { get; set; }
    }
}
