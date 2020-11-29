using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
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

        // We'll finalize via webhook callback
        public override bool FinalizeAtContinueUrl => false;

        public override IEnumerable<TransactionMetaDataDefinition> TransactionMetaDataDefinitions => new[]{
            new TransactionMetaDataDefinition("dibsEasyPaymentId", "Dibs (Easy) Payment ID"),
            new TransactionMetaDataDefinition("dibsEasyChargeId", "Dibs (Easy) Charge ID"),
            new TransactionMetaDataDefinition("dibsEasyRefundId", "Dibs (Easy) Refund ID"),
            new TransactionMetaDataDefinition("dibsEasyWebhookGuid", "Dibs (Easy) Webhook Guid")
        };

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

            var paymentMethodId = order.PaymentInfo.PaymentMethodId;
            var paymentMethod = paymentMethodId != null ? Vendr.Services.PaymentMethodService.GetPaymentMethod(paymentMethodId.Value) : null;

            string paymentId = string.Empty;
            string paymentFormLink = string.Empty;

            var webhookGuid = Guid.NewGuid();

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

                var shippingMethod = Vendr.Services.ShippingMethodService.GetShippingMethod(order.ShippingInfo.ShippingMethodId.Value);
                if (shippingMethod != null)
                {
                    items = items.Append(new DibsOrderItem
                    {
                        Reference = shippingMethod.Sku,
                        Name = shippingMethod.Name,
                        Quantity = 1,
                        Unit = "pcs",
                        UnitPrice = (int)AmountToMinorUnits(order.ShippingInfo.TotalPrice.Value.WithoutTax),
                        TaxRate = (int)AmountToMinorUnits(order.ShippingInfo.TaxRate.Value * 100),
                        GrossTotalAmount = (int)AmountToMinorUnits(order.ShippingInfo.TotalPrice.Value.WithTax),
                        NetTotalAmount = (int)AmountToMinorUnits(order.ShippingInfo.TotalPrice.Value.WithoutTax)
                    });
                }

                if (order.TransactionAmount.Adjustments.Count > 0)
                {
                    // Custom Price adjustments
                    
                    var priceAdjustments = order.TransactionAmount.Adjustments.OfType<PriceAdjustment>();

                    if (priceAdjustments?.Any() == true)
                    {
                        foreach (var price in priceAdjustments)
                        {
                            items = items.Append(new DibsOrderItem
                            {
                                Reference = "",
                                Name = price.Name,
                                Quantity = 1,
                                Unit = "pcs",
                                GrossTotalAmount = (int)AmountToMinorUnits(price.Price),
                            });
                        }
                    }

                    var discountAdjustments = order.TransactionAmount.Adjustments.OfType<DiscountAdjustment>();
                    if (discountAdjustments.Any())
                    {
                        foreach (var discount in discountAdjustments)
                        {
                            items = items.Append(new DibsOrderItem
                            {
                                Reference = discount.DiscountId.ToString(),
                                Name = discount.DiscountName,
                                Quantity = 1,
                                Unit = "pcs",
                                GrossTotalAmount = (int)AmountToMinorUnits(discount.Price),
                            });
                        }
                    }

                    // Gift Card adjustments
                    var giftCardAdjustments = order.TransactionAmount.Adjustments.OfType<GiftCardAdjustment>();
                    if (giftCardAdjustments.Any())
                    {
                        foreach (var giftcard in giftCardAdjustments)
                        {
                            items = items.Append(new DibsOrderItem
                            {
                                Reference = giftcard.GiftCardId.ToString(),
                                Name = giftcard.GiftCardCode,
                                Quantity = 1,
                                Unit = "pcs",
                                GrossTotalAmount = (int)AmountToMinorUnits(giftcard.Amount),
                            });
                        }
                    }
                }

                //if (order.GiftCards.Count > 0)
                //{
                //    foreach (var giftcard in order.GiftCards)
                //    {
                //        items = items.Append(new DibsOrderItem
                //        {
                //            Reference = giftcard.Code,
                //            Name = $"Gift Card - {giftcard.Code}",
                //            Quantity = 1,
                //            Unit = "pcs",
                //            GrossTotalAmount = -Math.Abs((int)AmountToMinorUnits(giftcard.Amount)),
                //        });
                //    }
                //}

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
                        Charge = settings.AutoCapture,
                        IntegrationType = "HostedPaymentPage",
                        ReturnUrl = callbackUrl,
                        TermsUrl = settings.TermsUrl,
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
                                Url = callbackUrl.Replace("http://", "https://"), // Must be https 
                                Authorization = webhookGuid.ToString()
                            }
                        }
                    } //,
                    //PaymentMethods = new DibsPaymentMethod[]
                    //{
                    //    new DibsPaymentMethod
                    //    {
                    //        Name = paymentMethod?.Name,
                    //        Fee = new DibsPaymentFee
                    //        {
                    //            Name = paymentMethod?.Name,
                    //            Reference = paymentMethod?.Alias,
                    //            Quantity = 1,
                    //            Unit = "pcs",
                    //            UnitPrice = (int)AmountToMinorUnits(order.PaymentInfo.TotalPrice.Value.WithoutTax),
                    //            TaxAmount = (int)AmountToMinorUnits(order.PaymentInfo.TotalPrice.Value.Tax),
                    //            TaxRate = (int)AmountToMinorUnits(order.PaymentInfo.TaxRate.Value * 100),
                    //            GrossTotalAmount = (int)AmountToMinorUnits(order.PaymentInfo.TotalPrice.Value.WithTax),
                    //            NetTotalAmount = (int)AmountToMinorUnits(order.PaymentInfo.TotalPrice.Value.WithoutTax)
                    //        }
                    //    }
                    //}
                };

                // Create payment
                var payment = client.CreatePayment(data);

                // Get payment id
                paymentId = payment.PaymentId;

                var paymentDetails = client.GetPayment(paymentId);
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
                    { "dibsEasyPaymentId", paymentId },
                    { "dibsEasyWebhookGuid", webhookGuid.ToString() },
                    { "dibsEasyContinueUrl", continueUrl },
                    { "dibsEasyCancelUrl", cancelUrl }
                },
                Form = new PaymentForm(paymentFormLink, FormMethod.Get)

                // Embedded checkout
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
                    var qs1 = request.QueryString;
                    var qs2 = request.RequestContext.HttpContext.Request.QueryString;

                    var status = request.QueryString["status"];

                    // Verify "Authorization" header returned from webhook
                    if (order.Properties["dibsEasyWebhookGuid"]?.Value == request.Headers["Authorization"])
                    {
                        var paymentId = dibsEvent.Data?.SelectToken("paymentId")?.Value<string>();
                        var payment = !string.IsNullOrEmpty(paymentId) ? client.GetPayment(paymentId) : null;
                        if (payment != null)
                        {
                            var continueUrl = order.Properties["dibsEasyContinueUrl"]?.Value;

                            var successResponse = new HttpResponseMessage(HttpStatusCode.Moved) // or HttpStatusCode.Redirect
                            {
                                Content = new StringContent("")
                            };
                            successResponse.Headers.Location = new Uri(continueUrl);

                            return new CallbackResult
                            {
                                HttpResponse = successResponse,
                                TransactionInfo = new TransactionInfo
                                {
                                    TransactionId = paymentId,
                                    AmountAuthorized = order.TotalPrice.Value.WithTax,
                                    PaymentStatus = GetPaymentStatus(payment)
                                }
                            };

                            //return CallbackResult.Ok(new TransactionInfo
                            //{
                            //    TransactionId = paymentId,
                            //    AmountAuthorized = order.TotalPrice.Value.WithTax,
                            //    PaymentStatus = GetPaymentStatus(payment)
                            //});
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Vendr.Log.Error<DibsEasyOneTimePaymentProvider>(ex, "Dibs Easy - ProcessCallback");
            }

            var errorUrl = order.Properties["dibsEasyCancelUrl"]?.Value;

            var errorResponse = new HttpResponseMessage(HttpStatusCode.Redirect)
            {
                Content = new StringContent("")
            };
            errorResponse.Headers.Location = new Uri(errorUrl);

            return new CallbackResult
            {
                HttpResponse = errorResponse
            };

            // return CallbackResult.BadRequest();
        }

        public override ApiResult FetchPaymentStatus(OrderReadOnly order, DibsEasyOneTimeSettings settings)
        {
            // Get payment: https://tech.dibspayment.com/easy/api/paymentapi#getPayment

            try
            {
                var clientConfig = GetDibsEasyClientConfig(settings);
                var client = new DibsEasyClient(clientConfig);

                var transactionId = order.TransactionInfo.TransactionId;

                // Get payment
                var payment = client.GetPayment(transactionId);
                if (payment != null)
                {
                    return new ApiResult()
                    {
                        TransactionInfo = new TransactionInfoUpdate()
                        {
                            TransactionId = GetTransactionId(payment),
                            PaymentStatus = GetPaymentStatus(payment)
                        }
                    };
                }
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
                var clientConfig = GetDibsEasyClientConfig(settings);
                var client = new DibsEasyClient(clientConfig);

                var transactionId = order.TransactionInfo.TransactionId;

                var data = new
                {
                    amount = AmountToMinorUnits(order.TransactionInfo.AmountAuthorized.Value)
                };

                // Cancel charge
                client.CancelPayment(transactionId, data);

                return new ApiResult()
                {
                    TransactionInfo = new TransactionInfoUpdate()
                    {
                        TransactionId = transactionId,
                        PaymentStatus = PaymentStatus.Cancelled
                    }
                };
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
                var clientConfig = GetDibsEasyClientConfig(settings);
                var client = new DibsEasyClient(clientConfig);

                var transactionId = order.TransactionInfo.TransactionId;

                var data = new
                {
                    amount = AmountToMinorUnits(order.TransactionInfo.AmountAuthorized.Value)
                };

                var result = client.ChargePayment(transactionId, data);
                if (result != null)
                {
                    return new ApiResult()
                    {
                        MetaData = new Dictionary<string, string>
                        {
                            { "dibsEasyChargeId", result.ChargeId }
                        },
                        TransactionInfo = new TransactionInfoUpdate()
                        {
                            TransactionId = transactionId,
                            PaymentStatus = PaymentStatus.Captured
                        }
                    };
                }
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
                var clientConfig = GetDibsEasyClientConfig(settings);
                var client = new DibsEasyClient(clientConfig);

                var transactionId = order.TransactionInfo.TransactionId;
                var chargeId = order.Properties["dibsEasyChargeId"]?.Value;

                var data = new
                {
                    invoice = order.OrderNumber,
                    amount = AmountToMinorUnits(order.TransactionInfo.AmountAuthorized.Value)
                };

                var result = client.RefundPayment(chargeId, data);
                if (result != null)
                {
                    return new ApiResult()
                    {
                        MetaData = new Dictionary<string, string>
                        {
                            { "dibsEasyRefundId", result.RefundId }
                        },
                        TransactionInfo = new TransactionInfoUpdate()
                        {
                            TransactionId = transactionId,
                            PaymentStatus = PaymentStatus.Refunded
                        }
                    };
                }
            }
            catch (Exception ex)
            {
                Vendr.Log.Error<DibsEasyOneTimePaymentProvider>(ex, "Dibs Easy - RefundPayment");
            }

            return ApiResult.Empty;
        }

        protected string GetTransactionId(DibsPaymentDetails paymentDetails)
        {
            return paymentDetails?.Payment?.PaymentId;
        }

        protected PaymentStatus GetPaymentStatus(DibsPaymentDetails paymentDetails)
        {
            var payment = paymentDetails.Payment;

            if (payment.Summary.RefundedAmount > 0)
                return PaymentStatus.Refunded;

            if (payment.Summary.CancelledAmount > 0)
                return PaymentStatus.Cancelled;

            if (payment.Summary.ChargedAmount > 0)
                return PaymentStatus.Captured;

            if (payment.Summary.ReservedAmount > 0)
                return PaymentStatus.Authorized;

            return PaymentStatus.Initialized;
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

                        if (!string.IsNullOrEmpty(json))
                        {
                            dibsWebhookEvent = JsonConvert.DeserializeObject<DibsWebhookEvent>(json);
                        }
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
