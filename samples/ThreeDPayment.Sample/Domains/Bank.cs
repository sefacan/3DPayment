using System;
using System.Collections.Generic;

namespace ThreeDPayment.Sample.Domains
{
    public class Bank : BaseEntity
    {
        public string Name { get; set; }
        public string SystemName { get; set; }
        public int BankCode { get; set; }
        public string LogoPath { get; set; }
        public bool UseCommonPaymentPage { get; set; }
        public bool DefaultBank { get; set; }
        public bool Active { get; set; }
        public DateTime CreateDate { get; set; }
        public DateTime UpdateDate { get; set; }

        public List<CreditCardInstallment> Installments { get; set; } = new List<CreditCardInstallment>();
        public List<CreditCard> CreditCards { get; set; } = new List<CreditCard>();
        public List<BankParameter> Parameters { get; set; } = new List<BankParameter>();
    }
}