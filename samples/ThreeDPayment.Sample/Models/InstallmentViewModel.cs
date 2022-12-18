/*
   Support: fsefacan@gmail.com
*/

using System.Collections.Generic;

namespace ThreeDPayment.Sample.Models
{
    public class InstallmentViewModel
    {
        public string Prefix { get; set; }
        public decimal TotalAmount { get; set; }
        public int UserId { get; set; }
        public int? BankId { get; set; }
        public string BankName { get; set; }
        public string BankLogo { get; set; }
        public List<InstallmentRate> InstallmentRates { get; set; } = new List<InstallmentRate>();

        public void AddCashRate(decimal totalAmount)
        {
            InstallmentRates.Add(new InstallmentViewModel.InstallmentRate
            {
                Text = "Peşin",
                Installment = 1,
                Amount = totalAmount.ToString("N2"),
                AmountValue = totalAmount,
                TotalAmount = totalAmount.ToString("N2"),
                TotalAmountValue = totalAmount
            });
        }

        public class InstallmentRate
        {
            public string Text { get; set; }
            public int Installment { get; set; }
            public decimal Rate { get; set; }

            public string Amount { get; set; }
            public decimal AmountValue { get; set; }

            public string TotalAmount { get; set; }
            public decimal TotalAmountValue { get; set; }
        }
    }
}
