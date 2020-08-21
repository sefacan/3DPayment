using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Xml;

namespace ThreeDPayment
{
    /// <summary>
    /// Vakıfbank sanal pos işlemleri asseco yöntemine göre biraz daha farklı.
    /// Vakıfbank girilen kart bilgisinin 3D doğrulamasını xml isteği ile yapıp sonuç başarılıysa sms sayfasına yönlendirme yapılmasını istiyor.
    /// Kart bilgisi 3D ödeme için uygun olması durumunda yönlendirilecek sayfa bilgisini bize xml içerisinde dönüyor
    /// </summary>
    public class VakifbankPaymentProvider : IPaymentProvider
    {
        private readonly HttpClient client;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public VakifbankPaymentProvider(IHttpClientFactory httpClientFactory,
            IHttpContextAccessor httpContextAccessor)
        {
            client = httpClientFactory.CreateClient();
            _httpContextAccessor = httpContextAccessor;
        }

        public PaymentParameterResult GetPaymentParameters(PaymentRequest request)
        {
            string merchantId = "000100000013506";
            string merchantPassword = "123456";
            string successUrl = "https://localhost:5001/home/callback";//Başarılı Url
            string failUrl = "https://localhost:5001/home/callback";//Hata Url

            try
            {
                var httpParameters = new Dictionary<string, string>();

                //kart numarasından çizgi ve boşlukları kaldırıyoruz
                string cardNumber = request.CardNumber.Replace("-", string.Empty);
                cardNumber = cardNumber.Replace(" ", string.Empty).Trim();

                httpParameters.Add("Pan", cardNumber);
                httpParameters.Add("ExpiryDate", $"{request.ExpireYear}{request.ExpireMonth}");
                httpParameters.Add("PurchaseAmount", request.TotalAmount.ToString(new CultureInfo("en-US")));
                httpParameters.Add("Currency", request.CurrencyIsoCode);//TL 949 | EURO 978 | Dolar 840

                /*
                 * Visa 100
                 * Master Card 200
                 * American Express 300
                */
                httpParameters.Add("BrandName", "100");
                httpParameters.Add("VerifyEnrollmentRequestId", request.OrderNumber);//sipariş numarası
                httpParameters.Add("SessionInfo", "1");//banka dökümanları sabit bir değer
                httpParameters.Add("MerchantID", merchantId);
                httpParameters.Add("MerchantPassword", merchantPassword);
                httpParameters.Add("SuccessUrl", successUrl);
                httpParameters.Add("FailureUrl", failUrl);
                if (request.Installment > 1)
                    httpParameters.Add("InstallmentCount", request.Installment.ToString());

                //Canlı https://3dsecure.vakifbank.com.tr:4443/MPIAPI/MPI_Enrollment.aspx
                var enrollmentUrl = new Uri("https://3dsecuretest.vakifbank.com.tr:4443/MPIAPI/MPI_Enrollment.aspx");
                var response = client.PostAsync(enrollmentUrl, new FormUrlEncodedContent(httpParameters)).GetAwaiter().GetResult();
                string responseContent = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();

                var xmlDocument = new XmlDocument();
                xmlDocument.LoadXml(responseContent);
                var statusNode = xmlDocument.SelectSingleNode("IPaySecure/Message/VERes/Status");
                if (statusNode.InnerText != "Y")
                {
                    var messageErrorNode = xmlDocument.SelectSingleNode("IPaySecure/ErrorMessage");
                    var messageErrorCodeNode = xmlDocument.SelectSingleNode("IPaySecure/MessageErrorCode");

                    return PaymentParameterResult.Failed(messageErrorNode.InnerText, messageErrorCodeNode?.InnerText);
                }

                var pareqNode = xmlDocument.SelectSingleNode("IPaySecure/Message/VERes/PaReq");
                var termUrlNode = xmlDocument.SelectSingleNode("IPaySecure/Message/VERes/TermUrl");
                var mdNode = xmlDocument.SelectSingleNode("IPaySecure/Message/VERes/MD");
                var acsUrlNode = xmlDocument.SelectSingleNode("IPaySecure/Message/VERes/ACSUrl");

                var parameters = new Dictionary<string, object>();
                parameters.Add("PaReq", pareqNode.InnerText);
                parameters.Add("TermUrl", termUrlNode.InnerText);
                parameters.Add("MD", mdNode.InnerText);

                //form post edilecek url xml response içerisinde bankadan dönüyor
                return PaymentParameterResult.Successed(parameters, acsUrlNode.InnerText);
            }
            catch (Exception ex)
            {
                return PaymentParameterResult.Failed(ex.ToString());
            }
        }

