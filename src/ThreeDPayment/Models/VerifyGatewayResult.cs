using System;

namespace ThreeDPayment.Models
{
    public class VerifyGatewayResult
    {
        public bool Success { get; set; }
        public string ResponseCode { get; set; }
        public string TransactionId { get; set; }
        public string ReferenceNumber { get; set; }
        public int Installment { get; set; }
        public Uri CampaignUrl { get; set; }
        public string Message { get; set; }
        public string ErrorMessage { get; set; }
        public string ErrorCode { get; set; }

        public static VerifyGatewayResult Successed(string transactionId, string referenceNumber,
            int installment = 0, string message = null,
            string responseCode = null, string campaignUrl = null)
        {
            return new VerifyGatewayResult
            {
                Success = true,
                TransactionId = transactionId,
                ReferenceNumber = referenceNumber,
                Installment = installment,
                Message = message,
                ResponseCode = responseCode,
                CampaignUrl = new Uri(campaignUrl)
            };
        }

        public static VerifyGatewayResult Failed(string errorMessage, string errorCode = null, string responseCode = null)
        {
            return new VerifyGatewayResult
            {
                Success = false,
                ErrorMessage = errorMessage,
                ErrorCode = errorCode,
                ResponseCode = responseCode
            };
        }
    }
}
