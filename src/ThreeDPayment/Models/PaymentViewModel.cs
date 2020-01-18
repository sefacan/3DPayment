using Microsoft.AspNetCore.Mvc.Rendering;
using System.Collections.Generic;
using ThreeDPayment.Payment;

namespace ThreeDPayment.Models
{
    public class PaymentViewModel
    {
        public string CardHolderName { get; set; }
        public string CardNumber { get; set; }
        public int ExpireMonth { get; set; }
        public int ExpireYear { get; set; }
        public string CvvCode { get; set; }
        public int Installment { get; set; }
        public Banks SelectedBank { get; set; }
        public IList<SelectListItem> Banks { get; set; }
    }
}