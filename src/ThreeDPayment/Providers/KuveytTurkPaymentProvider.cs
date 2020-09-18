using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using ThreeDPayment.Models;

namespace ThreeDPayment.Providers
{
    public class KuveytTurkPaymentProvider : IPaymentProvider
    {
        private readonly HttpClient client;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public KuveytTurkPaymentProvider(IHttpClientFactory httpClientFactory,
            IHttpContextAccessor httpContextAccessor)
        {
            client = httpClientFactory.CreateClient();
            _httpContextAccessor = httpContextAccessor;
        }

        public Task<PaymentGatewayResult> ThreeDGatewayRequest(PaymentGatewayRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<VerifyGatewayResult> VerifyGateway(VerifyGatewayRequest request, IFormCollection form)
        {
            throw new NotImplementedException();
        }

        public Task<CancelPaymentResult> CancelRequest(CancelPaymentRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<RefundPaymentResult> RefundRequest(RefundPaymentRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<PaymentDetailResult> PaymentDetailRequest(PaymentDetailRequest request)
        {
            throw new NotImplementedException();
        }

        public Dictionary<string, string> TestParameters => new Dictionary<string, string>
        {
        };

        private static readonly IDictionary<string, string> ErrorCodes = new Dictionary<string, string>
        {
        };
    }
}