using System;

namespace ThreeDPayment.Sample.Domains
{
    public class PaymentTransaction : BaseEntity
    {
        public Guid OrderNumber { get; set; }
        public string TransactionNumber { get; set; }
        public string ReferenceNumber { get; set; }
        public string UserIpAddress { get; set; }
        public string UserAgent { get; set; }
        public int BankId { get; set; }
        public string CardPrefix { get; set; }
        public string CardHolderName { get; set; }
        public int Installment { get; set; }
        public int ExtraInstallment { get; set; }
        public decimal TotalAmount { get; set; }
        public string BankErrorMessage { get; set; }
        public DateTime? PaidDate { get; set; }
        public DateTime CreateDate { get; set; }
        public int StatusId { get; set; }
        public bool Deleted { get; set; }
        public string BankRequest { get; set; }
        public string BankResponse { get; set; }

        public Bank Bank { get; set; }

        public PaymentStatus Status
        {
            get => (PaymentStatus)StatusId;
            set => StatusId = (int)value;
        }

        public void MarkAsCreated()
        {
            CreateDate = DateTime.Now;
            Status = PaymentStatus.Pending;
        }

        public void MarkAsPaid(DateTime paidDate)
        {
            PaidDate = paidDate;
            Status = PaymentStatus.Paid;
            BankErrorMessage = null;
        }

        public void MarkAsFailed(string bankErrorMessage, string bankResponse)
        {
            Status = PaymentStatus.Failed;
            BankErrorMessage = bankErrorMessage;
            BankResponse = bankResponse;
        }
    }
}