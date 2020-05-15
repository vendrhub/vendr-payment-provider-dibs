using Flurl.Http;
using Flurl.Http.Configuration;
using Newtonsoft.Json;
using System;
using System.Threading.Tasks;
using Vendr.Contrib.PaymentProviders.Dibs.Easy.Api.Models;
using Vendr.PaymentProviders.Dibs.Easy.Api.Models;

namespace Vendr.Contrib.PaymentProviders.Reepay.Api
{
    public class DibsEasyClient
    {
        private DibsEasyClientConfig _config;

        public DibsEasyClient(DibsEasyClientConfig config)
        {
            _config = config;
        }

        public DibsPaymentResult CreatePayment(DibsPaymentRequest data)
        {
            return Request("/v1/payments/", (req) => req
                .WithHeader("Content-Type", "application/json")
                .PostJsonAsync(data)
                .ReceiveJson<DibsPaymentResult>());
        }

        public DibsPaymentDetails GetPaymentDetails(string paymentId)
        {
            return Request($"/v1/payments/{paymentId}", (req) => req
                .GetJsonAsync<DibsPaymentDetails>());
        }

        public string CancelPayment(string paymentId)
        {
            return Request($"/v1/payments/{paymentId}/cancels", (req) => req
                .WithHeader("Content-Type", "application/json")
                .PostAsync(null)
                .ReceiveJson<string>());
        }

        public string ChargePayment(string paymentId, object data)
        {
            return Request($"/v1/payments/{paymentId}/charges", (req) => req
                .WithHeader("Content-Type", "application/json")
                .PostJsonAsync(data)
                .ReceiveJson<string>());
        }

        public string RefundPayment(string chargeId, object data)
        {
            return Request($"/v1/charges/{chargeId}/refunds", (req) => req
                .WithHeader("Content-Type", "application/json")
                .PostJsonAsync(data)
                .ReceiveJson<string>());
        }

        private TResult Request<TResult>(string url, Func<IFlurlRequest, Task<TResult>> func)
        {
            var result = default(TResult);

            try
            {
                var req = new FlurlRequest(_config.BaseUrl + url)
                        .ConfigureRequest(x =>
                        {
                            var jsonSettings = new JsonSerializerSettings
                            {
                                NullValueHandling = NullValueHandling.Ignore,
                                DefaultValueHandling = DefaultValueHandling.Include,
                                MissingMemberHandling = MissingMemberHandling.Ignore
                            };
                            x.JsonSerializer = new NewtonsoftJsonSerializer(jsonSettings);
                        })
                        .WithHeader("Authorization", _config.Authorization);

                result = func.Invoke(req).Result;
            }
            catch (FlurlHttpException ex)
            {
                throw;
            }

            return result;
        }
    }
}
