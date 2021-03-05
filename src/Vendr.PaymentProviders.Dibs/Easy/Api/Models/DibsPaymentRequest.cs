using Newtonsoft.Json;
using System.Collections.Generic;

namespace Vendr.PaymentProviders.Dibs.Easy.Api.Models
{
    public class DibsPaymentRequest
    {
        [JsonProperty("order")]
        public DibsOrder Order { get; set; }

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

        [JsonProperty("shippingAddress")]
        public DibsAddress ShippingAddress { get; set; }

        [JsonProperty("phoneNumber")]
        public DibsCustomerPhone PhoneNumber { get; set; }

        [JsonProperty("privatePerson")]
        public DibsCustomerName PrivatePerson { get; set; }

        [JsonProperty("company")]
        public DibsCompany Company { get; set; }
    }

    public class DibsAddress
    {
        [JsonProperty("addressLine1")]
        public string Line1 { get; set; }

        [JsonProperty("addressLine2")]
        public string Line2 { get; set; }

        [JsonProperty("postalCode")]
        public string PostalCode { get; set; }

        [JsonProperty("city")]
        public string City { get; set; }

        [JsonProperty("country")]
        public string Country { get; set; }
    }

    public class DibsCustomerPhone
    {
        [JsonProperty("prefix")]
        public string Prefix { get; set; }

        [JsonProperty("number")]
        public string Number { get; set; }
    }

    public class DibsCustomerName
    {
        [JsonProperty("firstName")]
        public string FirstName { get; set; }

        [JsonProperty("lastName")]
        public string LastName { get; set; }
    }

    public class DibsCompany
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("contact")]
        public DibsCustomerName Contact { get; set; }
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

        [JsonProperty("consumer")]
        public DibsConsumer Consumer { get; set; }
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
        public List<Dictionary<string, string>> Headers { get; set; }
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
