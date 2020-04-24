using System;
using System.Globalization;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using Flurl.Http;
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
        public override bool CanRefundPayments => true;
        public override bool CanFetchPaymentStatus => true;

        public override bool FinalizeAtContinueUrl => false;

        public override string GetCancelUrl(OrderReadOnly order, DibsSettings settings)
        {
            settings.MustNotBeNull("settings");
            settings.CancelUrl.MustNotBeNull("settings.CancelUrl");

            return settings.CancelUrl;
        }

        public override string GetErrorUrl(OrderReadOnly order, DibsSettings settings)
        {
            settings.MustNotBeNull("settings");
            settings.ErrorUrl.MustNotBeNull("settings.ErrorUrl");

            return settings.ErrorUrl;
        }

        public override string GetContinueUrl(OrderReadOnly order, DibsSettings settings)
        {
            settings.MustNotBeNull("settings");
            settings.ContinueUrl.MustNotBeNull("settings.ContinueUrl");

            return settings.ContinueUrl;
        }

        public override PaymentFormResult GenerateForm(OrderReadOnly order, string continueUrl, string cancelUrl, string callbackUrl, DibsSettings settings)
        {
            var currency = Vendr.Services.CurrencyService.GetCurrency(order.CurrencyId);
            var currencyCode = currency.Code.ToUpperInvariant();

            // Ensure currency has valid ISO 4217 code
            if (!Iso4217.CurrencyCodes.ContainsKey(currencyCode)) {
                throw new Exception("Currency must be a valid ISO 4217 currency code: " + currency.Name);
            }

            var strCurrency = Iso4217.CurrencyCodes[currencyCode].ToString(CultureInfo.InvariantCulture);
            var orderAmount = AmountToMinorUnits(order.TotalPrice.Value.WithTax).ToString("0", CultureInfo.InvariantCulture);

            var payTypes = settings.PayTypes?.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                   .Where(x => !string.IsNullOrWhiteSpace(x))
                   .Select(s => s.Trim())
                   .ToArray();

            // MD5(key2 + MD5(key1 + "merchant=<merchant>&orderid=<orderid> &currency=<cur>&amount=<amount>"))
            var md5Check = $"merchant={settings.MerchantId}&orderid={order.OrderNumber}&currency={strCurrency}&amount={orderAmount}";
            var md5Hash = MD5Hash(settings.MD5Key2 + MD5Hash(settings.MD5Key1 + md5Check));

            // Parse language - default language is Danish.
            Enum.TryParse(settings.Lang, true, out DibsLang lang);

            return new PaymentFormResult()
            {
                Form = new PaymentForm("https://payment.architrade.com/paymentweb/start.action", FormMethod.Post)
                    .WithInput("orderid", order.OrderNumber)
                    .WithInput("merchant", settings.MerchantId)
                    .WithInput("amount", orderAmount)
                    .WithInput("currency", strCurrency)
                    .WithInput("accepturl", continueUrl)
                    .WithInput("cancelurl", cancelUrl)
                    .WithInput("callbackurl", callbackUrl)
                    .WithInput("lang", lang.ToString())
                    .WithInputIf("paytype", payTypes != null && payTypes.Length > 0, string.Join(",", payTypes))
                    .WithInputIf("capturenow", settings.Capture, "1")
                    .WithInputIf("calcfee", settings.CalcFee, "1")
                    .WithInputIf("decorator", !string.IsNullOrWhiteSpace(settings.Decorator), settings.Decorator)
                    .WithInputIf("test", settings.TestMode, "1")
                    .WithInput("md5key", md5Hash)
            };
        }

        public override CallbackResult ProcessCallback(OrderReadOnly order, HttpRequestBase request, DibsSettings settings)
        {
            try
            {
                var authkey = request.Form["authkey"];
                var transaction = request.Form["transact"];
                var currencyCode = request.Form["currency"];
                var strAmount = request.Form["amount"];
                var strFee = request.Form["fee"] ?? "0"; // Not always in the return data
                var captured = request.Form["capturenow"] == "1";

                var totalAmount = decimal.Parse(strAmount, CultureInfo.InvariantCulture) + decimal.Parse(strFee, CultureInfo.InvariantCulture);

                var md5Check = $"transact={transaction}&amount={totalAmount.ToString("0", CultureInfo.InvariantCulture)}&currency={currencyCode}";

                // authkey = MD5(key2 + MD5(key1 + "transact=<transact>&amount=<amount>&currency=<currency>"))
                if (MD5Hash(settings.MD5Key2 + MD5Hash(settings.MD5Key1 + md5Check)) == authkey)
                {
                    return new CallbackResult
                    {
                        TransactionInfo = new TransactionInfo
                        {
                            AmountAuthorized = AmountFromMinorUnits((long)totalAmount),
                            TransactionId = transaction,
                            PaymentStatus = !captured ? PaymentStatus.Authorized : PaymentStatus.Captured
                        }
                    };
                }
                else
                {
                    Vendr.Log.Warn<DibsPaymentProvider>($"Dibs [{order.OrderNumber}] - MD5Sum security check failed");
                }
            }
            catch (Exception ex)
            {
                Vendr.Log.Error<DibsPaymentProvider>(ex, "Dibs - ProcessCallback");
            }

            return CallbackResult.Empty;
        }

        public override ApiResult FetchPaymentStatus(OrderReadOnly order, DibsSettings settings)
        {
            try
            {
                var response = $"https://@payment.architrade.com/cgi-adm/payinfo.cgi"
                    .WithBasicAuth(settings.ApiUsername, settings.ApiPassword)
                    .PostUrlEncodedAsync(new
                    {
                        transact = order.TransactionInfo.TransactionId
                    })
                    .ReceiveString();

                var responseParams = HttpUtility.ParseQueryString(response.Result);
                var status = responseParams["status"];

                var paymentStatus = PaymentStatus.Initialized;

                switch (status)
                {
                    case "2":
                        paymentStatus = PaymentStatus.Authorized;
                        break;
                    case "5":
                        paymentStatus = PaymentStatus.Captured;
                        break;
                    case "6":
                        paymentStatus = PaymentStatus.Cancelled;
                        break;
                    case "11":
                        paymentStatus = PaymentStatus.Refunded;
                        break;
                }

                return new ApiResult()
                {
                    TransactionInfo = new TransactionInfoUpdate()
                    {
                        TransactionId = order.TransactionInfo.TransactionId,
                        PaymentStatus = paymentStatus
                    }
                };

            }
            catch (Exception ex)
            {
                Vendr.Log.Error<DibsPaymentProvider>(ex, "Dibs - FetchPaymentStatus");
            }

            return ApiResult.Empty;
        }

        public override ApiResult CancelPayment(OrderReadOnly order, DibsSettings settings)
        {
            try
            {
                // MD5(key2 + MD5(key1 + "merchant=<merchant>&orderid=<orderid>&transact=<transact>")) 
                var md5Check = $"merchant={settings.MerchantId}&orderid={order.OrderNumber}&transact={order.TransactionInfo.TransactionId}";
                
                var response = $"https://payment.architrade.com/cgi-adm/cancel.cgi"
                    .WithBasicAuth(settings.ApiUsername, settings.ApiPassword)
                    .PostUrlEncodedAsync(new
                    {
                        merchant = settings.MerchantId,
                        orderid = order.OrderNumber,
                        transact = order.TransactionInfo.TransactionId,
                        textreply = "1",
                        md5key = MD5Hash(settings.MD5Key2 + MD5Hash(settings.MD5Key1 + md5Check))
                    })
                    .ReceiveString();

                var responseParams = HttpUtility.ParseQueryString(response.Result);
                var result = responseParams["result"];

                if (result == "0") // 0 == Accepted
                {
                    return new ApiResult()
                    {
                        TransactionInfo = new TransactionInfoUpdate()
                        {
                            TransactionId = order.TransactionInfo.TransactionId,
                            PaymentStatus = PaymentStatus.Cancelled
                        }
                    };
                }
                else
                {
                    Vendr.Log.Warn<DibsPaymentProvider>($"Dibs [{order.OrderNumber}] - Error making API request - error message: {result}");
                }
            }
            catch (Exception ex)
            {
                Vendr.Log.Error<DibsPaymentProvider>(ex, "Dibs - CancelPayment");
            }

            return ApiResult.Empty;
        }

        public override ApiResult CapturePayment(OrderReadOnly order, DibsSettings settings)
        {
            try
            {
                var strAmount = AmountToMinorUnits(order.TransactionInfo.AmountAuthorized.Value).ToString("0", CultureInfo.InvariantCulture);

                // MD5(key2 + MD5(key1 + "merchant=<merchant>&orderid=<orderid>&transact=<transact>&amount=<amount>")) 
                var md5Check = $"merchant={settings.MerchantId}&orderid={order.OrderNumber}&transact={order.TransactionInfo.TransactionId}&amount={strAmount}";

                var response = $"https://payment.architrade.com/cgi-bin/capture.cgi"
                    .PostUrlEncodedAsync(new
                    {
                        merchant = settings.MerchantId,
                        orderid = order.OrderNumber,
                        transact = order.TransactionInfo.TransactionId,
                        amount = strAmount,
                        textreply = "1",
                        md5key = MD5Hash(settings.MD5Key2 + MD5Hash(settings.MD5Key1 + md5Check))
                    })
                    .ReceiveString();

                var responseParams = HttpUtility.ParseQueryString(response.Result);
                var result = responseParams["result"];

                if (result == "0") // 0 == Accepted
                {
                    return new ApiResult()
                    {
                        TransactionInfo = new TransactionInfoUpdate()
                        {
                            TransactionId = order.TransactionInfo.TransactionId,
                            PaymentStatus = PaymentStatus.Captured
                        }
                    };
                }
                else
                {
                    Vendr.Log.Warn<DibsPaymentProvider>($"Dibs [{order.OrderNumber}] - Error making API request - error message: {result}");
                }
            }
            catch (Exception ex)
            {
                Vendr.Log.Error<DibsPaymentProvider>(ex, "Dibs - CapturePayment");
            }

            return ApiResult.Empty;
        }

        public override ApiResult RefundPayment(OrderReadOnly order, DibsSettings settings)
        {
            try
            {
                var currency = Vendr.Services.CurrencyService.GetCurrency(order.CurrencyId);
                var currencyCode = currency.Code.ToUpperInvariant();

                // Ensure currency has valid ISO 4217 code
                if (!Iso4217.CurrencyCodes.ContainsKey(currencyCode))
                {
                    throw new Exception("Currency must be a valid ISO 4217 currency code: " + currency.Name);
                }

                var strCurrency = Iso4217.CurrencyCodes[currencyCode].ToString(CultureInfo.InvariantCulture);
                var strAmount = AmountToMinorUnits(order.TransactionInfo.AmountAuthorized.Value).ToString("0", CultureInfo.InvariantCulture);

                // MD5(key2 + MD5(key1 + "merchant=<merchant>&orderid=<orderid>&transact=<transact>&amount=<amount>")) 
                var md5Check = $"merchant={settings.MerchantId}&orderid={order.OrderNumber}&transact={order.TransactionInfo.TransactionId}&amount={strAmount}";

                var response = $"https://payment.architrade.com/cgi-adm/refund.cgi"
                    .WithBasicAuth(settings.ApiUsername, settings.ApiPassword)
                    .PostUrlEncodedAsync(new
                    {
                        merchant = settings.MerchantId,
                        orderid = order.OrderNumber,
                        transact = order.TransactionInfo.TransactionId,
                        amount = strAmount,
                        currency = strCurrency,
                        textreply = "1",
                        md5key = MD5Hash(settings.MD5Key2 + MD5Hash(settings.MD5Key1 + md5Check))
                    })
                    .ReceiveString();

                var responseParams = HttpUtility.ParseQueryString(response.Result);
                var result = responseParams["result"];

                if (result == "0") // 0 == Accepted
                {
                    return new ApiResult()
                    {
                        TransactionInfo = new TransactionInfoUpdate()
                        {
                            TransactionId = order.TransactionInfo.TransactionId,
                            PaymentStatus = PaymentStatus.Refunded
                        }
                    };
                }
                else
                {
                    Vendr.Log.Warn<DibsPaymentProvider>($"Dibs [{order.OrderNumber}] - Error making API request - error message: {result}");
                }
            }
            catch (Exception ex)
            {
                Vendr.Log.Error<DibsPaymentProvider>(ex, "Dibs - CapturePayment");
            }

            return ApiResult.Empty;
        }
    }
}
