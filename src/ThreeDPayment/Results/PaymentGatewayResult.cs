using System;
using System.Collections.Generic;

namespace ThreeDPayment.Results
{
    public class PaymentGatewayResult
    {
        public Uri GatewayUrl { get; set; }
        public bool Success { get; set; }
        public string Message { get; set; }
        public string ErrorMessage { get; set; }
        public string ErrorCode { get; set; }
        public IDictionary<string, object> Parameters { get; set; } = new Dictionary<string, object>();
        public string HtmlFormContent { get; set; }
        public bool HtmlContent => !string.IsNullOrEmpty(HtmlFormContent);

        public static PaymentGatewayResult Successed(string htmlFormContent,
            string message = null)
        {
            return new PaymentGatewayResult
            {
                Success = true,
                HtmlFormContent = htmlFormContent,
                Message = message
            };
        }

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