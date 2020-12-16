using Microsoft.AspNetCore.Http;
using System.Collections.Generic;
using System.Threading.Tasks;
using ThreeDPayment.Requests;
using ThreeDPayment.Results;

namespace ThreeDPayment
{
    public interface IPaymentProvider
    {
        Task<PaymentGatewayResult> ThreeDGatewayRequest(PaymentGatewayRequest request);
        Task<VerifyGatewayResult> VerifyGateway(VerifyGatewayRequest request, PaymentGatewayRequest gatewayRequest, IFormCollection form);
        Dictionary<string, string> TestParameters { get; }
    }
}