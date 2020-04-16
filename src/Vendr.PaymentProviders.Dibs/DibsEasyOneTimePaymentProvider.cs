using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using Vendr.Contrib.PaymentProviders.Dibs.Easy.Api.Models;
using Vendr.Contrib.PaymentProviders.Reepay.Api;
using Vendr.Core;
using Vendr.Core.Models;
using Vendr.Core.Web;
using Vendr.Core.Web.Api;
using Vendr.Core.Web.PaymentProviders;
using Vendr.PaymentProviders.Dibs;

namespace Vendr.Contrib.PaymentProviders
{
    [PaymentProvider("dibs-easy-checkout-onetime", "DIBS Easy (One Time)", "DIBS Easy payment provider for one time payments")]
    public class DibsEasyOneTimePaymentProvider : DibsPaymentProviderBase<DibsEasyOneTimeSettings>
    {
        public DibsEasyOneTimePaymentProvider(VendrContext vendr)
            : base(vendr)
        { }

        public override bool CanCancelPayments => true;
        public override bool CanCapturePayments => true;
        public override bool CanRefundPayments => true;
        public override bool CanFetchPaymentStatus => true;

        public override bool FinalizeAtContinueUrl => true;

        public override IEnumerable<TransactionMetaDataDefinition> TransactionMetaDataDefinitions => new[]{
            new TransactionMetaDataDefinition("dibsEasyPaymentId", "Dibs (Easy) Payment ID")
        };

        public override OrderReference GetOrderReference(HttpRequestBase request, DibsEasyOneTimeSettings settings)
        {
            try
            {

            }
            catch (Exception ex)
            {
                Vendr.Log.Error<DibsEasyOneTimePaymentProvider>(ex, "Dibs Easy - GetOrderReference");
            }

            return base.GetOrderReference(request, settings);
        }

        public override PaymentFormResult GenerateForm(OrderReadOnly order, string continueUrl, string cancelUrl, string callbackUrl, DibsEasyOneTimeSettings settings)
        {
            var currency = Vendr.Services.CurrencyService.GetCurrency(order.CurrencyId);
            var currencyCode = currency.Code.ToUpperInvariant();

            // Ensure currency has valid ISO 4217 code
            if (!Iso4217.CurrencyCodes.ContainsKey(currencyCode))
            {
                throw new Exception("Currency must be a valid ISO 4217 currency code: " + currency.Name);
            }

            var orderAmount = AmountToMinorUnits(order.TotalPrice.Value.WithTax);

            var paymentMethods = settings.PaymentMethods?.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                   .Where(x => !string.IsNullOrWhiteSpace(x))
                   .Select(s => s.Trim())
                   .ToArray();

            string paymentId = string.Empty;
            string paymentFormLink = string.Empty;

            try
            {
                var clientConfig = GetDibsEasyClientConfig(settings);
                var client = new DibsEasyClient(clientConfig);

                var items = order.OrderLines.Select(x => new
                {
                    reference = x.Sku,
                    name = x.Name,
                    quantity = (int)x.Quantity,
                    unit = "pcs",
                    unitPrice = AmountToMinorUnits(x.UnitPrice.Value.WithoutTax),
                    taxRate = AmountToMinorUnits(x.TaxRate.Value * 100),
                    grossTotalAmount = AmountToMinorUnits(x.TotalPrice.Value.WithTax),
                    netTotalAmount = AmountToMinorUnits(x.TotalPrice.Value.WithoutTax)
                });

                var data = new
                {
                    order = new
                    {
                        items = items,
                        amount =  orderAmount,
                        currency = currencyCode,
                        reference = order.OrderNumber
                    },
                    consumer = new
                    {
                        reference = order.CustomerInfo.CustomerReference,
                        email = order.CustomerInfo.Email
                    },
                    checkout = new
                    {
                        charge = false,
                        integrationType = "HostedPaymentPage",
                        returnUrl = continueUrl,
                        termsUrl = "https://www.mydomain.com/toc",
                        merchantHandlesConsumerData = true
                    }
                };

                // Create payment
                var payment = client.CreatePayment(data);

                // Get payment id
                paymentId = payment.PaymentId;

                var paymentDetails = client.GetPaymentDetails(paymentId);

                if (paymentDetails != null)
                {
                    var uriBuilder = new UriBuilder(paymentDetails.Payment.Checkout.Url);
                    var query = HttpUtility.ParseQueryString(uriBuilder.Query);

                    //if (!string.IsNullOrEmpty(settings.Language))
                    //{
                    //    query["language"] = settings.Language;
                    //}
                    query["language"] = "da-DK";

                    uriBuilder.Query = query.ToString();
                    paymentFormLink = uriBuilder.ToString();
                }
            }
            catch (Exception ex)
            {
                Vendr.Log.Error<DibsEasyOneTimePaymentProvider>(ex, "Dibs Easy - error creating payment.");
            }

            var checkoutKey = settings.TestMode ? settings.TestCheckoutKey : settings.LiveCheckoutKey;

            return new PaymentFormResult()
            {
                MetaData = new Dictionary<string, string>
                {
                    { "dibsEasyPaymentId", paymentId }
                },
                Form = new PaymentForm(paymentFormLink, FormMethod.Get)
                            //.WithJsFile($"https://{(settings.TestMode ? "test." : "")}checkout.dibspayment.eu/v1/checkout.js?v=1")
                            //.WithJs(@"
                            //    var elem = document.createElement('div');
                            //    elem.id = 'dibs-complete-content';
                            //    document.body.appendChild(elem);
        
                            //    var checkoutOptions = {
                            //        checkoutKey: '" + checkoutKey + @"',
                            //        paymentId : '" + paymentId + @"',
                            //        language: 'en-GB',
                            //        theme: {
                            //            textColor: 'blue'
                            //        }
                            //    };
                                
                            //    var checkout = new Dibs.Checkout(checkoutOptions);
                            //")
            };
        }

