using Microsoft.AspNetCore.Http;

namespace ThreeDPayment.Payment
{
    public interface IPaymentProvider
    {
        PaymentParameterResult GetPaymentParameters(PaymentRequest request);
        PaymentResult GetPaymentResult(IFormCollection form);
    }
}