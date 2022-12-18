/*
   Support: fsefacan@gmail.com
*/

using System.ComponentModel.DataAnnotations;

namespace ThreeDPayment.Sample.Domains
{
    public enum PaymentStatus
    {
        [Display(Name = "Beklemede")]
        Pending = 10,

        [Display(Name = "Ödendi")]
        Paid = 20,

        [Display(Name = "Hatalı")]
        Failed = 30
    }
}