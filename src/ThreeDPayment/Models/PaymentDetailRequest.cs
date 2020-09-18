using System;
using System.Collections.Generic;

namespace ThreeDPayment.Models
{
    public class PaymentDetailRequest
    {
        public string TransactionId { get; set; }
        public string ReferenceNumber { get; set; }
        public string OrderNumber { get; set; }
        public DateTime PaidDate { get; set; }
        public string CurrencyIsoCode { get; set; }
        public string LanguageIsoCode { get; set; }
        public BankNames BankName { get; set; }

        public Dictionary<string, string> BankParameters { get; set; } = new Dictionary<string, string>();
    }
}
