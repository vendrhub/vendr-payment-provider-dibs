using System;
using System.Globalization;
using System.Linq;
using System.Web;
using Flurl.Http;
using Vendr.Core.Models;
using Vendr.Core.Api;
using Vendr.Core.PaymentProviders;
using Vendr.Extensions;
using Vendr.Common.Logging;
using System.Threading.Tasks;
using System.Net.Http;

namespace Vendr.PaymentProviders.Dibs
{
    [PaymentProvider("dibs-d2", "DIBS D2", "DIBS D2 payment provider for one time payments")]
    public class DibsPaymentProvider : PaymentProviderBase<DibsSettings>
    {
        private readonly ILogger<DibsPaymentProvider> _logger;

        public DibsPaymentProvider(VendrContext vendr,
            ILogger<DibsPaymentProvider> logger)
            : base(vendr)
        {
            _logger = logger;
        }

        public override bool CanCancelPayments => true;
        public override bool CanCapturePayments => true;
        public override bool CanRefundPayments => true;
        public override bool CanFetchPaymentStatus => true;

        public override bool FinalizeAtContinueUrl => false;

        public override string GetCancelUrl(PaymentProviderContext<DibsSettings> ctx)
        {
            ctx.Settings.MustNotBeNull("ctx.Settings");
            ctx.Settings.CancelUrl.MustNotBeNull("ctx.Settings.CancelUrl");

            return ctx.Settings.CancelUrl;
        }

        public override string GetErrorUrl(PaymentProviderContext<DibsSettings> ctx)
        {
            ctx.Settings.MustNotBeNull("ctx.Settings");
            ctx.Settings.ErrorUrl.MustNotBeNull("ctx.Settings.ErrorUrl");

            return ctx.Settings.ErrorUrl;
        }

        public override string GetContinueUrl(PaymentProviderContext<DibsSettings> ctx)
        {
            ctx.Settings.MustNotBeNull("ctx.Settings");
            ctx.Settings.ContinueUrl.MustNotBeNull("ctx.Settings.ContinueUrl");

            return ctx.Settings.ContinueUrl;
        }

        public override Task<PaymentFormResult> GenerateFormAsync(PaymentProviderContext<DibsSettings> ctx)
        {
            var currency = Vendr.Services.CurrencyService.GetCurrency(ctx.Order.CurrencyId);
            var currencyCode = currency.Code.ToUpperInvariant();

            // Ensure currency has valid ISO 4217 code
            if (!Iso4217.CurrencyCodes.ContainsKey(currencyCode)) {
                throw new Exception("Currency must be a valid ISO 4217 currency code: " + currency.Name);
            }

            var strCurrency = Iso4217.CurrencyCodes[currencyCode].ToString(CultureInfo.InvariantCulture);
            var orderAmount = AmountToMinorUnits(ctx.Order.TransactionAmount.Value).ToString("0", CultureInfo.InvariantCulture);

            var payTypes = ctx.Settings.PayTypes?.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                   .Where(x => !string.IsNullOrWhiteSpace(x))
                   .Select(s => s.Trim())
                   .ToArray();

            // MD5(key2 + MD5(key1 + "merchant=<merchant>&ctx.Orderid=<ctx.Orderid> &currency=<cur>&amount=<amount>"))
            var md5Check = $"merchant={ctx.Settings.MerchantId}&ctx.Orderid={ctx.Order.OrderNumber}&currency={strCurrency}&amount={orderAmount}";
            var md5Hash = MD5Hash(ctx.Settings.MD5Key2 + MD5Hash(ctx.Settings.MD5Key1 + md5Check));

            // Parse language - default language is Danish.
            Enum.TryParse(ctx.Settings.Lang, true, out DibsLang lang);

            return Task.FromResult(new PaymentFormResult()
            {
                Form = new PaymentForm("https://payment.architrade.com/paymentweb/start.action", PaymentFormMethod.Post)
                    .WithInput("ctx.Orderid", ctx.Order.OrderNumber)
                    .WithInput("merchant", ctx.Settings.MerchantId)
                    .WithInput("amount", orderAmount)
                    .WithInput("currency", strCurrency)
                    .WithInput("accepturl", ctx.Urls.ContinueUrl)
                    .WithInput("cancelurl", ctx.Urls.CancelUrl)
                    .WithInput("callbackurl", ctx.Urls.CallbackUrl)
                    .WithInput("lang", lang.ToString())
                    .WithInputIf("paytype", payTypes != null && payTypes.Length > 0, string.Join(",", payTypes))
                    .WithInputIf("capturenow", ctx.Settings.Capture, "1")
                    .WithInputIf("calcfee", ctx.Settings.CalcFee, "1")
                    .WithInputIf("decorator", !string.IsNullOrWhiteSpace(ctx.Settings.Decorator), ctx.Settings.Decorator)
                    .WithInputIf("test", ctx.Settings.TestMode, "1")
                    .WithInput("md5key", md5Hash)
            });
        }

