using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using ThreeDPayment.Models;

namespace ThreeDPayment.Providers
{
    public class NestPayPaymentProvider : IPaymentProvider
    {
        private readonly HttpClient client;

        public NestPayPaymentProvider(IHttpClientFactory httpClientFactory)
        {
            client = httpClientFactory.CreateClient();
        }

        public Task<PaymentGatewayResult> ThreeDGatewayRequest(PaymentGatewayRequest request)
        {
            try
            {
                string clientId = request.BankParameters["clientId"];
                string processType = request.BankParameters["processType"];
                string storeKey = request.BankParameters["storeKey"];
                string storeType = request.BankParameters["storeType"];
                string totalAmount = request.TotalAmount.ToString(new CultureInfo("en-US"));
                string random = DateTime.Now.ToString();

                var parameters = new Dictionary<string, object>();
                parameters.Add("clientid", clientId);
                parameters.Add("amount", totalAmount);//kuruş ayrımı nokta olmalı!!!
                parameters.Add("oid", request.OrderNumber);//sipariş numarası

                //işlem başarılı da olsa başarısız da olsa callback sayfasına yönlendirerek kendi tarafımızda işlem sonucunu kontrol ediyoruz
                parameters.Add("okUrl", request.CallbackUrl);//başarılı dönüş adresi
                parameters.Add("failUrl", request.CallbackUrl);//hatalı dönüş adresi
                parameters.Add("islemtipi", processType);//direk satış
                parameters.Add("taksit", request.Installment);//taksit sayısı | 1 veya boş tek çekim olur
                parameters.Add("rnd", random);//rastgele bir sayı üretilmesi isteniyor

                string hashstr = $"{clientId}{request.OrderNumber}{totalAmount}{request.CallbackUrl}{request.CallbackUrl}{processType}{request.Installment}{random}{storeKey}";
                var cryptoServiceProvider = new SHA1CryptoServiceProvider();
                var inputbytes = cryptoServiceProvider.ComputeHash(Encoding.UTF8.GetBytes(hashstr));
                var hashData = Convert.ToBase64String(inputbytes);

                parameters.Add("hash", hashData);//hash data
                parameters.Add("currency", request.CurrencyIsoCode);//ISO code TL 949 | EURO 978 | Dolar 840
                parameters.Add("pan", request.CardNumber);
                parameters.Add("cardHolderName", request.CardHolderName);
                parameters.Add("Ecom_Payment_Card_ExpDate_Month", request.ExpireMonth);//kart bitiş ay'ı
                parameters.Add("Ecom_Payment_Card_ExpDate_Year", request.ExpireYear);//kart bitiş yıl'ı
                parameters.Add("cv2", request.CvvCode);//kart güvenlik kodu
                parameters.Add("cardType", "1");//kart tipi visa 1 | master 2 | amex 3
                parameters.Add("storetype", storeType);
                parameters.Add("lang", request.LanguageIsoCode);//iki haneli dil iso kodu

                return Task.FromResult(PaymentGatewayResult.Successed(parameters, request.BankParameters["gatewayUrl"]));
            }
            catch (Exception ex)
            {
                return Task.FromResult(PaymentGatewayResult.Failed(ex.ToString()));
            }
        }

        public Task<VerifyGatewayResult> VerifyGateway(VerifyGatewayRequest request, IFormCollection form)
        {
            if (form == null)
            {
                return Task.FromResult(VerifyGatewayResult.Failed("Form verisi alınamadı."));
            }

            var mdStatus = form["mdStatus"];
            if (StringValues.IsNullOrEmpty(mdStatus))
            {
                return Task.FromResult(VerifyGatewayResult.Failed(form["mdErrorMsg"], form["ProcReturnCode"]));
            }

            var response = form["Response"];
            //mdstatus 1,2,3 veya 4 olursa 3D doğrulama geçildi anlamına geliyor
            if (!mdStatus.Equals("1") || !mdStatus.Equals("2") || !mdStatus.Equals("3") || !mdStatus.Equals("4"))
            {
                return Task.FromResult(VerifyGatewayResult.Failed($"{response} - {form["mdErrorMsg"]}", form["ProcReturnCode"]));
            }

            if (StringValues.IsNullOrEmpty(response) || !response.Equals("Approved"))
            {
                return Task.FromResult(VerifyGatewayResult.Failed($"{response} - {form["ErrMsg"]}", form["ProcReturnCode"]));
            }

            return Task.FromResult(VerifyGatewayResult.Successed(form["TransId"], form["TransId"],
                int.Parse(form["taksit"]), response,
                form["ProcReturnCode"]));
        }

