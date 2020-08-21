namespace ThreeDPayment
{
    public class PaymentResult
    {
        public bool Success { get; set; }
        public string ResponseCode { get; set; }
        public string TransactionId { get; set; }
        public string ErrorMessage { get; set; }
        public string ErrorCode { get; set; }

        public static PaymentResult Successed(string transactionId, string responseCode = null)
        {
            return new PaymentResult
            {
                Success = true,
                TransactionId = transactionId,
                ResponseCode = responseCode
            };
        }

        public static PaymentResult Failed(string errorMessage, string errorCode, string responseCode = null)
        {
            return new PaymentResult
            {
                Success = false,
                ErrorMessage = errorMessage,
                ErrorCode = errorCode,
                ResponseCode = responseCode
            };
        }
    }
}