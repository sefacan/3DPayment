using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using ThreeDPayment.Models;

namespace ThreeDPayment.Providers
{
    public class GarantiPaymentProvider : IPaymentProvider
    {
        private readonly HttpClient client;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public GarantiPaymentProvider(IHttpClientFactory httpClientFactory,
            IHttpContextAccessor httpContextAccessor)
        {
            client = httpClientFactory.CreateClient();
            _httpContextAccessor = httpContextAccessor;
        }

        public Task<PaymentGatewayResult> ThreeDGatewayRequest(PaymentGatewayRequest request)
        {
            try
            {
                string terminalUserId = request.BankParameters["terminalUserId"];
                string terminalId = request.BankParameters["terminalId"];
                string terminalMerchantId = request.BankParameters["terminalMerchantId"];
                string terminalProvUserId = request.BankParameters["terminalProvUserId"];
                string terminalProvPassword = request.BankParameters["terminalProvPassword"];
                string storeKey = request.BankParameters["storeKey"];//garanti sanal pos ekranından üreteceğimiz güvenlik anahtarı
                string mode = request.BankParameters["mode"];//PROD | TEST
                string type = "sales";

                var parameters = new Dictionary<string, object>();
                parameters.Add("cardnumber", request.CardNumber);
                parameters.Add("cardcvv2", request.CvvCode);//kart güvenlik kodu
                parameters.Add("cardexpiredatemonth", request.ExpireMonth);//kart bitiş ay'ı
                parameters.Add("cardexpiredateyear", request.ExpireYear);//kart bitiş yıl'ı
                parameters.Add("secure3dsecuritylevel", "3D_PAY");//SMS onaylı ödeme modeli 3DPay olarak adlandırılıyor.
                parameters.Add("mode", mode);
                parameters.Add("apiversion", "v0.01");
                parameters.Add("terminalprovuserid", terminalProvUserId);
                parameters.Add("terminaluserid", terminalUserId);
                parameters.Add("terminalmerchantid", terminalMerchantId);
                parameters.Add("terminalid", terminalId);
                parameters.Add("txntype", type);//direk satış
                parameters.Add("txncurrencycode", request.CurrencyIsoCode);//TL ISO code | EURO 978 | Dolar 840
                parameters.Add("motoind", "N");
                parameters.Add("customeripaddress", request.CustomerIpAddress);
                parameters.Add("orderaddressname1", request.CardHolderName);
                parameters.Add("orderid", request.OrderNumber);//sipariş numarası

                //işlem başarılı da olsa başarısız da olsa callback sayfasına yönlendirerek kendi tarafımızda işlem sonucunu kontrol ediyoruz
                parameters.Add("successurl", request.CallbackUrl);//başarılı dönüş adresi
                parameters.Add("errorurl", request.CallbackUrl);//hatalı dönüş adresi

                //garanti bankasında tutar bilgisinde nokta, virgül gibi değerler istenmiyor. 1.10 TL'lik işlem 110 olarak gönderilmeli. Yani tutarı 100 ile çarpabiliriz.
                string amount = (request.TotalAmount * 100m).ToString("N");//virgülden sonraki sıfırlara gerek yok
                parameters.Add("txnamount", amount);

                string installment = request.Installment.ToString();
                if (request.Installment <= 1)
                    installment = string.Empty;//0 veya 1 olması durumunda taksit bilgisini boş gönderiyoruz

                parameters.Add("txninstallmentcount", installment);//taksit sayısı | boş tek çekim olur

                //garanti tarafından terminal numarasını 9 haneye tamamlamak için başına sıfır eklenmesi isteniyor.
                string _terminalid = string.Format("{0:000000000}", int.Parse(terminalId));

                //provizyon şifresi ve 9 haneli terminal numarasının birleşimi ile bir hash oluşturuluyor
                string securityData = GetSHA1($"{terminalProvPassword}{_terminalid}");

                //ilgili veriler birleştirilip hash oluşturuluyor
                string hashstr = GetSHA1($"{terminalId}{request.OrderNumber}{amount}{request.CallbackUrl}{request.CallbackUrl}{type}{installment}{storeKey}{securityData}");
                parameters.Add("secure3dhash", hashstr);

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

            return Task.FromResult(VerifyGatewayResult.Successed(form["TransId"], form["hostrefnum"],
                int.Parse(form["txninstallmentcount"]), response,
                form["ProcReturnCode"], form["campaignchooselink"]));
        }

        public async Task<CancelPaymentResult> CancelRequest(CancelPaymentRequest request)
        {
            string terminalUserId = request.BankParameters["terminalUserId"];
            string terminalId = request.BankParameters["terminalId"];
            string terminalMerchantId = request.BankParameters["terminalMerchantId"];
            string terminalProvUserId = request.BankParameters["terminalProvUserId"];
            string terminalProvPassword = request.BankParameters["terminalProvPassword"];
            string mode = request.BankParameters["mode"];//PROD | TEST

            //garanti tarafından terminal numarasını 9 haneye tamamlamak için başına sıfır eklenmesi isteniyor.
            string _terminalid = string.Format("{0:000000000}", int.Parse(terminalId));

            //garanti bankasında tutar bilgisinde nokta, virgül gibi değerler istenmiyor. 1.10 TL'lik işlem 110 olarak gönderilmeli. Yani tutarı 100 ile çarpabiliriz.
            string amount = (request.TotalAmount * 100m).ToString("N");//virgülden sonraki sıfırlara gerek yok

            //provizyon şifresi ve 9 haneli terminal numarasının birleşimi ile bir hash oluşturuluyor
            string securityData = GetSHA1($"{terminalProvPassword}{_terminalid}");

            //ilgili veriler birleştirilip hash oluşturuluyor
            string hashstr = GetSHA1($"{request.OrderNumber}{terminalId}{amount}{securityData}");

            string requestXml = $@"<?xml version=""1.0"" encoding=""utf-8""?>
                                        <GVPSRequest>
                                            <Mode>{mode}</Mode>
                                            <Version>v0.01</Version>
                                            <ChannelCode></ChannelCode>
                                            <Terminal>
                                                <ProvUserID>{terminalProvUserId}</ProvUserID>
                                                <HashData>{hashstr}</HashData>
                                                <UserID>{terminalUserId}</UserID>
                                                <ID>{terminalId}</ID>
                                                <MerchantID>{terminalMerchantId}</MerchantID>
                                            </Terminal>
                                            <Customer>
                                                <IPAddress>{_httpContextAccessor.HttpContext.Connection.RemoteIpAddress}</IPAddress>
                                                <EmailAddress></EmailAddress>
                                            </Customer>
                                            <Order>
                                                <OrderID>{request.OrderNumber}</OrderID>
                                                <GroupID></GroupID>
                                            </Order>
                                            <Transaction>
                                                <Type>void</Type>
                                                <InstallmentCnt>{request.Installment}</InstallmentCnt>
                                                <Amount>{amount}</Amount>
                                                <CurrencyCode>{request.CurrencyIsoCode}</CurrencyCode>
                                                <CardholderPresentCode>0</CardholderPresentCode>
                                                <MotoInd>N</MotoInd>
                                                <OriginalRetrefNum>{request.ReferenceNumber}</OriginalRetrefNum>
                                            </Transaction>
                                        </GVPSRequest>";

            var response = await client.PostAsync(request.BankParameters["verifyUrl"], new StringContent(requestXml, Encoding.UTF8, "text/xml"));
            string responseContent = await response.Content.ReadAsStringAsync();

            var xmlDocument = new XmlDocument();
            xmlDocument.LoadXml(responseContent);

            if (xmlDocument.SelectSingleNode("GVPSResponse/Transaction/Response/ReasonCode") == null ||
                xmlDocument.SelectSingleNode("GVPSResponse/Transaction/Response/ReasonCode").InnerText != "00" ||
                xmlDocument.SelectSingleNode("GVPSResponse/Transaction/Response/ReasonCode").InnerText != "0000")
            {
                string errorMessage = xmlDocument.SelectSingleNode("GVPSResponse/Transaction/Response/ErrorMsg")?.InnerText ?? string.Empty;
                if (string.IsNullOrEmpty(errorMessage))
                    errorMessage = "Bankadan hata mesajı alınamadı.";

                return CancelPaymentResult.Failed(errorMessage);
            }

            string transactionId = xmlDocument.SelectSingleNode("GVPSResponse/Transaction/RetrefNum")?.InnerText;
            return CancelPaymentResult.Successed(transactionId);
        }

        public async Task<RefundPaymentResult> RefundRequest(RefundPaymentRequest request)
        {
            string terminalUserId = request.BankParameters["terminalUserId"];
            string terminalId = request.BankParameters["terminalId"];
            string terminalMerchantId = request.BankParameters["terminalMerchantId"];
            string terminalProvUserId = request.BankParameters["terminalProvUserId"];
            string terminalProvPassword = request.BankParameters["terminalProvPassword"];
            string mode = request.BankParameters["mode"];//PROD | TEST

            //garanti terminal numarasını 9 haneye tamamlamak için başına sıfır eklenmesini istiyor.
            string _terminalid = string.Format("{0:000000000}", int.Parse(terminalId));

            //garanti tutar bilgisinde nokta, virgül gibi değerler istenmiyor. 1.10 TL'lik işlem 110 olarak gönderilmeli. Yani tutarı 100 ile çarpabiliriz.
            string amount = (request.TotalAmount * 100m).ToString("N");//virgülden sonraki sıfırlara gerek yok

            //provizyon şifresi ve 9 haneli terminal numarasının birleşimi ile bir hash oluşturuluyor
            string securityData = GetSHA1($"{terminalProvPassword}{_terminalid}");

            //ilgili veriler birleştirilip hash oluşturuluyor
            string hashstr = GetSHA1($"{request.OrderNumber}{terminalId}{amount}{securityData}");

            string requestXml = $@"<?xml version=""1.0"" encoding=""utf-8""?>
                                        <GVPSRequest>
                                            <Mode>{mode}</Mode>
                                            <Version>v0.01</Version>
                                            <ChannelCode></ChannelCode>
                                            <Terminal>
                                                <ProvUserID>{terminalProvUserId}</ProvUserID>
                                                <HashData>{hashstr}</HashData>
                                                <UserID>{terminalUserId}</UserID>
                                                <ID>{terminalId}</ID>
                                                <MerchantID>{terminalMerchantId}</MerchantID>
                                            </Terminal>
                                            <Customer>
                                                <IPAddress>{_httpContextAccessor.HttpContext.Connection.RemoteIpAddress}</IPAddress>
                                                <EmailAddress></EmailAddress>
                                            </Customer>
                                            <Order>
                                                <OrderID>{request.OrderNumber}</OrderID>
                                                <GroupID></GroupID>
                                            </Order>
                                            <Transaction>
                                                <Type>refund</Type>
                                                <InstallmentCnt>{request.Installment}</InstallmentCnt>
                                                <Amount>{amount}</Amount>
                                                <CurrencyCode>{request.CurrencyIsoCode}</CurrencyCode>
                                                <CardholderPresentCode>0</CardholderPresentCode>
                                                <MotoInd>N</MotoInd>
                                                <OriginalRetrefNum>{request.ReferenceNumber}</OriginalRetrefNum>
                                            </Transaction>
                                        </GVPSRequest>";

            var response = await client.PostAsync(request.BankParameters["verifyUrl"], new StringContent(requestXml, Encoding.UTF8, "text/xml"));
            string responseContent = await response.Content.ReadAsStringAsync();

            var xmlDocument = new XmlDocument();
            xmlDocument.LoadXml(responseContent);

            if (xmlDocument.SelectSingleNode("GVPSResponse/Transaction/Response/ReasonCode") == null ||
                xmlDocument.SelectSingleNode("GVPSResponse/Transaction/Response/ReasonCode").InnerText != "00" ||
                xmlDocument.SelectSingleNode("GVPSResponse/Transaction/Response/ReasonCode").InnerText != "0000")
            {
                string errorMessage = xmlDocument.SelectSingleNode("GVPSResponse/Transaction/Response/ErrorMsg")?.InnerText ?? string.Empty;
                if (string.IsNullOrEmpty(errorMessage))
                    errorMessage = "Bankadan hata mesajı alınamadı.";

                return RefundPaymentResult.Failed(errorMessage);
            }

            string transactionId = xmlDocument.SelectSingleNode("GVPSResponse/Transaction/RetrefNum")?.InnerText;
            return RefundPaymentResult.Successed(transactionId);
        }

        public async Task<PaymentDetailResult> PaymentDetailRequest(PaymentDetailRequest request)
        {
            string terminalUserId = request.BankParameters["terminalUserId"];
            string terminalId = request.BankParameters["terminalId"];
            string terminalMerchantId = request.BankParameters["terminalMerchantId"];
            string terminalProvUserId = request.BankParameters["terminalProvUserId"];
            string terminalProvPassword = request.BankParameters["terminalProvPassword"];
            string mode = request.BankParameters["mode"];//PROD | TEST

            //garanti terminal numarasını 9 haneye tamamlamak için başına sıfır eklenmesini istiyor.
            string _terminalid = string.Format("{0:000000000}", int.Parse(terminalId));

            //provizyon şifresi ve 9 haneli terminal numarasının birleşimi ile bir hash oluşturuluyor
            string securityData = GetSHA1($"{terminalProvPassword}{_terminalid}");

            string amount = "100";//sabit 100 gönderin dediler. Yani 1 TL.

            //ilgili veriler birleştirilip hash oluşturuluyor
            string hashstr = GetSHA1($"{request.OrderNumber}{terminalId}{amount}{securityData}");

            string requestXml = $@"<?xml version=""1.0"" encoding=""utf-8""?>
                                        <GVPSRequest>
                                           <Mode>{mode}</Mode>
                                           <Version>v0.01</Version>
                                           <ChannelCode />
                                           <Terminal>
                                              <ProvUserID>{terminalProvUserId}</ProvUserID>
                                              <HashData>{hashstr}</HashData>
                                              <UserID>{terminalUserId}</UserID>
                                              <ID>{terminalId}</ID>
                                              <MerchantID>{terminalMerchantId}</MerchantID>
                                           </Terminal>
                                           <Customer>
                                              <IPAddress>{_httpContextAccessor.HttpContext.Connection.RemoteIpAddress}</IPAddress>
                                              <EmailAddress></EmailAddress>
                                           </Customer>
                                           <Card>
                                              <Number />
                                              <ExpireDate />
                                              <CVV2 />
                                           </Card>
                                           <Order>
                                              <OrderID>{request.OrderNumber}</OrderID>
                                              <GroupID />
                                           </Order>
                                           <Transaction>
                                              <Type>orderinq</Type>
                                              <InstallmentCnt />
                                              <Amount>{amount}</Amount>
                                              <CurrencyCode>{request.CurrencyIsoCode}</CurrencyCode>
                                              <CardholderPresentCode>0</CardholderPresentCode>
                                              <MotoInd>N</MotoInd>
                                           </Transaction>
                                        </GVPSRequest>";

            var response = await client.PostAsync(request.BankParameters["verifyUrl"], new StringContent(requestXml, Encoding.UTF8, "text/xml"));
            string responseContent = await response.Content.ReadAsStringAsync();

            var xmlDocument = new XmlDocument();
            xmlDocument.LoadXml(responseContent);

            string finalStatus = xmlDocument.SelectSingleNode("GVPSResponse/Order/OrderInqResult/Status")?.InnerText ?? string.Empty;
            string transactionId = xmlDocument.SelectSingleNode("GVPSResponse/Transaction/RetrefNum")?.InnerText;
            string referenceNumber = xmlDocument.SelectSingleNode("GVPSResponse/Transaction/RetrefNum")?.InnerText;
            string cardPrefix = xmlDocument.SelectSingleNode("GVPSResponse/Order/OrderInqResult/CardNumberMasked")?.InnerText;
            string installment = xmlDocument.SelectSingleNode("GVPSResponse/Order/OrderInqResult/InstallmentCnt")?.InnerText ?? "0";
            string bankMessage = xmlDocument.SelectSingleNode("GVPSResponse/Transaction/Response/Message")?.InnerText;
            string responseCode = xmlDocument.SelectSingleNode("GVPSResponse/Transaction/Response/ReasonCode")?.InnerText;

            if (finalStatus.Equals("APPROVED", StringComparison.OrdinalIgnoreCase))
            {
                return PaymentDetailResult.PaidResult(transactionId, referenceNumber, cardPrefix, int.Parse(installment), bankMessage, responseCode);
            }
            else if (finalStatus.Equals("VOID", StringComparison.OrdinalIgnoreCase))
            {
                return PaymentDetailResult.CanceledResult(transactionId, referenceNumber, bankMessage, responseCode);
            }
            else if (finalStatus.Equals("REFUNDED", StringComparison.OrdinalIgnoreCase))
            {
                return PaymentDetailResult.RefundedResult(transactionId, referenceNumber, bankMessage, responseCode);
            }

            var bankErrorMessage = xmlDocument.SelectSingleNode("GVPSResponse/Transaction/Response/SysErrMsg")?.InnerText ?? string.Empty;
            var errorMessage = xmlDocument.SelectSingleNode("GVPSResponse/Transaction/Response/ErrorMsg")?.InnerText ?? string.Empty;
            if (string.IsNullOrEmpty(errorMessage))
                errorMessage = "Bankadan hata mesajı alınamadı.";

            return PaymentDetailResult.FailedResult(bankErrorMessage, responseCode, errorMessage);
        }

        private string GetSHA1(string text)
        {
            var cryptoServiceProvider = new SHA1CryptoServiceProvider();
            var inputbytes = cryptoServiceProvider.ComputeHash(Encoding.UTF8.GetBytes(text));

            var builder = new StringBuilder();
            for (int i = 0; i < inputbytes.Length; i++)
            {
                builder.Append(string.Format("{0,2:x}", inputbytes[i]).Replace(" ", "0"));
            }

            return builder.ToString().ToUpper();
        }

        public Dictionary<string, string> TestParameters => new Dictionary<string, string>
        {
            { "terminalUserId", "1" },
            { "terminalId", "1" },
            { "terminalMerchantId", "1" },
            { "terminalProvUserId", "1" },
            { "terminalProvPassword", "1" },
            { "storeKey", "1" },
            { "mode", "TEST" },
            { "gatewayUrl", "https://sanalposprov.garanti.com.tr/VPServlet" },
            { "verifyUrl", "https://sanalposprov.garanti.com.tr/VPServlet" }
        };

        private static readonly IDictionary<string, string> ErrorCodes = new Dictionary<string, string>
        {
            { "1", "Bankasından Provizyon Alınız." },
            { "2", "Bankasından Provizyon Alınız.(Visa)" },
            { "3", "Üye İşyeri Kategori Kodu Hatalı." },
            { "4", "Karta El Koy" },
            { "5", "İşlem Onaylanmadı." },
            { "6", "İşlem Kabul Edilmedi." },
            { "7", "Karta El Koyunuz." },
            { "8", "Kimliğini Kontrol Ederek Işlemi Yapınız." },
            { "9", "Kart Yenilenmiş. Müşteriden Isteyin" },
            { "11", "İşlem Gerçekleştirildi(Vip)." },
            { "12", "Geçersiz Işlem." },
            { "13", "Geçersiz Tutar." },
            { "14", "Kart Numarası Hatalı." },
            { "15", "Bankası Bulunamadı/iem Routing Problem." },
            { "16", "Bakiye Yetersiz. Yarın Tekrar Deneyin." },
            { "17", "İşlem İptal Edildi." },
            { "18", "Kapalı Kart.tekrar Denemeyin." },
            { "19", "Bir Kere Daha Provizyon Talep Ediniz." },
            { "21", "İşlem Iptal Edilemedi." },
            { "25", "Böyle Bir Bilgi Bulunamadı." },
            { "28", "Orijinali Rededilmiş/dosya Servis Dışı." },
            { "29", "İptal Yapılamadı.(Orjinali Bulunamadı)" },
            { "30", "Mesajı Nformatı Hatalı." },
            { "31", "Issuer Sign-on Olmamış." },
            { "32", "İşlem Kısmen Gerçekleştirilebildi." },
            { "33", "Kartın Süresi Dolmuş! Karta El Koyunuz." },
            { "34", "Muhtemelen Çalıntı Kart!!! El Koyunuz." },
            { "36", "Sınırlandırılmış Kart!! El Koyunuz." },
            { "37", "Lütfen Banka Güvenliğini Arayınız." },
            { "38", "Şifre Giriş Limiti Aşıldı!! El Koyunuz." },
            { "39", "Kredi Hesabı Tanımsız." },
            { "41", "Kayıp Kart!!!karta El Koyunuz." },
            { "43", "çalıntı Kart!!!karta El Koyunuz." },
            { "51", "Hesap Müsait Değil." },
            { "52", "Çek Hesabı Tanımsız." },
            { "53", "Hesap Tanımsız." },
            { "54", "Vadesi Dolmuş Kart." },
            { "55", "Şifre Hatalı." },
            { "56", "Bu Kart Mevcut Değil." },
            { "57", "Kart Sahibi Bu İşlemi Yapamaz." },
            { "58", "Bu Işlemiy Apmanıza Müsaade Edilmiyor." },
            { "61", "Para Çekme Limiti Aşılıyor." },
            { "62", "Kısıtlı Kart/kendi Ülkesinde Geçerli." },
            { "63", "Bu İşlemi Yapmaya Yetkili Değilsiniz" },
            { "65", "Günlük İşlem Adedi Dolmuş." },
            { "68", "Cevapçokgeçgeldi.i̇şlemii̇ptalediniz." },
            { "75", "Şifre Giriş Limiti Aşıldı." },
            { "76", "Şifre Hatalı. Şifre Giriş Limiti Aşıldı." },
            { "77", "Orjinal Işlem Ile Uyumsuz Bilgi Alındı." },
            { "78", "Account Balance Not Available." },
            { "80", "Hatalı Tarih/network Hatası." },
            { "81", "Şifreleme/yabancı Network Hatası." },
            { "82", "Hatalı Cvv/issuer Cevap Vermedi." },
            { "83", "Şifre Doğrulanamıyor/i̇letişim Hatası." },
            { "85", "Hesap Doğrulandı." },
            { "86", "Şifre Doğrulanamıyor." },
            { "88", "Şifreleme Hatası." }
        };
    }
}