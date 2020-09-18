using System;
using System.Collections.Generic;
using System.Net;

namespace ThreeDPayment.Models
{
    public class PaymentGatewayRequest
    {
        public string CardHolderName { get; set; }
        public string CardNumber { get; set; }
        public int ExpireMonth { get; set; }
        public int ExpireYear { get; set; }
        public string CvvCode { get; set; }
        public string CartType { get; set; }
        public int Installment { get; set; }
        public decimal TotalAmount { get; set; }
        public string OrderNumber { get; set; }
        public string CurrencyIsoCode { get; set; }
        public string LanguageIsoCode { get; set; }
        public IPAddress CustomerIpAddress { get; set; }
        public Uri CallbackUrl { get; set; }
        public BankNames BankName { get; set; }

        public Dictionary<string, string> BankParameters { get; set; } = new Dictionary<string, string>();
    }
}