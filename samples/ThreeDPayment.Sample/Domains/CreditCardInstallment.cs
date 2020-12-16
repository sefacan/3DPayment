using System;

namespace ThreeDPayment.Sample.Domains
{
    public class CreditCardInstallment : BaseEntity
    {
        public int CreditCardId { get; set; }
        public int Installment { get; set; }
        public decimal InstallmentRate { get; set; }
        public bool Active { get; set; }
        public bool Deleted { get; set; }
        public DateTime CreateDate { get; set; }
        public DateTime UpdateDate { get; set; }

        public CreditCard CreditCard { get; set; }
    }
}