        public async Task<CancelPaymentResult> CancelRequest(CancelPaymentRequest request)
        {
            string clientId = request.BankParameters["clientId"];
            string userName = request.BankParameters["userName"];
            string password = request.BankParameters["password"];

            string requestXml = $@"<?xml version=""1.0"" encoding=""utf-8""?>
                                    <CC5Request>
                                      <Name>{userName}</Name>
                                      <Password>{password}</Password>
                                      <ClientId>{clientId}</ClientId>
                                      <Type>Void</Type>
                                      <OrderId>{request.OrderNumber}</OrderId>
                                    </CC5Request>";

            var response = await client.PostAsync(request.BankParameters["verifyUrl"], new StringContent(requestXml, Encoding.UTF8, "text/xml"));
            string responseContent = await response.Content.ReadAsStringAsync();

            var xmlDocument = new XmlDocument();
            xmlDocument.LoadXml(responseContent);

            if (xmlDocument.SelectSingleNode("CC5Response/Response") == null ||
                xmlDocument.SelectSingleNode("CC5Response/Response").InnerText != "Approved")
            {
                var errorMessage = xmlDocument.SelectSingleNode("CC5Response/ErrMsg")?.InnerText ?? string.Empty;
                if (string.IsNullOrEmpty(errorMessage))
                    errorMessage = "Bankadan hata mesajı alınamadı.";

                return CancelPaymentResult.Failed(errorMessage);
            }

            if (xmlDocument.SelectSingleNode("CC5Response/ProcReturnCode") == null ||
                xmlDocument.SelectSingleNode("CC5Response/ProcReturnCode").InnerText == "00")
            {
                var errorMessage = xmlDocument.SelectSingleNode("CC5Response/ErrMsg")?.InnerText ?? string.Empty;
                if (string.IsNullOrEmpty(errorMessage))
                    errorMessage = "Bankadan hata mesajı alınamadı.";

                return CancelPaymentResult.Failed(errorMessage);
            }

            var transactionId = xmlDocument.SelectSingleNode("CC5Response/TransId")?.InnerText ?? string.Empty;
            return CancelPaymentResult.Successed(transactionId);
        }

        public async Task<RefundPaymentResult> RefundRequest(RefundPaymentRequest request)
        {
            string clientId = request.BankParameters["clientId"];
            string userName = request.BankParameters["userName"];
            string password = request.BankParameters["password"];

            string requestXml = $@"<?xml version=""1.0"" encoding=""utf-8""?>
                                    <CC5Request>
                                      <Name>{userName}</Name>
                                      <Password>{password}</Password>
                                      <ClientId>{clientId}</ClientId>
                                      <Type>Credit</Type>
                                      <OrderId>{request.OrderNumber}</OrderId>
                                    </CC5Request>";

            var response = await client.PostAsync(request.BankParameters["verifyUrl"], new StringContent(requestXml, Encoding.UTF8, "text/xml"));
            string responseContent = await response.Content.ReadAsStringAsync();

            var xmlDocument = new XmlDocument();
            xmlDocument.LoadXml(responseContent);

            if (xmlDocument.SelectSingleNode("CC5Response/Response") == null ||
                xmlDocument.SelectSingleNode("CC5Response/Response").InnerText != "Approved")
            {
                var errorMessage = xmlDocument.SelectSingleNode("CC5Response/ErrMsg")?.InnerText ?? string.Empty;
                if (string.IsNullOrEmpty(errorMessage))
                    errorMessage = "Bankadan hata mesajı alınamadı.";

                return RefundPaymentResult.Failed(errorMessage);
            }

            if (xmlDocument.SelectSingleNode("CC5Response/ProcReturnCode") == null ||
                xmlDocument.SelectSingleNode("CC5Response/ProcReturnCode").InnerText == "00")
            {
                var errorMessage = xmlDocument.SelectSingleNode("CC5Response/ErrMsg")?.InnerText ?? string.Empty;
                if (string.IsNullOrEmpty(errorMessage))
                    errorMessage = "Bankadan hata mesajı alınamadı.";

                return RefundPaymentResult.Failed(errorMessage);
            }

            var transactionId = xmlDocument.SelectSingleNode("CC5Response/TransId")?.InnerText ?? string.Empty;
            return RefundPaymentResult.Successed(transactionId);
        }

