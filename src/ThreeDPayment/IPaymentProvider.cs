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
        Task<CancelPaymentResult> CancelRequest(CancelPaymentRequest request);
        Task<RefundPaymentResult> RefundRequest(RefundPaymentRequest request);
        Task<PaymentDetailResult> PaymentDetailRequest(PaymentDetailRequest request);
        Dictionary<string, string> TestParameters { get; }
    }
}