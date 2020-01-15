using System;
using System.Web;
using System.Web.Mvc;
using Vendr.Core;
using Vendr.Core.Models;
using Vendr.Core.Web.Api;
using Vendr.Core.Web.PaymentProviders;

namespace Vendr.PaymentProviders.Dibs
{
    [PaymentProvider("dibs-d2", "DIBS D2", "DIBS D2 payment provider for one time payments")]
    public class DibsPaymentProvider : PaymentProviderBase<DibsSettings>
    {
        public DibsPaymentProvider(VendrContext vendr)
            : base(vendr)
        { }

        public override bool CanCancelPayments => true;
        public override bool CanCapturePayments => true;
        public override bool FinalizeAtContinueUrl => true;

        public override PaymentForm GenerateForm(OrderReadOnly order, string continueUrl, string cancelUrl, string callbackUrl, DibsSettings settings)
        {
            return new PaymentForm(continueUrl, FormMethod.Post);
        }

        public override string GetCancelUrl(OrderReadOnly order, DibsSettings settings)
        {
            return string.Empty;
        }

        public override string GetErrorUrl(OrderReadOnly order, DibsSettings settings)
        {
            return string.Empty;
        }

        public override string GetContinueUrl(OrderReadOnly order, DibsSettings settings)
        {
            settings.MustNotBeNull("settings");
            settings.ContinueUrl.MustNotBeNull("settings.ContinueUrl");

            return settings.ContinueUrl;
        }

        public override CallbackResponse ProcessCallback(OrderReadOnly order, HttpRequestBase request, DibsSettings settings)
        {
            return new CallbackResponse
            {
                TransactionInfo = new TransactionInfo
                {
                    AmountAuthorized = order.TotalPrice.Value.WithTax,
                    TransactionFee = 0m,
                    TransactionId = Guid.NewGuid().ToString("N"),
                    PaymentStatus = PaymentStatus.Authorized
                }
            };
        }

        public override ApiResponse CancelPayment(OrderReadOnly order, DibsSettings settings)
        {
            return new ApiResponse(order.TransactionInfo.TransactionId, PaymentStatus.Cancelled);
        }

        public override ApiResponse CapturePayment(OrderReadOnly order, DibsSettings settings)
        {
            return new ApiResponse(order.TransactionInfo.TransactionId, PaymentStatus.Captured);
        }
    }
}