        public async Task<PaymentDetailResult> PaymentDetailRequest(PaymentDetailRequest request)
        {
            string clientId = request.BankParameters["clientId"];
            string userName = request.BankParameters["userName"];
            string password = request.BankParameters["password"];

            string requestXml = $@"<?xml version=""1.0"" encoding=""utf-8""?>
                                    <CC5Request>
                                        <Name>{userName}</Name>
                                        <Password>{password}</Password>
                                        <ClientId>{clientId}</ClientId>
                                        <OrderId>{request.OrderNumber}</OrderId>
                                        <Extra>
                                            <ORDERDETAIL>QUERY</ORDERDETAIL>
                                        </Extra>
                                    </CC5Request>";

            var response = await client.PostAsync(request.BankParameters["verifyUrl"], new StringContent(requestXml, Encoding.UTF8, "text/xml"));
            string responseContent = await response.Content.ReadAsStringAsync();

            var xmlDocument = new XmlDocument();
            xmlDocument.LoadXml(responseContent);

            string finalStatus = xmlDocument.SelectSingleNode("CC5Response/Extra/ORDER_FINAL_STATUS")?.InnerText ?? string.Empty;
            string transactionId = xmlDocument.SelectSingleNode("CC5Response/Extra/TRANS_ID")?.InnerText;
            string referenceNumber = xmlDocument.SelectSingleNode("CC5Response/Extra/TRX_1_TRAN_UID")?.InnerText;
            string cardPrefix = xmlDocument.SelectSingleNode("CC5Response/Extra/TRX_1_PAN")?.InnerText;
            string installment = xmlDocument.SelectSingleNode("CC5Response/Extra/TRX_1_INSTALMENT")?.InnerText ?? "0";
            string bankMessage = xmlDocument.SelectSingleNode("CC5Response/Extra/TRX_1_ACQRESPDETAIL")?.InnerText;
            string responseCode = xmlDocument.SelectSingleNode("CC5Response/Extra/TRX_1_MDSTATUS")?.InnerText;

            if (finalStatus.Equals("SALE", StringComparison.OrdinalIgnoreCase))
            {
                return PaymentDetailResult.PaidResult(transactionId, referenceNumber, cardPrefix, int.Parse(installment), bankMessage, responseCode);
            }
            else if (finalStatus.Equals("VOID", StringComparison.OrdinalIgnoreCase))
            {
                return PaymentDetailResult.CanceledResult(transactionId, referenceNumber, bankMessage, responseCode);
            }
            else if (finalStatus.Equals("REFUND", StringComparison.OrdinalIgnoreCase))
            {
                return PaymentDetailResult.RefundedResult(transactionId, referenceNumber, bankMessage, responseCode);
            }

            var errorMessage = xmlDocument.SelectSingleNode("CC5Response/ErrMsg")?.InnerText ?? string.Empty;
            if (string.IsNullOrEmpty(errorMessage))
                errorMessage = "Bankadan hata mesajı alınamadı.";

            return PaymentDetailResult.FailedResult(errorMessage: errorMessage);
        }

        public Dictionary<string, string> TestParameters => new Dictionary<string, string>
        {
            { "clientId", "700655000200" },
            { "processType", "Auth" },
            { "storeKey", "TRPS0200" },
            { "storeType", "3D_PAY" },
            { "gatewayUrl", "https://entegrasyon.asseco-see.com.tr/fim/est3Dgate" },
            { "userName", "ISBANKAPI" },
            { "password", "ISBANK07" },
            { "verifyUrl", "https://entegrasyon.asseco-see.com.tr/fim/api" }
        };

