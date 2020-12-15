using System;
using System.Diagnostics.CodeAnalysis;

namespace ThreeDPayment.Sample.Domains
{
    public class CreditCardPrefix : BaseEntity, IEquatable<CreditCardPrefix>
    {
        public int CreditCardId { get; set; }
        public string Prefix { get; set; }
        public bool Active { get; set; }
        public bool Deleted { get; set; }
        public DateTime CreateDate { get; set; }
        public DateTime UpdateDate { get; set; }

        public CreditCard CreditCard { get; set; }

        public bool Equals([AllowNull] CreditCardPrefix other)
        {
            if (other == null)
                return false;

            return other.Prefix == Prefix;
        }
    }
}