        public override async Task<CallbackResult> ProcessCallbackAsync(PaymentProviderContext<DibsSettings> ctx)
        {
            try
            {
                var formData = await ctx.Request.Content.ReadAsFormDataAsync();

                var authkey = formData["authkey"];
                var transaction = formData["transact"];
                var currencyCode = formData["currency"];
                var strAmount = formData["amount"];
                var strFee = formData["fee"] ?? "0"; // Not always in the return data
                var captured = formData["capturenow"] == "1";

                var totalAmount = decimal.Parse(strAmount, CultureInfo.InvariantCulture) + decimal.Parse(strFee, CultureInfo.InvariantCulture);

                var md5Check = $"transact={transaction}&amount={totalAmount.ToString("0", CultureInfo.InvariantCulture)}&currency={currencyCode}";

                // authkey = MD5(key2 + MD5(key1 + "transact=<transact>&amount=<amount>&currency=<currency>"))
                if (MD5Hash(ctx.Settings.MD5Key2 + MD5Hash(ctx.Settings.MD5Key1 + md5Check)) == authkey)
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
                    _logger.Warn($"Dibs [{ctx.Order.OrderNumber}] - MD5Sum security check failed");
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Dibs - ProcessCallback");
            }

            return CallbackResult.Empty;
        }

        public override async Task<ApiResult> FetchPaymentStatusAsync(PaymentProviderContext<DibsSettings> ctx)
        {
            try
            {
                var response = await $"https://@payment.architrade.com/cgi-adm/payinfo.cgi"
                    .WithBasicAuth(ctx.Settings.ApiUsername, ctx.Settings.ApiPassword)
                    .PostUrlEncodedAsync(new
                    {
                        transact = ctx.Order.TransactionInfo.TransactionId
                    })
                    .ReceiveString();

                var responseParams = HttpUtility.ParseQueryString(response);
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
                        TransactionId = ctx.Order.TransactionInfo.TransactionId,
                        PaymentStatus = paymentStatus
                    }
                };

            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Dibs - FetchPaymentStatus");
            }

