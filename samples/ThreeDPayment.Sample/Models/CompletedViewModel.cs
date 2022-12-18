/*
   Support: fsefacan@gmail.com
*/

using System;

namespace ThreeDPayment.Sample.Models
{
    public class CompletedViewModel
    {
        public Guid OrderNumber { get; set; }
        public string TransactionNumber { get; set; }
        public string ReferenceNumber { get; set; }
        public int BankId { get; set; }
        public string BankName { get; set; }
        public string CardHolderName { get; set; }
        public int Installment { get; set; }
        public decimal TotalAmount { get; set; }
        public DateTime? PaidDate { get; set; }
        public DateTime CreateDate { get; set; }
    }
}