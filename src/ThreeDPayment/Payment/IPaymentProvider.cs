using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;

namespace ThreeDPayment.Payment
{
    public interface IPaymentProvider
    {
        IDictionary<string, object> GetPaymentParameters(PaymentRequest request);
        PaymentResult GetPaymentResult(IFormCollection form);
        Uri PaymentUrl { get; }
    }
}