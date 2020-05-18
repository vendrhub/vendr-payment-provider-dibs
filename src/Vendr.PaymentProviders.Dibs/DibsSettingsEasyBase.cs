using Vendr.Core.Web.PaymentProviders;

namespace Vendr.PaymentProviders.Dibs
{
    public class DibsSettingsEasyBase : DibsSettingsBase
    {
        [PaymentProviderSetting(Name = "Accepted Payment Methods",
            Description = "A comma separated list of Payment Methods to accept.",
            SortOrder = 1000)]
        public string PaymentMethods { get; set; }

        [PaymentProviderSetting(Name = "Terms URL",
            Description = "The URL to your terms and conditions.",
            SortOrder = 1100)]
        public string TermsUrl { get; set; }

        [PaymentProviderSetting(Name = "Test Secret Key",
            Description = "Your test DIBS secret key",
            SortOrder = 1200)]
        public string TestSecretKey { get; set; }

        [PaymentProviderSetting(Name = "Live Secret Key",
            Description = "Your live DIBS secret key",
            SortOrder = 1300)]
        public string LiveSecretKey { get; set; }

        [PaymentProviderSetting(Name = "Test Checkout Key",
            Description = "Your test DIBS checkout key",
            SortOrder = 1400)]
        public string TestCheckoutKey { get; set; }

        [PaymentProviderSetting(Name = "Live Checkout Key",
            Description = "Your live DIBS checkout key",
            SortOrder = 1500)]
        public string LiveCheckoutKey { get; set; }
    }
}
