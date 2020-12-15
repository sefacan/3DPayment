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
        Failed = 30,

        [Display(Name = "İptal")]
        Canceled = 40,

        [Display(Name = "İade")]
        Refunded = 50
    }
}