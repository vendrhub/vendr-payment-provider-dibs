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
            new TransactionMetaDataDefinition("dibsEasyWebhookAuthKey", "Dibs (Easy) Webhook Authorization")
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

            var orderAmount = AmountToMinorUnits(order.TransactionAmount.Value);

            var paymentMethods = settings.PaymentMethods?.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                   .Where(x => !string.IsNullOrWhiteSpace(x))
                   .Select(s => s.Trim())
                   .ToArray();

            var paymentMethodId = order.PaymentInfo.PaymentMethodId;
            var paymentMethod = paymentMethodId != null ? Vendr.Services.PaymentMethodService.GetPaymentMethod(paymentMethodId.Value) : null;

            string paymentId = string.Empty;
            string paymentFormLink = string.Empty;

            var webhookAuthKey = Guid.NewGuid().ToString();

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
                    TaxAmount = (int)AmountToMinorUnits(x.TotalPrice.Value.Tax),
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
                        TaxAmount = (int)AmountToMinorUnits(order.ShippingInfo.TotalPrice.Value.Tax),
                        GrossTotalAmount = (int)AmountToMinorUnits(order.ShippingInfo.TotalPrice.Value.WithTax),
                        NetTotalAmount = (int)AmountToMinorUnits(order.ShippingInfo.TotalPrice.Value.WithoutTax)
                    });
                }

                // Check adjustments on subtotal price
                if (order.SubtotalPrice.Adjustments.Count > 0)
                {
                    // Discounts
                    var discountAdjustments = order.SubtotalPrice.Adjustments.OfType<DiscountAdjustment>();
                    if (discountAdjustments.Any())
                    {
                        foreach (var discount in discountAdjustments)
                        {
                            var taxRate = (discount.Price.Tax / discount.Price.WithoutTax) * 100;

                            items = items.Append(new DibsOrderItem
                            {
                                Reference = discount.DiscountId.ToString(),
                                Name = discount.DiscountName,
                                Quantity = 1,
                                Unit = "pcs",
                                UnitPrice = (int)AmountToMinorUnits(discount.Price.WithoutTax),
                                TaxRate = (int)AmountToMinorUnits(taxRate),
                                TaxAmount = (int)AmountToMinorUnits(discount.Price.Tax),
                                GrossTotalAmount = (int)AmountToMinorUnits(discount.Price.WithTax),
                                NetTotalAmount = (int)AmountToMinorUnits(discount.Price.WithoutTax)
                            });
                        }
                    }

                    // Custom price adjustments
                    var priceAdjustments = order.SubtotalPrice.Adjustments.Except(discountAdjustments).OfType<PriceAdjustment>();
                    if (priceAdjustments.Any())
                    {
                        foreach (var adjustment in priceAdjustments)
                        {
                            var reference = Guid.NewGuid().ToString();
                            var taxRate = (adjustment.Price.Tax / adjustment.Price.WithoutTax) * 100;

                            items = items.Append(new DibsOrderItem
                            {
                                Reference = reference,
                                Name = adjustment.Name,
                                Quantity = 1,
                                Unit = "pcs",
                                UnitPrice = (int)AmountToMinorUnits(adjustment.Price.WithoutTax),
                                TaxRate = (int)AmountToMinorUnits(taxRate),
                                TaxAmount = (int)AmountToMinorUnits(adjustment.Price.Tax),
                                GrossTotalAmount = (int)AmountToMinorUnits(adjustment.Price.WithTax),
                                NetTotalAmount = (int)AmountToMinorUnits(adjustment.Price.WithoutTax)
                            });
                        }
                    }
                }

                // Check adjustments on total price
                if (order.TotalPrice.Adjustments.Count > 0)
                {
                    // Discounts
                    var discountAdjustments = order.TotalPrice.Adjustments.OfType<DiscountAdjustment>();
                    if (discountAdjustments.Any())
                    {
                        foreach (var discount in discountAdjustments)
                        {
                            var taxRate = (discount.Price.Tax / discount.Price.WithoutTax) * 100;

                            items = items.Append(new DibsOrderItem
                            {
                                Reference = discount.DiscountId.ToString(),
                                Name = discount.DiscountName,
                                Quantity = 1,
                                Unit = "pcs",
                                UnitPrice = (int)AmountToMinorUnits(discount.Price.WithoutTax),
                                TaxRate = (int)AmountToMinorUnits(taxRate),
                                TaxAmount = (int)AmountToMinorUnits(discount.Price.Tax),
                                GrossTotalAmount = (int)AmountToMinorUnits(discount.Price.WithTax),
                                NetTotalAmount = (int)AmountToMinorUnits(discount.Price.WithoutTax)
                            });
                        }
                    }

                    // Custom price adjustments
                    var priceAdjustments = order.TotalPrice.Adjustments.Except(discountAdjustments).OfType<PriceAdjustment>();
                    if (priceAdjustments.Any())
                    {
                        foreach (var adjustment in priceAdjustments)
                        {
                            var reference = Guid.NewGuid().ToString();
                            var taxRate = (adjustment.Price.Tax / adjustment.Price.WithoutTax) * 100;

                            items = items.Append(new DibsOrderItem
                            {
                                Reference = reference,
                                Name = adjustment.Name,
                                Quantity = 1,
                                Unit = "pcs",
                                UnitPrice = (int)AmountToMinorUnits(adjustment.Price.WithoutTax),
                                TaxRate = (int)AmountToMinorUnits(taxRate),
                                TaxAmount = (int)AmountToMinorUnits(adjustment.Price.Tax),
                                GrossTotalAmount = (int)AmountToMinorUnits(adjustment.Price.WithTax),
                                NetTotalAmount = (int)AmountToMinorUnits(adjustment.Price.WithoutTax)
                            });
                        }
                    }
                }

                // Check adjustments on transaction amount
                if (order.TransactionAmount.Adjustments.Count > 0)
                {
                    // Gift Card adjustments
                    var giftCardAdjustments = order.TransactionAmount.Adjustments.OfType<GiftCardAdjustment>();
                    if (giftCardAdjustments.Any())
                    {
                        foreach (var giftcard in giftCardAdjustments)
                        {
                            items = items.Append(new DibsOrderItem
                            {
                                Reference = giftcard.GiftCardId.ToString(),
                                Name = giftcard.GiftCardCode, //$"Gift Card - {giftcard.Code}",
                                Quantity = 1,
                                Unit = "pcs",
                                UnitPrice = (int)AmountToMinorUnits(giftcard.Amount),
                                TaxRate = (int)AmountToMinorUnits(order.TaxRate.Value * 100),
                                GrossTotalAmount = (int)AmountToMinorUnits(giftcard.Amount),
                                NetTotalAmount = (int)AmountToMinorUnits(giftcard.Amount)
                            });
                        }
                    }

                    // Custom Amount adjustments
                    var amountAdjustments = order.TransactionAmount.Adjustments.Except(giftCardAdjustments).OfType<AmountAdjustment>();
                    if (amountAdjustments.Any())
                    {
                        foreach (var amount in amountAdjustments)
                        {
                            var reference = Guid.NewGuid().ToString();

                            items = items.Append(new DibsOrderItem
                            {
                                Reference = reference,
                                Name = amount.Name,
                                Quantity = 1,
                                Unit = "pcs",
                                UnitPrice = (int)AmountToMinorUnits(amount.Amount),
                                TaxRate = (int)AmountToMinorUnits(order.TaxRate.Value * 100),
                                GrossTotalAmount = (int)AmountToMinorUnits(amount.Amount),
                                NetTotalAmount = (int)AmountToMinorUnits(amount.Amount)
                            });
                        }
                    }
                }

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
                        CancelUrl = cancelUrl,
                        ReturnUrl = continueUrl,
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
                                EventName = DibsEvents.PaymentCheckoutCompleted,
                                Url = ForceHttps(callbackUrl), // Must be https 
                                Authorization = webhookAuthKey,
                                // Need documentation from Dibs/Nets what headers are for.
                                //Headers = new List<Dictionary<string, string>>
                                //{
                                //    new Dictionary<string, string>(1)
                                //    {
                                //        { "Referrer-Policy", "no-referrer-when-downgrade" }
                                //    }
                                //}
                            },
                            new DibsWebhook
                            {
                                EventName = DibsEvents.PaymentChargeCreated,
                                Url = ForceHttps(callbackUrl),
                                Authorization = webhookAuthKey
                            },
                            new DibsWebhook
                            {
                                EventName = DibsEvents.PaymentRefundCompleted,
                                Url = ForceHttps(callbackUrl),
                                Authorization = webhookAuthKey
                            },
                            new DibsWebhook
                            {
                                EventName = DibsEvents.PaymentCancelCreated,
                                Url = ForceHttps(callbackUrl),
                                Authorization = webhookAuthKey
                            }
                        }
                    }
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
                    { "dibsEasyWebhookAuthKey", webhookAuthKey }
                },
                Form = new PaymentForm(paymentFormLink, FormMethod.Get)
            };
        }

        public override CallbackResult ProcessCallback(OrderReadOnly order, HttpRequestBase request, DibsEasyOneTimeSettings settings)
        {
            try
            {
                // Process callback
                
                var clientConfig = GetDibsEasyClientConfig(settings);
                var client = new DibsEasyClient(clientConfig);

                var dibsEvent = GetDibsWebhookEvent(client, request, order.Properties["dibsEasyWebhookAuthKey"]?.Value);

                if (dibsEvent != null)
                {
                    var paymentId = dibsEvent.Data?.SelectToken("paymentId")?.Value<string>();

                    if (!string.IsNullOrEmpty(paymentId))
                    {
                        var payment = !string.IsNullOrEmpty(paymentId) ? client.GetPayment(paymentId) : null;
                        if (payment != null)
                        {
                            var amount = (long)payment.Payment.OrderDetails.Amount;

                            if (dibsEvent.Event == DibsEvents.PaymentCheckoutCompleted)
                            {
                                //var amount = dibsEvent.Data.SelectToken("amount").SelectToken("paymentId").Value<long>();

                                return CallbackResult.Ok(new TransactionInfo
                                {
                                    TransactionId = paymentId,
                                    AmountAuthorized = AmountFromMinorUnits(amount),
                                    PaymentStatus = GetPaymentStatus(payment)
                                });
                            }

                            if (dibsEvent.Event == DibsEvents.PaymentChargeCreated)
                            {
                                return CallbackResult.Ok(new TransactionInfo
                                {
                                    TransactionId = paymentId,
                                    AmountAuthorized = AmountFromMinorUnits(amount),
                                    PaymentStatus = GetPaymentStatus(payment)
                                });
                            }

                            if (dibsEvent.Event == DibsEvents.PaymentRefundCompleted)
                            {
                                return CallbackResult.Ok(new TransactionInfo
                                {
                                    TransactionId = paymentId,
                                    AmountAuthorized = AmountFromMinorUnits(amount),
                                    PaymentStatus = GetPaymentStatus(payment)
                                });
                            }

                            if (dibsEvent.Event == DibsEvents.PaymentCancelCreated)
                            {
                                return CallbackResult.Ok(new TransactionInfo
                                {
                                    TransactionId = paymentId,
                                    AmountAuthorized = AmountFromMinorUnits(amount),
                                    PaymentStatus = GetPaymentStatus(payment)
                                });
                            }
                        }
                    }
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

        protected DibsWebhookEvent GetDibsWebhookEvent(DibsEasyClient client, HttpRequestBase request, string webhookAuthorization)
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
                            // Verify "Authorization" header returned from webhook
                            VerifyAuthorization(request, webhookAuthorization);

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

        private void VerifyAuthorization(HttpRequestBase request, string webhookAuthorization)
        {
            if (request.Headers["Authorization"] == null)
                throw new Exception("The authorization header is not present in the webhook event.");

            if (request.Headers["Authorization"] != webhookAuthorization)
                throw new Exception("The authorization in the webhook event could not be verified.");
        }

        public static string ForceHttps(string url)
        {
            var uri = new UriBuilder(url);

            var hadDefaultPort = uri.Uri.IsDefaultPort;
            uri.Scheme = Uri.UriSchemeHttps;
            uri.Port = hadDefaultPort ? -1 : uri.Port;

            return uri.ToString();
        }

    }
}
