namespace ThreeDPayment.Payment
{
    public class PaymentRequest
    {
        public string CardHolderName { get; set; }
        public string CardNumber { get; set; }
        public int ExpireMonth { get; set; }
        public int ExpireYear { get; set; }
        public string CvvCode { get; set; }
        public int Installment { get; set; }
        public decimal TotalAmount { get; set; }
        public string OrderNumber { get; set; }
        public string CurrencyIsoCode { get; set; }
        public string LanguageIsoCode { get; set; }
        public string CustomerIpAddress { get; set; }
    }
}