        public PaymentResult GetPaymentResult(IFormCollection form)
        {
            var paymentResult = new PaymentResult();
            if (form == null)
            {
                paymentResult.ErrorMessage = "Form verisi alınamadı.";
                return paymentResult;
            }

            var status = form["Status"];
            if (StringValues.IsNullOrEmpty(status))
            {
                paymentResult.ErrorMessage = "İşlem sonuç bilgisi alınamadı.";
                return paymentResult;
            }

            if (status != "Y")
            {
                if (ErrorCodes.ContainsKey(form["ErrorCode"]))
                    paymentResult.ErrorMessage = ErrorCodes[form["ErrorCode"]];
                else
                    paymentResult.ErrorMessage = "3D doğrulama başarısız";

                paymentResult.ErrorCode = form["ErrorCode"];
                return paymentResult;
            }

            string requestXml = "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
                                                        "<VposRequest>" +
                                                        "<MerchantId>" + form["MerchantId"] + "</MerchantId>" +
                                                        "<Password>" + form["get724_merchant_password"] + "</Password>" +
                                                        "<TerminalNo>" + form["get724_user_name"] + "</TerminalNo>" +
                                                        "<Pan>" + form["Pan"] + "</Pan>" +
                                                        "<Expiry>" + form["expire_date"] + "</Expiry>" +
                                                        "<CurrencyAmount>" + form["amount"] + "</CurrencyAmount>" +
                                                        "<CurrencyCode>" + form["PurchCurrency"] + "</CurrencyCode>" +
                                                        "<TransactionType>Sale</TransactionType>" +
                                                        "<TransactionId></TransactionId>";

            //boş veya 0 olan taksit bilgisini gönderme
            if (int.TryParse(form["InstallmentCount"], out int installment) && installment > 0)
                requestXml += "<NumberOfInstallments>" + installment + "</NumberOfInstallments>";

            requestXml += "<CardHoldersName>" + form["card_holders_name"] + "</CardHoldersName>" +
            "<Cvv>" + form["cv2"] + "</Cvv>" +
            "<ECI>" + form["Eci"] + "</ECI>" +
            "<CAVV>" + form["CAVV"] + "</CAVV>" +
            "<MpiTransactionId>" + form["VerifyEnrollmentRequestId"] + "</MpiTransactionId>" +
            "<OrderId>" + form["VerifyEnrollmentRequestId"] + "</OrderId>" +
            "<ClientIp>" + _httpContextAccessor.HttpContext.Connection.RemoteIpAddress + "</ClientIp>" +
            "<TransactionDeviceSource>0</TransactionDeviceSource>" +
            "</VposRequest>";

            //Canlı https://onlineodeme.vakifbank.com.tr:4443/VposService/v3/Vposreq.aspx
            string requestUrl = "https://onlineodemetest.vakifbank.com.tr:4443/VposService/v3/Vposreq.aspx";//TEST

            var parameters = new Dictionary<string, string>();
            parameters.Add("prmstr", requestXml);
            var response = client.PostAsync(requestUrl, new FormUrlEncodedContent(parameters)).GetAwaiter().GetResult();
            string responseContent = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();

            var xmlDocument = new XmlDocument();
            xmlDocument.LoadXml(responseContent);
            var resultCodeNode = xmlDocument.SelectSingleNode("VposResponse/ResultCode");
            var resultDetailNode = xmlDocument.SelectSingleNode("VposResponse/ResultDetail");
            if (resultCodeNode.InnerText != "0000")
            {
                paymentResult.ErrorMessage = resultDetailNode.InnerText;
                paymentResult.ErrorCode = resultCodeNode.InnerText;
                return paymentResult;
            }

            paymentResult.Success = true;
            paymentResult.ResponseCode = resultCodeNode.InnerText;
            paymentResult.TransactionId = form["Xid"];
            paymentResult.ErrorMessage = resultDetailNode.InnerText;
            paymentResult.ErrorCode = resultCodeNode.InnerText;

            return paymentResult;
        }

