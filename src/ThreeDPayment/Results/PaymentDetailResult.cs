namespace ThreeDPayment.Results
{
    public class PaymentDetailResult
    {
        public string TransactionId { get; set; }
        public string ReferenceNumber { get; set; }
        public string CardPrefix { get; set; }
        public int Installment { get; set; }
        public int ExtraInstallment { get; set; }
        public string BankMessage { get; set; }
        public string ResponseCode { get; set; }
        public string ErrorMessage { get; set; }
        public string ErrorCode { get; set; }

        public bool Paid { get; set; }
        public bool Refunded { get; set; }
        public bool Canceled { get; set; }
        public bool Failed { get; set; }

        public static PaymentDetailResult PaidResult(string transactionId, string referenceNumber,
            string cardPrefix = null, int installment = 0,
            int extraInstallment = 0,
            string bankMessage = null, string responseCode = null,
            string errorMessage = null, string errorCode = null)
        {
            return new PaymentDetailResult
            {
                Paid = true,
                TransactionId = transactionId,
                ReferenceNumber = referenceNumber,
                CardPrefix = cardPrefix,
                Installment = installment,
                ExtraInstallment = extraInstallment,
                BankMessage = bankMessage,
                ResponseCode = responseCode,
                ErrorMessage = errorMessage,
                ErrorCode = errorCode
            };
        }

        public static PaymentDetailResult RefundedResult(string transactionId, string referenceNumber,
            string bankMessage = null, string responseCode = null,
            string errorMessage = null, string errorCode = null)
        {
            return new PaymentDetailResult
            {
                Refunded = true,
                TransactionId = transactionId,
                ReferenceNumber = referenceNumber,
                BankMessage = bankMessage,
                ResponseCode = responseCode,
                ErrorMessage = errorMessage,
                ErrorCode = errorCode
            };
        }

        public static PaymentDetailResult CanceledResult(string transactionId, string referenceNumber,
            string bankMessage = null, string responseCode = null,
            string errorMessage = null, string errorCode = null)
        {
            return new PaymentDetailResult
            {
                Canceled = true,
                TransactionId = transactionId,
                ReferenceNumber = referenceNumber,
                BankMessage = bankMessage,
                ResponseCode = responseCode,
                ErrorMessage = errorMessage,
                ErrorCode = errorCode
            };
        }

        public static PaymentDetailResult FailedResult(string bankMessage = null, string responseCode = null,
            string errorMessage = null, string errorCode = null)
        {
            return new PaymentDetailResult
            {
                Failed = false,
                BankMessage = bankMessage,
                ResponseCode = responseCode,
                ErrorMessage = errorMessage,
                ErrorCode = errorCode
            };
        }
    }
}