using System;
using System.Collections.Generic;

namespace ThreeDPayment.Sample.Domains
{
    public class CreditCard : BaseEntity
    {
        public int BankId { get; set; }
        public string Name { get; set; }
        public bool Active { get; set; }
        public bool ManufacturerCard { get; set; }
        public bool CampaignCard { get; set; }
        public bool Deleted { get; set; }
        public DateTime CreateDate { get; set; }
        public DateTime UpdateDate { get; set; }

        public Bank Bank { get; set; }
        public List<CreditCardPrefix> Prefixes { get; set; } = new List<CreditCardPrefix>();
        public List<CreditCardInstallment> Installments { get; set; } = new List<CreditCardInstallment>();
    }
}