using Vendr.Core.Web.PaymentProviders;

namespace Vendr.PaymentProviders.Dibs
{
    public class DibsSettingsEasyBase : DibsSettingsBase
    {
        [PaymentProviderSetting(Name = "Accepted Payment Methods",
            Description = "A comma separated list of Payment Methods to accept.",
            SortOrder = 1000)]
        public string PaymentMethods { get; set; }

        [PaymentProviderSetting(Name = "Billing Company Property Alias",
            Description = "The order property alias containing company of the billing address (optional).",
            SortOrder = 1100)]
        public string BillingCompanyPropertyAlias { get; set; }

        [PaymentProviderSetting(Name = "Billing Phone Property Alias",
            Description = "The order property alias containing phone of the billing address.",
            SortOrder = 1200)]
        public string BillingPhonePropertyAlias { get; set; }

        [PaymentProviderSetting(Name = "Shipping Address (Line 1) Property Alias",
            Description = "The order property alias containing line 1 of the shipping address.",
            SortOrder = 1300)]
        public string ShippingAddressLine1PropertyAlias { get; set; }

        [PaymentProviderSetting(Name = "Shipping Address (Line 2) Property Alias",
            Description = "The order property alias containing line 2 of the shipping address.",
            SortOrder = 1400)]
        public string ShippingAddressLine2PropertyAlias { get; set; }

        [PaymentProviderSetting(Name = "Shipping Address Zip Code Property Alias",
            Description = "The order property alias containing the zip code of the shipping address.",
            SortOrder = 1500)]
        public string ShippingAddressZipCodePropertyAlias { get; set; }

        [PaymentProviderSetting(Name = "Shipping Address City Property Alias",
            Description = "The order property alias containing the city of the shipping address.",
            SortOrder = 1600)]
        public string ShippingAddressCityPropertyAlias { get; set; }

        [PaymentProviderSetting(Name = "Terms URL",
            Description = "The URL to your terms and conditions.",
            SortOrder = 1700)]
        public string TermsUrl { get; set; }

        [PaymentProviderSetting(Name = "Test Secret Key",
            Description = "Your test DIBS secret key",
            SortOrder = 1800)]
        public string TestSecretKey { get; set; }

        [PaymentProviderSetting(Name = "Live Secret Key",
            Description = "Your live DIBS secret key",
            SortOrder = 1900)]
        public string LiveSecretKey { get; set; }

        [PaymentProviderSetting(Name = "Test Checkout Key",
            Description = "Your test DIBS checkout key",
            SortOrder = 2000)]
        public string TestCheckoutKey { get; set; }

        [PaymentProviderSetting(Name = "Live Checkout Key",
            Description = "Your live DIBS checkout key",
            SortOrder = 2100)]
        public string LiveCheckoutKey { get; set; }
    }
}
