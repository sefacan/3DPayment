using System;
using System.Diagnostics.CodeAnalysis;

namespace ThreeDPayment.Sample.Domains
{
    public class CreditCardInstallment : BaseEntity, IEquatable<CreditCardInstallment>
    {
        public int CreditCardId { get; set; }
        public int Installment { get; set; }
        public decimal InstallmentRate { get; set; }
        public bool Active { get; set; }
        public bool Deleted { get; set; }
        public DateTime CreateDate { get; set; }
        public DateTime UpdateDate { get; set; }

        public CreditCard CreditCard { get; set; }

        public bool Equals([AllowNull] CreditCardInstallment other)
        {
            if (other == null)
                return false;

            return other.Installment == Installment;
        }
    }
}