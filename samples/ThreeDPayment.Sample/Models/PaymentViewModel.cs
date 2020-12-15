namespace ThreeDPayment.Sample.Models
{
    public class PaymentViewModel
    {
        public string CardHolderName { get; set; }
        public string CardNumber { get; set; }
        public int ExpireMonth { get; set; }
        public int ExpireYear { get; set; }
        public string CvvCode { get; set; }
        public string CardType { get; set; }
        public int Installment { get; set; }
        public bool ManufacturerCard { get; set; }
        public decimal TotalAmount { get; set; }
        public int? BankId { get; set; }
    }
}