        private static readonly IDictionary<string, string> ErrorCodes = new Dictionary<string, string>
        {
            { "0000", "Başarılı" },
            { "0001", "BANKANIZI ARAYIN" },
            { "0002", "BANKANIZI ARAYIN" },
            { "0003", "ÜYE KODU HATALI/TANIMSIZ" },
            { "0004", "KARTA EL KOYUNUZ" },
            { "0005", "İŞLEM ONAYLANMADI." },
            { "0006", "HATALI İŞLEM" },
            { "0007", "KARTA EL KOYUNUZ" },
            { "0009", "TEKRAR DENEYİNİZ" },
            { "0010", "TEKRAR DENEYİNİZ" },
            { "0011", "TEKRAR DENEYİNİZ" },
            { "0012", "Geçersiz İşlem" },
            { "0013", "Geçersiz İşlem Tutarı" },
            { "0014", "Geçersiz Kart Numarası" },
            { "0015", "MÜŞTERİ YOK/BIN HATALI" },
            { "0021", "İŞLEM ONAYLANMADI" },
            { "0030", "MESAJ FORMATI HATALI (ÜYE İŞYERİ)" },
            { "0032", "DOSYASINA ULAŞILAMADI" },
            { "0033", "SÜRESİ BİTMİŞ/İPTAL KART" },
            { "0034", "SAHTE KART" },
            { "0036", "İŞLEM ONAYLANMADI" },
            { "0038", "ŞİFRE AŞIMI/KARTA EL KOY" },
            { "0041", "KAYIP KART- KARTA EL KOY" },
            { "0043", "ÇALINTI KART-KARTA EL KOY" },
            { "0051", "LIMIT YETERSIZ" },
            { "0052", "HESAP NOYU KONTROL EDİN" },
            { "0053", "HESAP YOK" },
            { "0054", "GEÇERSİZ KART" },
            { "0055", "Hatalı Kart Şifresi" },
            { "0056", "Kart Tanımlı Değil." },
            { "0057", "KARTIN İŞLEM İZNİ YOK" },
            { "0058", "POS İŞLEM TİPİNE KAPALI" },
            { "0059", "SAHTEKARLIK ŞÜPHESİ" },
            { "0061", "Para çekme tutar limiti aşıldı" },
            { "0062", "YASAKLANMIŞ KART" },
            { "0063", "Güvenlik ihlali" },
            { "0065", "GÜNLÜK İŞLEM ADEDİ LİMİTİ AŞILDI" },
            { "0075", "Şifre Deneme Sayısı Aşıldı" },
            { "0077", "ŞİFRE SCRIPT TALEBİ REDDEDİLDİ" },
            { "0078", "ŞİFRE GÜVENİLİR BULUNMADI" },
            { "0089", "İŞLEM ONAYLANMADI" },
            { "0091", "KARTI VEREN BANKA HİZMET DIŞI" },
            { "0092", "BANKASI BİLİNMİYOR" },
            { "0093", "İŞLEM ONAYLANMADI" },
            { "0096", "BANKASININ SİSTEMİ ARIZALI" },
            { "0312", "GEÇERSİZ KART" },
            { "0315", "TEKRAR DENEYİNİZ" },
            { "0320", "ÖNPROVİZYON KAPATILAMADI" },
            { "0323", "ÖNPROVİZYON KAPATILAMADI" },
            { "0357", "İŞLEM ONAYLANMADI" },
            { "0358", "Kart Kapalı" },
            { "0381", "RED KARTA EL KOY" },
            { "0382", "SAHTE KART-KARTA EL KOYUNUZ" },
            { "0501", "GEÇERSİZ TAKSİT/İŞLEM TUTARI" },
            { "0503", "KART NUMARASI HATALI" },
            { "0504", "İŞLEM ONAYLANMADI" },
            { "0540", "İade Edilecek İşlemin Orijinali Bulunamadı" },
            { "0541", "Orj. İşlemin tamamı iade edildi" },
            { "0542", "İADE İŞLEMİ GERÇEKLEŞTİRİLEMEZ" },
            { "0550", "İŞLEM YKB POS UNDAN YAPILMALI" },
            { "0570", "YURTDIŞI KART İŞLEM İZNİ YOK" },
            { "0571", "İşyeri Amex İşlem İzni Yok" },
            { "0572", "İşyeri Amex Tanımları Eksik" },
            { "0574", "ÜYE İŞYERİ İŞLEM İZNİ YOK" },
            { "0575", "İŞLEM ONAYLANMADI" },
            { "0577", "TAKSİTLİ İŞLEM İZNİ YOK" },
            { "0580", "HATALI 3D GÜVENLİK BİLGİSİ" },
            { "0581", "ECI veya CAVV bilgisi eksik" },
            { "0582", "HATALI 3D GÜVENLİK BİLGİSİ" },
            { "0583", "TEKRAR DENEYİNİZ" },
            { "0880", "İŞLEM ONAYLANMADI" },
            { "0961", "İŞLEM TİPİ GEÇERSİZ" },
            { "0962", "TerminalID Tanımısız" },
            { "0963", "Üye İşyeri Tanımlı Değil" },
            { "0966", "İŞLEM ONAYLANMADI" },
            { "0971", "Eşleşmiş bir işlem iptal edilemez" },
            { "0972", "Para Kodu Geçersiz" },
            { "0973", "İŞLEM ONAYLANMADI" },
            { "0974", "İŞLEM ONAYLANMADI" },
            { "0975", "ÜYE İŞYERİ İŞLEM İZNİ YOK" },
            { "0976", "İŞLEM ONAYLANMADI" },
            { "0978", "KARTIN TAKSİTLİ İŞLEME İZNİ YOK" },
            { "0980", "İŞLEM ONAYLANMADI" },
            { "0981", "EKSİK GÜVENLİK BİLGİSİ" },
            { "0982", "İŞLEM İPTAL DURUMDA. İADE EDİLEMEZ" },
            { "0983", "İade edilemez,iptal" },
            { "0984", "İADE TUTAR HATASI" },
            { "0985", "İŞLEM ONAYLANMADI." },
            { "0986", "GIB Taksit Hata" },
            { "0987", "İŞLEM ONAYLANMADI." },
            { "8484", "Birden fazla hata olması durumunda geri dönülür. ResultDetail alanından detayları alınabilir." },
            { "1001", "Sistem hatası." },
            { "1006", "Bu TransactionId ile daha önce başarılı bir işlem gerçekleştirilmiş" },
            { "1007", "Referans transaction alınamadı" },
            { "1046", "İade işleminde tutar hatalı." },
            { "1047", "İşlem tutarı geçersizdir." },
            { "1049", "Geçersiz tutar." },
            { "1050", "CVV hatalı." },
            { "1051", "Kredi kartı numarası hatalıdır." },
            { "1052", "Kredi kartı son kullanma tarihi hatalı." },
            { "1054", "İşlem numarası hatalıdır." },
            { "1059", "Yeniden iade denemesi." },
            { "1060", "Hatalı taksit sayısı." },
            { "2011", "TerminalNo Bulunamadı." },
            { "2200", "İş yerinin işlem için gerekli hakkı yok." },
            { "2202", "İşlem iptal edilemez. ( Batch Kapalı )" },
            { "5001", "İş yeri şifresi yanlış." },
            { "5002", "İş yeri aktif değil." },
            { "1073", "Terminal üzerinde aktif olarak bir batch bulunamadı" },
            { "1074", "İşlem henüz sonlanmamış yada referans işlem henüz tamamlanmamış." },
            { "1075", "Sadakat puan tutarı hatalı" },
            { "1076", "Sadakat puan kodu hatalı" },
            { "1077", "Para kodu hatalı" },
            { "1078", "Geçersiz sipariş numarası" },
            { "1079", "Geçersiz sipariş açıklaması" },
            { "1080", "Sadakat tutarı ve para tutarı gönderilmemiş." },
            { "1061", "Aynı sipariş numarasıyla (OrderId) daha önceden başarılı işlem yapılmış" },
            { "1065", "Ön provizyon daha önceden kapatılmış" },
            { "1082", "Geçersiz işlem tipi" },
            { "1083", "Referans işlem daha önceden iptal edilmiş." },
            { "1084", "Geçersiz poaş kart numarası" },
            { "7777", "Banka tarafında gün sonu yapıldığından işlem gerçekleştirilemedi" },
            { "1087", "Yabancı para birimiyle taksitli provizyon kapama işlemi yapılamaz" },
            { "1088", "Önprovizyon iptal edilmiş" },
            { "1089", "Referans işlem yapılmak istenen işlem için uygun değil" },
            { "1091", "Recurring işlemin toplam taksit sayısı hatalı" },
            { "1092", "Recurring işlemin tekrarlama aralığı hatalı" },
            { "1093", "Sadece Satış (Sale) işlemi recurring olarak işaretlenebilir" },
            { "1095", "Lütfen geçerli bir email adresi giriniz" },
            { "1096", "Lütfen geçerli bir IP adresi giriniz" },
            { "1097", "Lütfen geçerli bir CAVV değeri giriniz" },
            { "1098", "Lütfen geçerli bir ECI değeri giriniz." },
            { "1099", "Lütfen geçerli bir Kart Sahibi ismi giriniz." },
            { "1100", "Lütfen geçerli bir brand girişi yapın." },
            { "1105", "Üye işyeri IP si sistemde tanımlı değil" },
            { "1102", "Recurring işlem aralık tipi hatalı bir değere sahip" },
            { "1101", "Referans transaction reverse edilmiş." },
            { "1104", "İlgili taksit için tanım yok" },
            { "1111", "Bu üye işyeri Non Secure işlem yapamaz" },
            { "1122", "SurchargeAmount değeri 0 dan büyük olmalıdır." },
            { "6000", "Talep Mesajı okunamadı." },
            { "6001", "İstek HttpPost Yöntemi ile yapılmalıdır." },
            { "6003", "POX Request Adresine istek yapıyorsunuz. Mesaj Boş Geldi. İstek Xml Mesajını prmstr parametresi ile iletiniz." },
            { "9117", "3DSecure Islemlerde ECI degeri bos olamaz." },
            { "33",   "Kartın 3D Secure şifre doğrulaması yapılamadı" },
            { "400",  "3D Şifre doğrulaması yapılamadı." },
            { "1026", "FailureUrl format hatası" },
            { "2000", "Acquirer info is empty" },
            { "2005", "Merchant cannot be found for this bank" },
            { "2006", "Merchant acquirer bin password required" },
            { "2009", "Brand not found" },
            { "2010", "CardHolder info is empty" },
            { "2011", "Pan is empty" },
            { "2012", "DeviceCategory must be between 0 and 2" },
            { "2013", "Threed secure message can not be found" },
            { "2014", "Pares message id does not match threed secure message id" },
            { "2015", "Signature verification false" },
            { "2017", "AcquireBin Can not be found" },
            { "2018", "Merchant acquirer bin password wrong" },
            { "2019", "Bank not found" },
            { "2020", "Bank Id does not match merchant bank" },
            { "2021", "Invalid Currency Code" },
            { "2022", "Verify EnrollmentRequest Id cannot be empty" },
            { "2023", "Verify Enrollment Request Id Already exist for this merchant" },
            { "2024", "Acs certificate cannot be found in database" },
            { "2025", "Certificate could not be found in certificate store" },
            { "2026", "Brand certificate not found in store" },
            { "2027", "Invalid xml file" },
            { "2028", "Threed Secure Message is Invalid State" },
            { "2029", "Invalid Pan" },
            { "2030", "Invalid Expire Date" },
            { "1001", "Fail Url Format is Invalid" },
            { "1002", "SuccessUrl format is invalid." },
            { "1003", "BrandId format is invalid" },
            { "1004", "DeviceCategory format is invalid" },
            { "1005", "SessionInfo format is invalid" },
            { "1006", "Xid format is invalid" },
            { "1007", "Currency format is invalid" },
            { "1008", "PurchaseAmount format is invalid" },
            { "1009", "Expire Date format is invalid" },
            { "1010", "Pan format is invalid" },
            { "1011", "Merchant acquirer bin password format is invalid" },
            { "1012", "HostMerchant format is invalid" },
            { "1013", "BankId format is invalid" },
            { "2031", "Verification failed: No Signature was found in the document" },
            { "2032", "Verification failed: More that one signature was found for the document" },
            { "2033", "Actual Brand Can not be Found" },
            { "2034", "Invalid Amount" },
            { "1014", "Is Recurring Format is Invalid" },
            { "1015", "Recurring Frequency Format Is Invalid" },
            { "1016", "Recurring End Date Format Is Invalid" },
            { "2035", "Invalid Recurring Information" },
            { "2036", "Invalid Recurring Frequency" },
            { "2037", "Invalid Reccuring End Date" },
            { "2038", "Recurring End Date Must Be Greater Than Expire Date" },
            { "2039", "Invalid x509 certificate Data" },
            { "2040", "Invalid Installment" },
            { "1017", "Installment count format is invalid" },
            { "3000", "Bank not found" },
            { "3001", "Country not found" },
            { "3002", "Invalid FailUrl" },
            { "3003", "HostMerchantNumber cannot  be empty" },
            { "3004", "MerchantBrandAcquirerBin cannot be empty" },
            { "3005", "MerchantName cannot be empty" },
            { "3006", "MerchantPassword cannot be empty" },
            { "3007", "Invalid SucessUrl" },
            { "3008", "Invalid MerchantSiteUrl" },
            { "3009", "Invalid AcquirerBin length" },
            { "3010", "Brand cannot be null" },
            { "3011", "Invalid AcquirerBinPassword length" },
            { "3012", "Invalid HostMerchantNumber length" },
            { "2041", "Pares Exponent Value Does Not Match Pareq Exponent" },
            { "2042", "Pares Acquirer Bin Value Does Not Match Pareq Acqiurer Bin" },
            { "2043", "Pares Merchant Id Does Not Match Pareq Merchant Id" },
            { "2044", "Pares Xid Does Not Match Pareq Xid" },
            { "2045", "Pares Purchase Amount Does Not Match Pareq Purchase Amount" },
            { "2046", "Pares Currency Does Not Match Pareq Currency" },
            { "2047", "VeRes Xsd Validation Error" },
            { "2048", "PaRes Xsd Validation Exception" },
            { "2049", "Invalid Request" },
            { "2050", "File Is Empty" },
            { "2051", "Custom Error" },
            { "2052", "Bank Brand Bin Already Exist" },
            { "3013", "End Date Must Be Greater Than Start" },
            { "3014", "Start Date Must Be Greater Than DateTime MinVal" },
            { "3015", "End Date Must Be Greater Than DateTime MinVal" },
            { "3016", "Invalid Search Period" },
            { "3017", "Bin Cannot Be Empty" },
            { "3018", "Card Type Cannot Be Empty" },
            { "3019", "Bank Brand Bin Not Found" },
            { "3020", "Bin Length Must Be Six" },
            { "2053", "Directory Server Communication Error" },
            { "2054", "ACS Hata Bildirdi" },
            { "5037", "SuccessUrl alanı hatalıdır." }
        };
    }
}
