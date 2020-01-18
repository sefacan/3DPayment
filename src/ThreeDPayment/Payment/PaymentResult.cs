namespace ThreeDPayment.Payment
{
    public class PaymentResult
    {
        public bool Success { get; set; }
        public string ResponseCode { get; set; }
        public string TransactionId { get; set; }
        public string ErrorMessage { get; set; }
        public string ErrorCode { get; set; }
    }
}