using Microsoft.AspNetCore.Http;

namespace ThreeDPayment
{
    public interface IPaymentProvider
    {
        PaymentParameterResult GetPaymentParameters(PaymentRequest request);
        PaymentResult GetPaymentResult(IFormCollection form);
    }
}