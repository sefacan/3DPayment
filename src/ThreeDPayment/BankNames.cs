using System.ComponentModel.DataAnnotations;

namespace ThreeDPayment
{
    public enum BankNames
    {
        //NestPay
        [Display(Name = "Akbank")]
        AkBank = 46,

        [Display(Name = "İş Bankası")]
        IsBankasi = 64,

        [Display(Name = "Halkbank")]
        HalkBank = 12,

        [Display(Name = "Ziraat Bankası")]
        ZiraatBankasi = 10,

        [Display(Name = "Türk Ekonomi Bankası(TEB)")]
        TurkEkonomiBankasi = 32,

        [Display(Name = "ING Bank")]
        IngBank = 99,

        [Display(Name = "Türkiye Finans")]
        TurkiyeFinans = 206,

        [Display(Name = "Anadolubank")]
        AnadoluBank = 135,

        [Display(Name = "HSBC")]
        HSBC = 123,

        [Display(Name = "Şekerbank")]
        SekerBank = 59,

        //InterVPOS
        [Display(Name = "Denizbank")]
        DenizBank = 134,

        //PayFor
        [Display(Name = "QNB Finansbank")]
        FinansBank = 111,

        //GVP
        [Display(Name = "Garanti Bankası")]
        Garanti = 62,

        //KuveytTurk
        [Display(Name = "Kuveyt Türk")]
        KuveytTurk = 205,

        //GET 7/24
        [Display(Name = "Vakıfbank")]
        VakifBank = 15,

        //Posnet
        [Display(Name = "Yapıkredi Bankası")]
        Yapikredi = 67,
        [Display(Name = "Albaraka Türk")]
        Albaraka = 203
    }
}