        public override CallbackResult ProcessCallback(OrderReadOnly order, HttpRequestBase request, DibsEasyOneTimeSettings settings)
        {
            try
            {
                // Process callback

                
            }
            catch (Exception ex)
            {
                Vendr.Log.Error<DibsEasyOneTimePaymentProvider>(ex, "Dibs Easy - ProcessCallback");
            }

            return CallbackResult.BadRequest();
        }

        public override ApiResult FetchPaymentStatus(OrderReadOnly order, DibsEasyOneTimeSettings settings)
        {
            // Get payment: https://tech.dibspayment.com/easy/api/paymentapi#getPayment

            try
            {
                //var clientConfig = GetDibsEasyClientConfig(settings);
                //var client = new ReepayClient(clientConfig);

                //// Get payment
                //var payment = client.GetPayment(order.OrderNumber);

                //return new ApiResult()
                //{
                //    TransactionInfo = new TransactionInfoUpdate()
                //    {
                //        TransactionId = GetTransactionId(payment),
                //        PaymentStatus = GetPaymentStatus(payment)
                //    }
                //};
            }
            catch (Exception ex)
            {
                Vendr.Log.Error<DibsEasyOneTimePaymentProvider>(ex, "Dibs Easy - FetchPaymentStatus");
            }

            return ApiResult.Empty;
        }

        public override ApiResult CancelPayment(OrderReadOnly order, DibsEasyOneTimeSettings settings)
        {
            // Cancel payment: https://tech.dibspayment.com/easy/api/paymentapi#cancelPayment

            try
            {
                //var clientConfig = GetDibsEasyClientConfig(settings);
                //var client = new DibsEasyClient(clientConfig);

                //// Cancel charge
                //var payment = client.CancelPayment(order.OrderNumber);

                //return new ApiResult()
                //{
                //    TransactionInfo = new TransactionInfoUpdate()
                //    {
                //        TransactionId = GetTransactionId(payment),
                //        PaymentStatus = GetPaymentStatus(payment)
                //    }
                //};
            }
            catch (Exception ex)
            {
                Vendr.Log.Error<DibsEasyOneTimePaymentProvider>(ex, "Dibs Easy - CancelPayment");
            }

            return ApiResult.Empty;
        }

        public override ApiResult CapturePayment(OrderReadOnly order, DibsEasyOneTimeSettings settings)
        {
            // Charge payment: https://tech.dibspayment.com/easy/api/paymentapi#chargePayment

            try
            {
                //var clientConfig = GetDibsEasyClientConfig(settings);
                //var client = new DibsEasyClient(clientConfig);

                //var data = new
                //{
                //    amount = AmountToMinorUnits(order.TransactionInfo.AmountAuthorized.Value)
                //};

                //// Settle charge
                //var payment = client.ChargePayment(order.OrderNumber, data);

                //return new ApiResult()
                //{
                //    TransactionInfo = new TransactionInfoUpdate()
                //    {
                //        TransactionId = GetTransactionId(payment),
                //        PaymentStatus = GetPaymentStatus(payment)
                //    }
                //};
            }
            catch (Exception ex)
            {
                Vendr.Log.Error<DibsEasyOneTimePaymentProvider>(ex, "Dibs Easy - CapturePayment");
            }

            return ApiResult.Empty;
        }

        public override ApiResult RefundPayment(OrderReadOnly order, DibsEasyOneTimeSettings settings)
        {
            // Refund payment: https://tech.dibspayment.com/easy/api/paymentapi#refundPayment

            try
            {
                //var clientConfig = GetDibsEasyClientConfig(settings);
                //var client = new DibsEasyClient(clientConfig);

                //var data = new
                //{
                //    invoice = order.OrderNumber,
                //    amount = AmountToMinorUnits(order.TransactionInfo.AmountAuthorized.Value)
                //};

                //// Refund charge
                //var refund = client.RefundPayment(data);

                //return new ApiResult()
                //{
                //    TransactionInfo = new TransactionInfoUpdate()
                //    {
                //        TransactionId = GetTransactionId(refund),
                //        PaymentStatus = GetPaymentStatus(refund)
                //    }
                //};
            }
            catch (Exception ex)
            {
                Vendr.Log.Error<DibsEasyOneTimePaymentProvider>(ex, "Dibs Easy - RefundPayment");
            }

            return ApiResult.Empty;
        }

        protected DibsEasyClientConfig GetDibsEasyClientConfig(DibsSettingsEasyBase settings)
        {
            var prefix = settings.TestMode ? "test-secret-key-" : "live-secret-key-";
            var secretKey = settings.TestMode ? settings.TestSecretKey : settings.LiveSecretKey;
            var auth = secretKey?.Trim().TrimStart(prefix.ToCharArray());

            return new DibsEasyClientConfig
            {
                BaseUrl = $"https://{(settings.TestMode ? "test." : "")}api.dibspayment.eu",
                Authorization = auth
                //WebhookSecret = settings.WebhookSecret
            };
        }
    }
}
