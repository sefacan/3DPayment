using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using ThreeDPayment.Requests;
using ThreeDPayment.Results;

namespace ThreeDPayment.Providers
{
    public class VakifbankPaymentProvider : IPaymentProvider
    {
        private readonly HttpClient client;

        public VakifbankPaymentProvider(IHttpClientFactory httpClientFactory)
        {
            client = httpClientFactory.CreateClient();
        }

        public async Task<PaymentGatewayResult> ThreeDGatewayRequest(PaymentGatewayRequest request)
        {
            try
            {
                string merchantId = request.BankParameters["merchantId"];
                string merchantPassword = request.BankParameters["merchantPassword"];
                string enrollmentUrl = request.BankParameters["enrollmentUrl"];

                var httpParameters = new Dictionary<string, string>();
                httpParameters.Add("Pan", request.CardNumber);
                httpParameters.Add("ExpiryDate", $"{string.Format("{0:00}", request.ExpireYear)}{string.Format("{0:00}", request.ExpireMonth)}");
                httpParameters.Add("PurchaseAmount", request.TotalAmount.ToString("N2", new CultureInfo("en-US")));
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
                httpParameters.Add("SuccessUrl", request.CallbackUrl.ToString());
                httpParameters.Add("FailureUrl", request.CallbackUrl.ToString());

                string installment = request.Installment.ToString();
                if (request.Installment < 2)
                    installment = string.Empty;//0 veya 1 olması durumunda taksit bilgisini boş gönderiyoruz

                httpParameters.Add("InstallmentCount", installment);

                var response = await client.PostAsync(enrollmentUrl, new FormUrlEncodedContent(httpParameters));
                string responseContent = await response.Content.ReadAsStringAsync();

                var xmlDocument = new XmlDocument();
                xmlDocument.LoadXml(responseContent);
                var statusNode = xmlDocument.SelectSingleNode("IPaySecure/Message/VERes/Status");
                if (statusNode.InnerText != "Y")
                {
                    var messageErrorNode = xmlDocument.SelectSingleNode("IPaySecure/ErrorMessage");
                    var messageErrorCodeNode = xmlDocument.SelectSingleNode("IPaySecure/MessageErrorCode");

                    return PaymentGatewayResult.Failed(messageErrorNode.InnerText, messageErrorCodeNode?.InnerText);
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
                return PaymentGatewayResult.Successed(parameters, acsUrlNode.InnerText);
            }
            catch (Exception ex)
            {
                return PaymentGatewayResult.Failed(ex.ToString());
            }
        }

        public async Task<VerifyGatewayResult> VerifyGateway(VerifyGatewayRequest request, PaymentGatewayRequest gatewayRequest, IFormCollection form)
        {
            if (form == null)
            {
                return VerifyGatewayResult.Failed("Form verisi alınamadı.");
            }

            var status = form["Status"].ToString();
            if (string.IsNullOrEmpty(status))
            {
                return VerifyGatewayResult.Failed("İşlem sonuç bilgisi alınamadı.");
            }

            if (!status.Equals("Y"))
            {
                string errorMessage = "3D doğrulama başarısız";
                if (ErrorCodes.ContainsKey(form["ErrorCode"]))
                    errorMessage = ErrorCodes[form["ErrorCode"]];

                return VerifyGatewayResult.Failed(errorMessage, form["ErrorCode"], status);
            }

            string merchantId = request.BankParameters["merchantId"];
            string merchantPassword = request.BankParameters["merchantPassword"];
            string terminalNo = request.BankParameters["terminalNo"];
            var expireMonth = string.Format("{0:00}", gatewayRequest.ExpireMonth);
            var expireYear = CultureInfo.CurrentCulture.Calendar.ToFourDigitYear(gatewayRequest.ExpireYear);
            var totalAmount = gatewayRequest.TotalAmount.ToString("N2", new CultureInfo("en-US"));

            var xmlBuilder = new StringBuilder();
            xmlBuilder.Append($@"<?xml version=""1.0"" encoding=""utf-8""?>
                                    <VposRequest>
                                        <MerchantId>{merchantId}</MerchantId>
                                        <Password>{merchantPassword}</Password>
                                        <TerminalNo>{terminalNo}</TerminalNo>
                                        <Pan>{gatewayRequest.CardNumber}</Pan>
                                        <Expiry>{expireYear}{expireMonth}</Expiry>
                                        <CurrencyAmount>{totalAmount}</CurrencyAmount>
                                        <CurrencyCode>{form["PurchCurrency"]}</CurrencyCode>
                                        <TransactionId></TransactionId>");

            //boş veya 0 ise taksit bilgisini gönderme
            if (int.TryParse(form["InstallmentCount"], out int installment) && installment > 1)
            {
                if (request.ManufacturerCard)
                {
                    xmlBuilder.Append($@"<TransactionType>TKSale</TransactionType>
                                         <CustomInstallments>
                                            <MaturityPeriod>{installment}</MaturityPeriod>
                                            <Frequency>{installment}</Frequency>
                                         </CustomInstallments>");
                }
                else
                {
                    xmlBuilder.Append($@"<TransactionType>Sale</TransactionType>
                                         <NumberOfInstallments>{installment}</NumberOfInstallments>");
                }
            }
            else
            {
                xmlBuilder.Append($@"<TransactionType>Sale</TransactionType>");
            }

            xmlBuilder.Append($@" <CardHoldersName>{form["card_holders_name"]}</CardHoldersName>
                                 <Cvv>{gatewayRequest.CvvCode}</Cvv>
                                 <ECI>{form["Eci"]}</ECI>
                                 <CAVV>{form["CAVV"]}</CAVV>
                                 <MpiTransactionId>{form["VerifyEnrollmentRequestId"]}</MpiTransactionId>
                                 <OrderId>{form["VerifyEnrollmentRequestId"]}</OrderId>
                                 <ClientIp>{gatewayRequest.CustomerIpAddress}</ClientIp>
                                 <TransactionDeviceSource>0</TransactionDeviceSource>
                             </VposRequest>");

            var parameters = new Dictionary<string, string>();
            parameters.Add("prmstr", xmlBuilder.ToString());
            var response = await client.PostAsync(request.BankParameters["verifyUrl"], new FormUrlEncodedContent(parameters));
            string responseContent = await response.Content.ReadAsStringAsync();

            var xmlDocument = new XmlDocument();
            xmlDocument.LoadXml(responseContent);
            var resultCodeNode = xmlDocument.SelectSingleNode("VposResponse/ResultCode");
            var resultDetailNode = xmlDocument.SelectSingleNode("VposResponse/ResultDetail");
            var transactionNode = xmlDocument.SelectSingleNode("VposResponse/TransactionId");

            if (resultCodeNode.InnerText != "0000")
            {
                string errorMessage = resultDetailNode.InnerText;
                if (string.IsNullOrEmpty(errorMessage) && ErrorCodes.ContainsKey(resultCodeNode.InnerText))
                    errorMessage = ErrorCodes[resultCodeNode.InnerText];

                return VerifyGatewayResult.Failed(errorMessage, resultCodeNode.InnerText, status);
            }

            int.TryParse(form["InstallmentCount"], out int installmentCount);
            int.TryParse(form["EXTRA.ARTITAKSIT"], out int extraInstallment);

            return VerifyGatewayResult.Successed(transactionNode.InnerText, form["Xid"],
                installmentCount, extraInstallment,
                resultDetailNode.InnerText,
                resultCodeNode.InnerText);
        }

        public async Task<CancelPaymentResult> CancelRequest(CancelPaymentRequest request)
        {
            string merchantId = request.BankParameters["merchantId"];
            string merchantPassword = request.BankParameters["merchantPassword"];

            string requestXml = $@"<VposRequest>
                                    <MerchantId>{merchantId}</MerchantId>
                                    <Password>{merchantPassword}</Password>
                                    <TransactionType>Cancel</TransactionType>
                                    <ReferenceTransactionId>{request.TransactionId}</ReferenceTransactionId>
                                    <ClientIp>{request.CustomerIpAddress}</ClientIp>
                                </VposRequest>";

            var parameters = new Dictionary<string, string>();
            parameters.Add("prmstr", requestXml);

            var response = await client.PostAsync(request.BankParameters["verifyUrl"], new FormUrlEncodedContent(parameters));
            string responseContent = await response.Content.ReadAsStringAsync();

            var xmlDocument = new XmlDocument();
            xmlDocument.LoadXml(responseContent);
            if (xmlDocument.SelectSingleNode("VposResponse/ResultCode") == null ||
                xmlDocument.SelectSingleNode("VposResponse/ResultCode").InnerText != "0000")
            {
                string errorMessage = xmlDocument.SelectSingleNode("VposResponse/ResultDetail")?.InnerText ?? string.Empty;
                if (string.IsNullOrEmpty(errorMessage))
                    errorMessage = "Bankadan hata mesajı alınamadı.";

                return CancelPaymentResult.Failed(errorMessage);
            }

            var transactionId = xmlDocument.SelectSingleNode("VposResponse/TransactionId")?.InnerText;
            return CancelPaymentResult.Successed(transactionId, transactionId);
        }

        public async Task<RefundPaymentResult> RefundRequest(RefundPaymentRequest request)
        {
            string merchantId = request.BankParameters["merchantId"];
            string merchantPassword = request.BankParameters["merchantPassword"];

            string requestXml = $@"<VposRequest>
                                    <MerchantId>{merchantId}</MerchantId>
                                    <Password>{merchantPassword}</Password>
                                    <TransactionType>Refund</TransactionType>
                                    <ReferenceTransactionId>{request.TransactionId}</ReferenceTransactionId>
                                    <ClientIp>{request.CustomerIpAddress}</ClientIp>
                                </VposRequest>";

            var parameters = new Dictionary<string, string>();
            parameters.Add("prmstr", requestXml);

            var response = await client.PostAsync(request.BankParameters["verifyUrl"], new FormUrlEncodedContent(parameters));
            string responseContent = await response.Content.ReadAsStringAsync();

            var xmlDocument = new XmlDocument();
            xmlDocument.LoadXml(responseContent);
            if (xmlDocument.SelectSingleNode("VposResponse/ResultCode") == null ||
                xmlDocument.SelectSingleNode("VposResponse/ResultCode").InnerText != "0000")
            {
                string errorMessage = xmlDocument.SelectSingleNode("VposResponse/ResultDetail")?.InnerText ?? string.Empty;
                if (string.IsNullOrEmpty(errorMessage))
                    errorMessage = "Bankadan hata mesajı alınamadı.";

                return RefundPaymentResult.Failed(errorMessage);
            }

            var transactionId = xmlDocument.SelectSingleNode("VposResponse/TransactionId")?.InnerText;
            return RefundPaymentResult.Successed(transactionId, transactionId);
        }

        public async Task<PaymentDetailResult> PaymentDetailRequest(PaymentDetailRequest request)
        {
            string merchantId = request.BankParameters["merchantId"];
            string merchantPassword = request.BankParameters["merchantPassword"];
            var startDate = request.PaidDate.AddDays(-1).ToString("yyyy-MM-dd");
            var endDate = request.PaidDate.AddDays(1).ToString("yyyy-MM-dd");

            string requestXml = $@"<?xml version=""1.0"" encoding=""utf-8""?>
                                        <SearchRequest>
                                           <MerchantCriteria>
                                              <HostMerchantId>{merchantId}</HostMerchantId>
                                              <MerchantPassword>{merchantPassword}</MerchantPassword>
                                           </MerchantCriteria>
                                           <DateCriteria>
                                              <StartDate>{startDate}</StartDate>
                                              <EndDate>{endDate}</EndDate>
                                           </DateCriteria>
                                           <TransactionCriteria>
                                              <TransactionId>{request.TransactionId}</TransactionId>
                                              <OrderId>{request.OrderNumber}</OrderId>
                                              <AuthCode />
                                           </TransactionCriteria>
                                        </SearchRequest>";

            var parameters = new Dictionary<string, string>();
            parameters.Add("prmstr", requestXml);

            var response = await client.PostAsync(request.BankParameters["verifyUrl"], new FormUrlEncodedContent(parameters));
            string responseContent = await response.Content.ReadAsStringAsync();

            var xmlDocument = new XmlDocument();
            xmlDocument.LoadXml(responseContent);

            var totalItemCount = int.Parse(xmlDocument.SelectSingleNode("SearchResponse/PagedResponseInfo/TotalItemCount").InnerText);
            if (totalItemCount < 1)
            {
                string errorMessage = xmlDocument.SelectSingleNode("SearchResponse/ResponseInfo/ResponseMessage").InnerText;
                if (string.IsNullOrEmpty(errorMessage))
                    errorMessage = "Bankadan hata mesajı alınamadı.";

                return PaymentDetailResult.FailedResult(errorMessage);
            }

            var transactionInfoNode = xmlDocument.SelectNodes("SearchResponse/TransactionSearchResultInfo/TransactionSearchResultInfo")
                .Cast<XmlNode>()
                .FirstOrDefault();

            if (transactionInfoNode == null)
            {
                string errorMessage = xmlDocument.SelectSingleNode("SearchResponse/ResponseInfo/ResponseMessage").InnerText;
                if (string.IsNullOrEmpty(errorMessage))
                    errorMessage = "Bankadan hata mesajı alınamadı.";

                return PaymentDetailResult.FailedResult(errorMessage);
            }

            string transactionId = transactionInfoNode.SelectSingleNode("TransactionId")?.InnerText;
            string referenceNumber = transactionInfoNode.SelectSingleNode("TransactionId")?.InnerText;
            string cardPrefix = transactionInfoNode.SelectSingleNode("PanMasked")?.InnerText;
            string bankMessage = transactionInfoNode.SelectSingleNode("ResponseMessage")?.InnerText;
            string responseCode = transactionInfoNode.SelectSingleNode("ResultCode")?.InnerText;

            var canceled = bool.Parse(transactionInfoNode.SelectSingleNode("IsCanceled")?.InnerText ?? "false");
            if (canceled)
            {
                return PaymentDetailResult.CanceledResult(transactionId, referenceNumber, bankMessage, responseCode);
            }

            var refunded = bool.Parse(transactionInfoNode.SelectSingleNode("IsRefunded")?.InnerText ?? "false");
            if (refunded)
            {
                return PaymentDetailResult.RefundedResult(transactionId, referenceNumber, bankMessage, responseCode);
            }

            if (responseCode == "0000")
            {
                return PaymentDetailResult.PaidResult(transactionId, referenceNumber, cardPrefix, bankMessage: bankMessage, responseCode: responseCode);
            }

            if (string.IsNullOrEmpty(bankMessage))
                bankMessage = "Bankadan hata mesajı alınamadı.";

            return PaymentDetailResult.FailedResult(errorMessage: bankMessage, errorCode: responseCode);
        }

        public Dictionary<string, string> TestParameters => new Dictionary<string, string>
        {
            { "merchantId", "655500056" },
            { "merchantPassword", "123456" },
            { "enrollmentUrl", "https://3dsecuretest.vakifbank.com.tr/MPIAPI/MPI_Enrollment.aspx" },
            { "verifyUrl", "https://onlineodemetest.vakifbank.com.tr:4443/UIService/TransactionSearchOperations.asmx" }
        };

        private static readonly IDictionary<string, string> ErrorCodes = new Dictionary<string, string>
        {
            { "0000", "Başarılı" },
            { "0001", "Bankanızı Arayın" },
            { "0002", "Bankanızı Arayın" },
            { "0003", "üye Kodu Hatalı/tanımsız" },
            { "0004", "Karta El Koy" },
            { "0005", "İşlem Onaylanmadı" },
            { "0006", "Hatalı İşlem" },
            { "0007", "Karta El Koy" },
            { "0009", "Tekrar Deneyin" },
            { "0010", "Tekrar Deneyin" },
            { "0011", "Tekrar Deneyin" },
            { "0012", "Geçersiz İşlem" },
            { "0013", "Geçersiz İşlem Tutarı" },
            { "0014", "Geçersiz Kart Numarası" },
            { "0015", "Müşteri Bulunamadı/bin Hatalı" },
            { "0021", "İşlem Onaylanmadı" },
            { "0030", "Mesaj Formatı Hatalı (üye İşyeri)" },
            { "0032", "Dosyasına Ulaşılamadı" },
            { "0033", "Süresi Bitmiş Kart" },
            { "0034", "Sahte Kart" },
            { "0036", "İşlem Onaylanmadı" },
            { "0038", "şifre Deneme Aşımı/karta El Koy" },
            { "0041", "Kayıp Kart - Karta El Koy" },
            { "0043", "çalıntı Kart - Karta El Koy" },
            { "0051", "Limit Yetersiz" },
            { "0052", "Hesap Numarasını Kontrol Edin" },
            { "0053", "Hesap Bulunamadı" },
            { "0054", "Geçersiz Kart" },
            { "0055", "Hatalı Kart Şifresi" },
            { "0056", "Kart Tanımlı Değil" },
            { "0057", "Kartın Işlem Izni Yok" },
            { "0058", "Pos İşlem Tipine Kapalı" },
            { "0059", "Sahtekarlık Şüphesi" },
            { "0061", "Para Çekme Tutar Limiti Aşıldı" },
            { "0062", "Yasaklanımş Kart" },
            { "0063", "Güvenlik Ihlali" },
            { "0065", "Günlük İşlem Adedi Limiti Aşıldı" },
            { "0075", "şifre Deneme Sayısı Aşıldı" },
            { "0077", "şifre Script Talebi Reddedildi" },
            { "0078", "şifre Güvenilir Değil" },
            { "0089", "İşlem Onaylanmadı" },
            { "0091", "Karti Veren Banka Hi̇zmet Dişi" },
            { "0092", "Bankasi Bi̇li̇nmi̇yor" },
            { "0093", "İşlem Onaylanmadı" },
            { "0096", "Bankasinin Si̇stemi̇ Arizali" },
            { "0312", "Geçersi̇z Kart" },
            { "0315", "Tekrar Deneyi̇ni̇z" },
            { "0320", "önprovi̇zyon Kapatilamadi" },
            { "0323", "önprovi̇zyon Kapatilamadi" },
            { "0357", "İşlem Onaylanmadı" },
            { "0358", "Kart Kapalı" },
            { "0381", "Red Karta El Koy" },
            { "0382", "Sahte Kart-karta El Koyunuz" },
            { "0501", "Geçersi̇z Taksi̇t/i̇şlem Tutari" },
            { "0503", "Kart Numarasi Hatali" },
            { "0504", "İşlem Onaylanmadı" },
            { "0540", "İade Edilecek İşlemin Orijinali Bulunamadı" },
            { "0541", "Orj. İşlemin Tamamı Iade Edildi" },
            { "0542", "İade İşlemi̇ Gerçekleşti̇ri̇lemez" },
            { "0550", "İşlem Ykb Pos Undan Yapilmali" },
            { "0570", "Yurtdişi Kart İşlem İzni̇ Yok" },
            { "0571", "İşyeri Amex İşlem İzni Yok" },
            { "0572", "İşyeri Amex Tanımları Eksik" },
            { "0574", "üye İşyeri̇ İşlem İzni̇ Yok" },
            { "0575", "İşlem Onaylanmadı" },
            { "0577", "Taksi̇tli̇ İşlem İzni̇ Yok" },
            { "0580", "Hatali 3d Güvenli̇k Bi̇lgi̇si̇" },
            { "0581", "Eci Veya Cavv Bilgisi Eksik" },
            { "0582", "Hatali 3d Güvenli̇k Bi̇lgi̇si̇" },
            { "0583", "Tekrar Deneyi̇ni̇z" },
            { "0880", "İşlem Onaylanmadı" },
            { "0961", "İşlem Ti̇pi̇ Geçersi̇z" },
            { "0962", "Terminalid Tanımısız" },
            { "0963", "üye İşyeri Tanımlı Değil" },
            { "0966", "İşlem Onaylanmadı" },
            { "0971", "Eşleşmiş Bir Işlem Iptal Edilemez" },
            { "0972", "Para Kodu Geçersiz" },
            { "0973", "İşlem Onaylanmadı" },
            { "0974", "İşlem Onaylanmadı" },
            { "0975", "üye İşyeri̇ İşlem İzni̇ Yok" },
            { "0976", "İşlem Onaylanmadı" },
            { "0978", "Kartin Taksi̇tli̇ İşleme İzni̇ Yok" },
            { "0980", "İşlem Onaylanmadı" },
            { "0981", "Eksi̇k Güvenli̇k Bi̇lgi̇si̇" },
            { "0982", "İşlem İptal Durumda. İade Edi̇lemez" },
            { "0983", "İade Edilemez,iptal" },
            { "0984", "İade Tutar Hatasi" },
            { "0985", "İşlem Onaylanmadı." },
            { "0986", "Gib Taksit Hata" },
            { "0987", "İşlem Onaylanmadı." },
            { "8484", "Birden Fazla Hata Olması Durumunda Geri Dönülür. Resultdetail Alanından Detayları Alınabilir." },
            { "1001", "Sistem Hatası." },
            { "1006", "Bu Transactionid Ile Daha Önce Başarılı Bir Işlem Gerçekleştirilmiş" },
            { "1007", "Referans Transaction Alınamadı" },
            { "1046", "İade Işleminde Tutar Hatalı." },
            { "1047", "İşlem Tutarı Geçersizdir." },
            { "1049", "Geçersiz Tutar." },
            { "1050", "Cvv Hatalı." },
            { "1051", "Kredi Kartı Numarası Hatalıdır." },
            { "1052", "Kredi Kartı Son Kullanma Tarihi Hatalı." },
            { "1054", "İşlem Numarası Hatalıdır." },
            { "1059", "Yeniden Iade Denemesi." },
            { "1060", "Hatalı Taksit Sayısı." },
            { "2011", "Terminal no bulunamadı." },
            { "2200", "İş Yerinin Işlem Için Gerekli Hakkı Yok." },
            { "2202", "İşlem Iptal Edilemez. ( Batch Kapalı )" },
            { "5001", "İş Yeri Şifresi Yanlış." },
            { "5002", "İş Yeri Aktif Değil." },
            { "1073", "Terminal Üzerinde Aktif Olarak Bir Batch Bulunamadı" },
            { "1074", "İşlem Henüz Sonlanmamış Yada Referans Işlem Henüz Tamamlanmamış." },
            { "1075", "Sadakat Puan Tutarı Hatalı" },
            { "1076", "Sadakat Puan Kodu Hatalı" },
            { "1077", "Para Kodu Hatalı" },
            { "1078", "Geçersiz Sipariş Numarası" },
            { "1079", "Geçersiz Sipariş Açıklaması" },
            { "1080", "Sadakat Tutarı Ve Para Tutarı Gönderilmemiş." },
            { "1061", "Aynı Sipariş Numarasıyla (Orderid) Daha Önceden Başarılı Işlem Yapılmış" },
            { "1065", "ön Provizyon Daha Önceden Kapatılmış" },
            { "1082", "Geçersiz Işlem Tipi" },
            { "1083", "Referans Işlem Daha Önceden Iptal Edilmiş." },
            { "1084", "Geçersiz Poaş Kart Numarası" },
            { "7777", "Banka Tarafında Gün Sonu Yapıldığından Işlem Gerçekleştirilemedi" },
            { "1087", "Yabancı Para Birimiyle Taksitli Provizyon Kapama Işlemi Yapılamaz" },
            { "1088", "önprovizyon Iptal Edilmiş" },
            { "1089", "Referans Işlem Yapılmak Istenen Işlem Için Uygun Değil" },
            { "1091", "Recurring Işlemin Toplam Taksit Sayısı Hatalı" },
            { "1092", "Recurring Işlemin Tekrarlama Aralığı Hatalı" },
            { "1093", "Sadece Satış (Sale) Işlemi Recurring Olarak Işaretlenebilir" },
            { "1095", "Lütfen Geçerli Bir Email Adresi Giriniz" },
            { "1096", "Lütfen Geçerli Bir Ip Adresi Giriniz" },
            { "1097", "Lütfen Geçerli Bir Cavv Değeri Giriniz" },
            { "1098", "Lütfen Geçerli Bir Eci Değeri Giriniz." },
            { "1099", "Lütfen Geçerli Bir Kart Sahibi Ismi Giriniz." },
            { "1100", "Lütfen Geçerli Bir Brand Girişi Yapın." },
            { "1105", "üye Işyeri Ip Si Sistemde Tanımlı Değil" },
            { "1102", "Recurring Işlem Aralık Tipi Hatalı Bir Değere Sahip" },
            { "1101", "Referans Transaction Reverse Edilmiş." },
            { "1104", "İlgili Taksit Için Tanım Yok" },
            { "1111", "Bu Üye Işyeri Non Secure Işlem Yapamaz" },
            { "1122", "Surchargeamount Değeri 0 Dan Büyük Olmalıdır." },
            { "6000", "Talep Mesajı Okunamadı." },
            { "6001", "İstek Httppost Yöntemi Ile Yapılmalıdır." },
            { "6003", "Pox Request Adresine Istek Yapıyorsunuz. Mesaj Boş Geldi. İstek Xml Mesajını Prmstr Parametresi Ile Iletiniz." },
            { "9117", "3dsecure Islemlerde Eci Degeri Bos Olamaz." },
            { "33",   "Kartın 3d Secure Şifre Doğrulaması Yapılamadı" },
            { "400",  "3d Şifre Doğrulaması Yapılamadı." },
            { "1026", "Failureurl Format Hatası" },
            { "2000", "Acquirer Info Is Empty" },
            { "2005", "Merchant Cannot Be Found For This Bank" },
            { "2006", "Merchant Acquirer Bin Password Required" },
            { "2009", "Brand Not Found" },
            { "2010", "Cardholder Info Is Empty" },
            { "2012", "Devicecategory Must Be Between 0 And 2" },
            { "2013", "Threed Secure Message Can Not Be Found" },
            { "2014", "Pares Message Id Does Not Match Threed Secure Message Id" },
            { "2015", "Signature Verification False" },
            { "2017", "Acquirebin Can Not Be Found" },
            { "2018", "Merchant Acquirer Bin Password Wrong" },
            { "2019", "Bank Not Found" },
            { "2020", "Bank Id Does Not Match Merchant Bank" },
            { "2021", "Invalid Currency Code" },
            { "2022", "Verify Enrollmentrequest Id Cannot Be Empty" },
            { "2023", "Verify Enrollment Request Id Already Exist For This Merchant" },
            { "2024", "Acs Certificate Cannot Be Found In Database" },
            { "2025", "Certificate Could Not Be Found In Certificate Store" },
            { "2026", "Brand Certificate Not Found In Store" },
            { "2027", "Invalid Xml File" },
            { "2028", "Threed Secure Message Is Invalid State" },
            { "2029", "Invalid Pan" },
            { "2030", "Invalid Expire Date" },
            { "1002", "Successurl Format Is Invalid." },
            { "1003", "Brandid Format Is Invalid" },
            { "1004", "Devicecategory Format Is Invalid" },
            { "1005", "Sessioninfo Format Is Invalid" },
            { "1008", "Purchaseamount Format Is Invalid" },
            { "1009", "Expire Date Format Is Invalid" },
            { "1010", "Pan Format Is Invalid" },
            { "1011", "Merchant Acquirer Bin Password Format Is Invalid" },
            { "1012", "Hostmerchant Format Is Invalid" },
            { "1013", "Bankid Format Is Invalid" },
            { "2031", "Verification Failed: No Signature Was Found In The Document" },
            { "2032", "Verification Failed: More That One Signature Was Found For The Document" },
            { "2033", "Actual Brand Can Not Be Found" },
            { "2034", "Invalid Amount" },
            { "1014", "Is Recurring Format Is Invalid" },
            { "1015", "Recurring Frequency Format Is Invalid" },
            { "1016", "Recurring End Date Format Is Invalid" },
            { "2035", "Invalid Recurring Information" },
            { "2036", "Invalid Recurring Frequency" },
            { "2037", "Invalid Reccuring End Date" },
            { "2038", "Recurring End Date Must Be Greater Than Expire Date" },
            { "2039", "Invalid X509 Certificate Data" },
            { "2040", "Invalid Installment" },
            { "1017", "Installment Count Format Is Invalid" },
            { "3000", "Bank Not Found" },
            { "3001", "Country Not Found" },
            { "3002", "Invalid Failurl" },
            { "3003", "Hostmerchantnumber Cannot  Be Empty" },
            { "3004", "Merchantbrandacquirerbin Cannot Be Empty" },
            { "3005", "Merchantname Cannot Be Empty" },
            { "3006", "Merchantpassword Cannot Be Empty" },
            { "3007", "Invalid Sucessurl" },
            { "3008", "Invalid Merchantsiteurl" },
            { "3009", "Invalid Acquirerbin Length" },
            { "3010", "Brand Cannot Be Null" },
            { "3011", "Invalid Acquirerbinpassword Length" },
            { "3012", "Invalid Hostmerchantnumber Length" },
            { "2041", "Pares Exponent Value Does Not Match Pareq Exponent" },
            { "2042", "Pares Acquirer Bin Value Does Not Match Pareq Acqiurer Bin" },
            { "2043", "Pares Merchant Id Does Not Match Pareq Merchant Id" },
            { "2044", "Pares Xid Does Not Match Pareq Xid" },
            { "2045", "Pares Purchase Amount Does Not Match Pareq Purchase Amount" },
            { "2046", "Pares Currency Does Not Match Pareq Currency" },
            { "2047", "Veres Xsd Validation Error" },
            { "2048", "Pares Xsd Validation Exception" },
            { "2049", "Invalid Request" },
            { "2050", "File Is Empty" },
            { "2051", "Custom Error" },
            { "2052", "Bank Brand Bin Already Exist" },
            { "3013", "End Date Must Be Greater Than Start" },
            { "3014", "Start Date Must Be Greater Than Datetime Minval" },
            { "3015", "End Date Must Be Greater Than Datetime Minval" },
            { "3016", "Invalid Search Period" },
            { "3017", "Bin Cannot Be Empty" },
            { "3018", "Card Type Cannot Be Empty" },
            { "3019", "Bank Brand Bin Not Found" },
            { "3020", "Bin Length Must Be Six" },
            { "2053", "Directory Server Communication Error" },
            { "2054", "Acs Hata Bildirdi" },
            { "5037", "Successurl Alanı Hatalıdır." }
        };
    }
}