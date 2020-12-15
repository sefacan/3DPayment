namespace ThreeDPayment.Results
{
    public class RefundPaymentResult
    {
        public string TransactionId { get; set; }
        public string ReferenceNumber { get; set; }
        public bool Success { get; set; }
        public string Message { get; set; }
        public string ErrorMessage { get; set; }
        public string ErrorCode { get; set; }

        public static RefundPaymentResult Successed(string transactionId, string referenceNumber, string message = null)
        {
            return new RefundPaymentResult
            {
                Success = true,
                TransactionId = transactionId,
                ReferenceNumber = referenceNumber,
                Message = message
            };
        }

        public static RefundPaymentResult Failed(string errorMessage, string errorCode = null)
        {
            return new RefundPaymentResult
            {
                Success = false,
                ErrorMessage = errorMessage,
                ErrorCode = errorCode
            };
        }
    }
}