            return ApiResult.Empty;
        }

        public override async Task<ApiResult> CancelPaymentAsync(PaymentProviderContext<DibsSettings> ctx)
        {
            try
            {
                // MD5(key2 + MD5(key1 + "merchant=<merchant>&ctx.Orderid=<ctx.Orderid>&transact=<transact>")) 
                var md5Check = $"merchant={ctx.Settings.MerchantId}&ctx.Orderid={ctx.Order.OrderNumber}&transact={ctx.Order.TransactionInfo.TransactionId}";
                
                var response = await $"https://payment.architrade.com/cgi-adm/cancel.cgi"
                    .WithBasicAuth(ctx.Settings.ApiUsername, ctx.Settings.ApiPassword)
                    .PostUrlEncodedAsync(new
                    {
                        merchant = ctx.Settings.MerchantId,
                        orderid = ctx.Order.OrderNumber,
                        transact = ctx.Order.TransactionInfo.TransactionId,
                        textreply = "1",
                        md5key = MD5Hash(ctx.Settings.MD5Key2 + MD5Hash(ctx.Settings.MD5Key1 + md5Check))
                    })
                    .ReceiveString();

                var responseParams = HttpUtility.ParseQueryString(response);
                var result = responseParams["result"];

                if (result == "0") // 0 == Accepted
                {
                    return new ApiResult()
                    {
                        TransactionInfo = new TransactionInfoUpdate()
                        {
                            TransactionId = ctx.Order.TransactionInfo.TransactionId,
                            PaymentStatus = PaymentStatus.Cancelled
                        }
                    };
                }
                else
                {
                    _logger.Warn($"Dibs [{ctx.Order.OrderNumber}] - Error making API request - error message: {result}");
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Dibs - CancelPayment");
            }

            return ApiResult.Empty;
        }

        public override async Task<ApiResult> CapturePaymentAsync(PaymentProviderContext<DibsSettings> ctx)
        {
            try
            {
                var strAmount = AmountToMinorUnits(ctx.Order.TransactionInfo.AmountAuthorized.Value).ToString("0", CultureInfo.InvariantCulture);

                // MD5(key2 + MD5(key1 + "merchant=<merchant>&ctx.Orderid=<ctx.Orderid>&transact=<transact>&amount=<amount>")) 
                var md5Check = $"merchant={ctx.Settings.MerchantId}&ctx.Orderid={ctx.Order.OrderNumber}&transact={ctx.Order.TransactionInfo.TransactionId}&amount={strAmount}";

                var response = await $"https://payment.architrade.com/cgi-bin/capture.cgi"
                    .PostUrlEncodedAsync(new
                    {
                        merchant = ctx.Settings.MerchantId,
                        orderid = ctx.Order.OrderNumber,
                        transact = ctx.Order.TransactionInfo.TransactionId,
                        amount = strAmount,
                        textreply = "1",
                        md5key = MD5Hash(ctx.Settings.MD5Key2 + MD5Hash(ctx.Settings.MD5Key1 + md5Check))
                    })
                    .ReceiveString();

                var responseParams = HttpUtility.ParseQueryString(response);
                var result = responseParams["result"];

                if (result == "0") // 0 == Accepted
                {
                    return new ApiResult()
                    {
                        TransactionInfo = new TransactionInfoUpdate()
                        {
                            TransactionId = ctx.Order.TransactionInfo.TransactionId,
                            PaymentStatus = PaymentStatus.Captured
                        }
                    };
                }
                else
                {
                    _logger.Warn($"Dibs [{ctx.Order.OrderNumber}] - Error making API request - error message: {result}");
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Dibs - CapturePayment");
            }

            return ApiResult.Empty;
        }

        public override async Task<ApiResult> RefundPaymentAsync(PaymentProviderContext<DibsSettings> ctx)
        {
            try
            {
                var currency = Vendr.Services.CurrencyService.GetCurrency(ctx.Order.CurrencyId);
                var currencyCode = currency.Code.ToUpperInvariant();

                // Ensure currency has valid ISO 4217 code
                if (!Iso4217.CurrencyCodes.ContainsKey(currencyCode))
                {
                    throw new Exception("Currency must be a valid ISO 4217 currency code: " + currency.Name);
                }

                var strCurrency = Iso4217.CurrencyCodes[currencyCode].ToString(CultureInfo.InvariantCulture);
                var strAmount = AmountToMinorUnits(ctx.Order.TransactionInfo.AmountAuthorized.Value).ToString("0", CultureInfo.InvariantCulture);

                // MD5(key2 + MD5(key1 + "merchant=<merchant>&ctx.Orderid=<ctx.Orderid>&transact=<transact>&amount=<amount>")) 
                var md5Check = $"merchant={ctx.Settings.MerchantId}&ctx.Orderid={ctx.Order.OrderNumber}&transact={ctx.Order.TransactionInfo.TransactionId}&amount={strAmount}";

                var response = await $"https://payment.architrade.com/cgi-adm/refund.cgi"
                    .WithBasicAuth(ctx.Settings.ApiUsername, ctx.Settings.ApiPassword)
                    .PostUrlEncodedAsync(new
                    {
                        merchant = ctx.Settings.MerchantId,
                        orderid = ctx.Order.OrderNumber,
                        transact = ctx.Order.TransactionInfo.TransactionId,
                        amount = strAmount,
                        currency = strCurrency,
                        textreply = "1",
                        md5key = MD5Hash(ctx.Settings.MD5Key2 + MD5Hash(ctx.Settings.MD5Key1 + md5Check))
                    })
                    .ReceiveString();

                var responseParams = HttpUtility.ParseQueryString(response);
                var result = responseParams["result"];

                if (result == "0") // 0 == Accepted
                {
                    return new ApiResult()
                    {
                        TransactionInfo = new TransactionInfoUpdate()
                        {
                            TransactionId = ctx.Order.TransactionInfo.TransactionId,
                            PaymentStatus = PaymentStatus.Refunded
                        }
                    };
                }
                else
                {
                    _logger.Warn($"Dibs [{ctx.Order.OrderNumber}] - Error making API request - error message: {result}");
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Dibs - CapturePayment");
            }

            return ApiResult.Empty;
        }
    }
}
