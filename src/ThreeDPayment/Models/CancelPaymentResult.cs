namespace ThreeDPayment.Models
{
    public class CancelPaymentResult
    {
        public string TransactionId { get; set; }
        public bool Success { get; set; }
        public string Message { get; set; }
        public string ErrorMessage { get; set; }
        public string ErrorCode { get; set; }

        public static CancelPaymentResult Successed(string transactionId, string message = null)
        {
            return new CancelPaymentResult
            {
                Success = true,
                Message = message
            };
        }

        public static CancelPaymentResult Failed(string errorMessage, string errorCode = null)
        {
            return new CancelPaymentResult
            {
                Success = false,
                ErrorMessage = errorMessage,
                ErrorCode = errorCode
            };
        }
    }
}