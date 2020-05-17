using Vendr.Core.Web.PaymentProviders;

namespace Vendr.PaymentProviders.Dibs
{
    public class DibsEasyOneTimeSettings : DibsSettingsEasyBase
    {
        [PaymentProviderSetting(Name = "Auto Capture",
            Description = "Flag indicating whether to immediately capture the payment, or whether to just authorize the payment for later (manual) capture.",
            SortOrder = 2000)]
        public bool AutoCapture { get; set; }
    }
}