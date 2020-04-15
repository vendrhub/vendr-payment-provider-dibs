using Vendr.Core.Web.PaymentProviders;

namespace Vendr.PaymentProviders.Dibs
{
    public class DibsSettings : DibsSettingsBase
    {
        [PaymentProviderSetting(Name = "MD5 Key 1",
            Description = "MD5 Key 1 from the Nets administration portal.",
            SortOrder = 500)]
        public string MD5Key1 { get; set; }

        [PaymentProviderSetting(Name = "MD5 Key 2",
            Description = "MD5 Key 2 from the Nets administration portal.",
            SortOrder = 600)]
        public string MD5Key2 { get; set; }

        [PaymentProviderSetting(Name = "API Username",
            Description = "The API Username from the Nets administration portal.",
            SortOrder = 700)]
        public string ApiUsername { get; set; }

        [PaymentProviderSetting(Name = "API Password",
            Description = "The API Password from the Nets administration portal.",
            SortOrder = 800)]
        public string ApiPassword { get; set; }

        [PaymentProviderSetting(Name = "Language",
            Description = "The language of the payment portal to display.",
            SortOrder = 900)]
        public string Lang { get; set; }

        [PaymentProviderSetting(Name = "Accepted Pay Types",
            Description = "A comma separated list of Pay Types to accept.",
            SortOrder = 1000)]
        public string PayTypes { get; set; }

        [PaymentProviderSetting(Name = "Calculate Fee",
            Description = "Flag indicating whether to automatically calculate and apply the fee from the acquirer.",
            SortOrder = 1100)]
        public bool CalcFee { get; set; }

        [PaymentProviderSetting(Name = "Capture",
            Description = "Flag indicating whether to immediately capture the payment, or whether to just authorize the payment for later (manual) capture.",
            SortOrder = 1200)]
        public bool Capture { get; set; }

        [PaymentProviderSetting(Name = "Decorator",
            Description = "Specifies which of the pre-built decorators to use. Possible values are \"default\", \"basal\", \"rich\" and \"responsive\".",
            SortOrder = 1300)]
        public string Decorator { get; set; }
    }
}