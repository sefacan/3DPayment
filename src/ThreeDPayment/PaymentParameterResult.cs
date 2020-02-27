using System;
using System.Collections.Generic;

namespace ThreeDPayment
{
    public class PaymentParameterResult
    {
        public PaymentParameterResult()
        {
            Parameters = new Dictionary<string, object>();
        }

        public IDictionary<string, object> Parameters { get; set; }
        public Uri PaymentUrl { get; set; }
        public bool Success { get; set; }
        public string Message { get; set; }
        public string ErrorMessage { get; set; }
    }
}