using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
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
using Vendr.PaymentProviders.Dibs.Easy.Api.Models;

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

                var items = order.OrderLines.Select(x => new DibsOrderItem
                {
                    Reference = x.Sku,
                    Name = x.Name,
                    Quantity = (int)x.Quantity,
                    Unit = "pcs",
                    UnitPrice = (int)AmountToMinorUnits(x.UnitPrice.Value.WithoutTax),
                    TaxRate = (int)AmountToMinorUnits(x.TaxRate.Value * 100),
                    GrossTotalAmount = (int)AmountToMinorUnits(x.TotalPrice.Value.WithTax),
                    NetTotalAmount = (int)AmountToMinorUnits(x.TotalPrice.Value.WithoutTax)
                });

                var data = new DibsPaymentRequest
                {
                    Order = new DibsOrder
                    {
                        Reference = order.OrderNumber,
                        Currency = currencyCode,
                        Amount = (int)orderAmount,
                        Items = items.ToArray()
                    },
                    Consumer = new DibsConsumer
                    {
                        Reference = order.CustomerInfo.CustomerReference,
                        Email = order.CustomerInfo.Email
                    },
                    Checkout = new DibsCheckout
                    {
                        Charge = false,
                        IntegrationType = "HostedPaymentPage",
                        ReturnUrl = continueUrl,
                        TermsUrl = "https://www.mydomain.com/toc",
                        Appearance = new DibsAppearance
                        {
                            DisplayOptions = new DibsDisplayOptions
                            {
                                ShowMerchantName = true,
                                ShowOrderSummary = true
                            }
                        },
                        MerchantHandlesConsumerData = true
                    },
                    Notifications = new DibsNotifications
                    {
                        Webhooks = new DibsWebhook[]
                        {
                            new DibsWebhook
                            {
                                EventName = "payment.checkout.completed",
                                Url = callbackUrl.Replace("http://", "https://"),
                                Authorization = "12345678"
                            }
                        }
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

                    if (!string.IsNullOrEmpty(settings.Language))
                    {
                        query["language"] = settings.Language;
                    }

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
                //Form = new PaymentForm(continueUrl, FormMethod.Get)
                //            .WithAttribute("onsubmit", "return handleDibsEasyCheckout(event)")
                //            .WithJsFile($"https://{(settings.TestMode ? "test." : "")}checkout.dibspayment.eu/v1/checkout.js?v=1")
                //            .WithJs(@"

                //                window.handleDibsEasyCheckout = function (e) {
                //                    e.preventDefault();

                //                    //var elem = document.createElement('div');
                //                    //elem.id = 'dibs-complete-checkout';
                //                    //document.body.appendChild(elem);
        
                //                    var checkoutOptions = {
                //                        checkoutKey: '" + checkoutKey + @"',
                //                        paymentId : '" + paymentId + @"',
                //                        //containerId: 'dibs-complete-checkout',
                //                        language: 'en-GB',
                //                        theme: {
                //                            textColor: 'blue'
                //                        }
                //                    };

                //                    var checkout = new Dibs.Checkout(checkoutOptions);
                                    
                //                    // Success
                //                    checkout.on('payment-completed', function(response) {
                //                        window.location = '" + continueUrl + @"' + '&paymentId=' + response.paymentId;
                //                    });
                                    
                //                    // Cancel
                //                    checkout.on('payment-declined', function(response) {
                //                        window.location = '" + cancelUrl + @"' + '&paymentId=' + response.paymentId;
                //                    });
                                    
                //                    return false;
                //                }
                //            ")
            };
        }

        public override CallbackResult ProcessCallback(OrderReadOnly order, HttpRequestBase request, DibsEasyOneTimeSettings settings)
        {
            try
            {
                // Process callback
                
                var clientConfig = GetDibsEasyClientConfig(settings);
                var client = new DibsEasyClient(clientConfig);
                var dibsEvent = GetDibsWebhookEvent(client, request);

                if (dibsEvent != null && dibsEvent.Event == "payment.checkout.completed")
                {
                    var paymentId = dibsEvent.Data?.SelectToken("paymentId")?.Value<string>();

                    var paymentDetails = client.GetPaymentDetails(paymentId);

                    //return CallbackResult.Ok(new TransactionInfo
                    //{
                    //    TransactionId = dibsEvent.Transaction,
                    //    AmountAuthorized = order.TotalPrice.Value.WithTax,
                    //    PaymentStatus = PaymentStatus.Authorized
                    //});
                }
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
                //var client = new DibsEasyClient(clientConfig);

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

        protected DibsWebhookEvent GetDibsWebhookEvent(DibsEasyClient client, HttpRequestBase request)
        {
            DibsWebhookEvent dibsWebhookEvent = null;

            if (HttpContext.Current.Items["Vendr_DibsEasyWebhookEvent"] != null)
            {
                dibsWebhookEvent = (DibsWebhookEvent)HttpContext.Current.Items["Vendr_DibsEasyWebhookEvent"];
            }
            else
            {
                try
                {
                    if (request.InputStream.CanSeek)
                        request.InputStream.Seek(0, SeekOrigin.Begin);

                    using (var reader = new StreamReader(request.InputStream))
                    {
                        var json = reader.ReadToEnd();


                        //dibsWebhookEvent = client.ParseWebhookEvent(request);
                    }
                }
                catch (Exception ex)
                {
                    Vendr.Log.Error<DibsEasyOneTimePaymentProvider>(ex, "Dibs Easy - GetDibsWebhookEvent");
                }

                HttpContext.Current.Items["Vendr_DibsEasyWebhookEvent"] = dibsWebhookEvent;
            }

            return dibsWebhookEvent;
        }

    }
}
