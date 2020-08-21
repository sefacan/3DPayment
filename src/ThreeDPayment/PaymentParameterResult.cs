using System;
using System.Collections.Generic;

namespace ThreeDPayment
{
    public class PaymentParameterResult
    {
        public IDictionary<string, object> Parameters { get; set; } = new Dictionary<string, object>();
        public Uri PaymentUrl { get; set; }
        public bool Success { get; set; }
        public string Message { get; set; }
        public string ErrorMessage { get; set; }
        public string ErrorCode { get; set; }

        public static PaymentParameterResult Successed(IDictionary<string, object> parameters, string paymentUrl, string message = null)
        {
            return new PaymentParameterResult
            {
                Success = true,
                Parameters = parameters,
                PaymentUrl = new Uri(paymentUrl),
                Message = message
            };
        }

        public static PaymentParameterResult Failed(string errorMessage, string errorCode = null)
        {
            return new PaymentParameterResult
            {
                Success = false,
                ErrorMessage = errorMessage,
                ErrorCode = errorCode
            };
        }
    }
}