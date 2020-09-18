using System;
using System.Collections.Generic;

namespace ThreeDPayment.Models
{
    public class PaymentGatewayResult
    {
        public IDictionary<string, object> Parameters { get; set; } = new Dictionary<string, object>();
        public Uri GatewayUrl { get; set; }
        public bool Success { get; set; }
        public string Message { get; set; }
        public string ErrorMessage { get; set; }
        public string ErrorCode { get; set; }

        public static PaymentGatewayResult Successed(IDictionary<string, object> parameters,
            string gatewayUrl,
            string message = null)
        {
            return new PaymentGatewayResult
            {
                Success = true,
                Parameters = parameters,
                GatewayUrl = new Uri(gatewayUrl),
                Message = message
            };
        }

        public static PaymentGatewayResult Failed(string errorMessage, string errorCode = null)
        {
            return new PaymentGatewayResult
            {
                Success = false,
                ErrorMessage = errorMessage,
                ErrorCode = errorCode
            };
        }
    }
}