        private static readonly IDictionary<string, string> ErrorCodes = new Dictionary<string, string>
        {
            { "0", "Basarili" },
            { "1", "Manual onay icin bankayi arayiniz" },
            { "2", "Sahte onay, Bankanizla teyit ediniz." },
            { "3", "Gecersiz isyeri ya da servis saglayici" },
            { "4", "Calinti Kart" },
            { "5", "Genel Red" },
            { "6", "Hata (sadece dosya guncelleme donus degerlerinde)" },
            { "7", "Calinti Kart, Ozel durum." },
            { "8", "Sahte Onay, fakat VPOS sisteminde kullanilmamalidir, bankanizla teyit ediniz." },
            { "11", "Sahte Onay(VIP), fakat VPOS sisteminde kullanilmamalidir, bankanizla teyit ediniz." },
            { "12", "Gecersiz Transaction." },
            { "13", "Gecersiz Tutar" },
            { "14", "Gecersiz hesap numarasi" },
            { "15", "Boyle bir issuer yok" },
            { "19", "Tekrar girin, tekrar deneyin" },
            { "21", "Transaction geri alinamiyor" },
            { "25", "Kayit dosyasi bulunamiyor" },
            { "28", "Orjinal reddedildi" },
            { "29", "Orjinal bulunamadi" },
            { "30", "Format hatasi (switch uretti)" },
            { "32", "Referral(Genel)" },
            { "33", "Suresi Gecmis Kart, Karta El Koy" },
            { "34", "Fraud suphesi, Karta El Koy" },
            { "36", "Kisitli card, Karta El Koy" },
            { "37", "Calinti Kart.Issuer kartin iadesini istiyor" },
            { "38", "Izin verilebilen PIN giris sayisi asildi, Karta El Koy." },
            { "41", "Kayip Kart, Karta El Koy" },
            { "43", "Calinti Kart, Karta El Koy" },
            { "51", "Limit Yetersiz." },
            { "52", "No checking account" },
            { "53", "No savings account" },
            { "54", "Kartin Suresi Gecmis" },
            { "55", "PIN Yanlis" },
            { "56", "Kart kaydi yok" },
            { "57", "Kart sahibine acik olmayan islem" },
            { "58", "Terminale acik olmayan islem" },
            { "61", "Iptal miktarinin limiti asildi" },
            { "62", "Sinirli Card" },
            { "63", "Guvenlik ihlali" },
            { "65", "Aktivite limit asildi" },
            { "75", "Izin verilebilir PIN girme sayisi asildi" },
            { "76", "Anahtar eslestirme hatasi" },
            { "77", "Uyumsuz veri" },
            { "80", "Gecersiz Tarih" },
            { "81", "Sifreleme Hatasi" },
            { "82", "CVV Hatasi veya girilen CVV gecersiz" },
            { "83", "PIN dogrulanamiyor" },
            { "85", "Reddedildi(Genel)" },
            { "91", "Issuer veya switch islem yapamiyor." },
            { "92", "Timeout oldu, Reversal deneniyor" },
            { "93", "Cakisma, tamamlanamiyor(taksit, sadakat)" },
            { "96", "System arizasi" },
            { "98", "Cift Islem Gonderme" },
            { "99", "Basarisiz Islem." },
            { "1001", "Sistem hatasi." },
            { "1006", "Bu transactionId ile daha önce basarili bir islem gerçeklestirilmis" },
            { "1007", "Referans transaction alinamadi" },
            { "1044", "Debit kartlarla taksitli islem yapilamaz" },
            { "1046", "Iade isleminde tutar hatali." },
            { "1047", "Islem tutari geçersiz." },
            { "1049", "Geçersiz tutar." },
            { "1050", "CVV hatali." },
            { "1051", "Kredi karti numarasi hatali." },
            { "1052", "Kredi karti son kullanma tarihi hatali." },
            { "1054", "Islem numarasi hatali." },
            { "1059", "Yeniden iade denemesi." },
            { "1060", "Hatali taksit sayisi." },
            { "1061", "Ayni siparis numarasiyla daha önceden basarili islem yapilmis" },
            { "1065", "Ön provizyon daha önceden kapatilmis" },
            { "1073", "Terminal üzerinde aktif olarak bir batch bulunamadi" },
            { "1074", "Islem henüz sonlanmamis yada referans islem henüz tamamlanmamis." },
            { "1075", "Sadakat puan tutari hatali" },
            { "1076", "Sadakat puan kodu hatali" },
            { "1077", "Para kodu hatali" },
            { "1078", "Geçersiz siparis numarasi" },
            { "1079", "Geçersiz siparis açiklamasi" },
            { "1080", "Sadakat tutari ve para tutari gönderilmemis." },
            { "1081", "Maximum puan satışında taksitli işlem gönderilemez" },
            { "1082", "Geçersiz islem tipi" },
            { "1083", "Referans islem daha önceden iptal edilmis." },
            { "1084", "Geçersiz poas kart numarasi" },
            { "1085", "Bu poas kart numarasi daha önceden kayit edilmis" },
            { "1086", "Poas kart numarasiyla eslesen herhangibir kredi karti bulunamadi" },
            { "1087", "Yabanci para birimiyle taksitli provizyon kapama islemi yapilamaz" },
            { "1088", "Önprovizyon iptal edilmis" },
            { "1089", "Referans islem yapilmak istenen islem için uygun degil" },
            { "1090", "Bölüm numarasi bulunamiyor" },
            { "1091", "Recurring islemin toplam taksit sayisi hatali" },
            { "1092", "Recurring islemin tekrarlama araligi hatali" },
            { "1093", "Sadece Satis (Sale) islemi recurring olarak isaretlenebilir" },
            { "1095", "Lütfen geçerli bir email adresi giriniz" },
            { "1096", "Lütfen geçerli bir IP adresi giriniz" },
            { "1097", "Lütfen geçerli bir CAVV degeri giriniz" },
            { "1098", "Lütfen geçerli bir ECI degeri giriniz" },
            { "1099", "Lütfen geçerli bir Kart Sahibi ismi giriniz" },
            { "1100", "Lütfen geçerli bir brand girisi yapin." },
            { "1101", "Referans transaction reverse edilmis." },
            { "1102", "Recurring islem araligi geçersiz." },
            { "1103", "Taksit sayisi girilmeli" },
            { "2011", "Uygun Terminal Bulunamadi" },
            { "2200", "Is yerinin islem için gerekli hakki yok." },
            { "2202", "Islem iptal edilemez. (Batch Kapali )" },
            { "5001", "Is yeri sifresi yanlis." },
            { "5002", "Is yeri aktif degil." },
            { "6000", "Merchant IsActive Field Is Invalid" },
            { "6001", "Merchant ContactAddressLine1 Length Is Invalid" },
            { "6002", "Merchant ContactAddressLine2 Length Is Invalid" },
            { "6003", "Merchant ContactCityLength Is Invalid" },
            { "6004", "Merchant ContactEmail Must Be Valid Email" },
            { "6005", "Merchant ContactEmail Length Is Invalid" },
            { "6006", "Merchant ContactName Length Is Invalid" },
            { "6007", "Merchant ContactPhone Length Is Invalid" },
            { "6008", "Merchant HostMerchantId Length Is Invalid" },
            { "6009", "Merchant HostMerchantId Is Empty" },
            { "6010", "Merchant MerchantName Length Is Invalid" },
            { "6011", "Merchant MerchantPassword Length Is Invalid" },
            { "6012", "TerminalInfo HostTerminalId Is Invalid" },
            { "6013", "TerminalInfo HostTerminalId Length Is Invalid" },
            { "6014", "TerminalInfo HostTerminalId Is Empty" },
            { "6015", "TerminalInfo TerminalName Is Invalid" },
            { "6016", "Merchant DivisionDescription Is Invalid" },
            { "6017", "Merchant DivisionNumber Is Invalid" },
            { "6018", "Merchant Not Found" },
            { "6019", "InvalidRequest" },
            { "6020", "Division Is Already Exist" },
            { "6021", "Division Can Not Be Found" },
            { "6022", "Transaction Type Exist In Merchant Permission" },
            { "6023", "Merchant Permission Exist In Merchant" },
            { "6024", "Currency Code Exist In Merchant Currency Codes Permission" },
            { "6025", "Terminal Exist In MerchantTerminals" },
            { "6026", "Terminal Can Not Be Found In MerchantTerminals" },
            { "6027", "Invalid login attempti.Please check ClientId and ClientPassword fields" },
            { "6028", "Merchant is already exist. you should try to Update method" },
            { "7777", "Banka tarafinda gün sonu yapildigindan islem gerçeklestirilemedi" },
            { "9000", "Host iletişimi esnasında bir hata oluştu" },
            { "9001", "İşlem Yükleme Limit Aşıldı" }
        };
    }
}