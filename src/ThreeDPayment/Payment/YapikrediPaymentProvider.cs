using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ThreeDPayment.Payment
{
    public class YapikrediPaymentProvider : IPaymentProvider
    {
        public PaymentParameterResult GetPaymentParameters(PaymentRequest request)
        {
            throw new NotImplementedException();
        }

        public PaymentResult GetPaymentResult(IFormCollection form)
        {
            throw new NotImplementedException();
        }
    }
}