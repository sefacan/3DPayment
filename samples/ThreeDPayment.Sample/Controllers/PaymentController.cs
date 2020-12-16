using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Net.Http.Headers;
using Newtonsoft.Json;
using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using ThreeDPayment.Requests;
using ThreeDPayment.Results;
using ThreeDPayment.Sample.Domains;
using ThreeDPayment.Sample.Helpers;
using ThreeDPayment.Sample.Models;
using ThreeDPayment.Sample.Services;

namespace ThreeDPayment.Controllers
{
    public class PaymentController : Controller
    {
        private const string PaymentSessionName = "PaymentInfo";
        private const string PaymentResultSessionName = "PaymentResult";

        private readonly IBankService _bankService;
        private readonly IPaymentService _paymentService;
        private readonly IHtmlHelper _htmlHelper;
        private readonly IPaymentProviderFactory _paymentProviderFactory;

        public PaymentController(IBankService bankService,
            IPaymentService paymentService,
            IHtmlHelper htmlHelper,
            IPaymentProviderFactory paymentProviderFactory)
        {
            _bankService = bankService;
            _paymentService = paymentService;
            _htmlHelper = htmlHelper;
            _paymentProviderFactory = paymentProviderFactory;
        }

        public IActionResult Index()
        {
            PaymentViewModel model = new PaymentViewModel();
            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> Index([FromForm] PaymentViewModel model)
        {
            try
            {
                //gateway request
                PaymentGatewayRequest gatewayRequest = new PaymentGatewayRequest
                {
                    CardHolderName = model.CardHolderName,
                    //clear credit card unnecessary characters
                    CardNumber = model.CardNumber?.Replace(" ", string.Empty).Replace(" ", string.Empty),
                    ExpireMonth = model.ExpireMonth,
                    ExpireYear = model.ExpireYear,
                    CvvCode = model.CvvCode,
                    CardType = model.CardType,
                    Installment = model.Installment,
                    TotalAmount = model.TotalAmount,
                    OrderNumber = Guid.NewGuid().ToString(),
                    CurrencyIsoCode = "949",
                    LanguageIsoCode = "tr",
                    CustomerIpAddress = HttpContext.Connection.RemoteIpAddress.ToString()
                };

                //bank
                Bank bank = await _bankService.GetById(model.BankId.Value);
                gatewayRequest.BankName = Enum.Parse<BankNames>(bank.SystemName);

                //bank parameters
                System.Collections.Generic.List<BankParameter> bankParameters = await _bankService.GetBankParameters(bank.Id);
                gatewayRequest.BankParameters = bankParameters.ToDictionary(key => key.Key, value => value.Value);

                //create payment transaction
                PaymentTransaction payment = new PaymentTransaction
                {
                    OrderNumber = Guid.Parse(gatewayRequest.OrderNumber),
                    UserIpAddress = gatewayRequest.CustomerIpAddress,
                    UserAgent = HttpContext.Request.Headers[HeaderNames.UserAgent],
                    BankId = model.BankId.Value,
                    CardPrefix = gatewayRequest.CardNumber.Substring(0, 6),
                    CardHolderName = gatewayRequest.CardHolderName,
                    Installment = model.Installment,
                    TotalAmount = model.TotalAmount,
                    BankRequest = JsonConvert.SerializeObject(gatewayRequest)
                };

                //mark as created
                payment.MarkAsCreated();

                //insert payment transaction
                await _paymentService.Insert(payment);

                var responseModel = new
                {
                    GatewayUrl = new Uri($"{Request.GetHostUrl(false)}{Url.RouteUrl("Confirm", new { paymentId = payment.OrderNumber })}"),
                    Message = "Redirecting to gateway..."
                };

                return Ok(responseModel);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);

                return Ok(new { errorMessage = "İşlem sırasında bir hata oluştu." });
            }
        }

        public async Task<IActionResult> Confirm(Guid paymentId)
        {
            if (paymentId == Guid.Empty)
            {
                VerifyGatewayResult failModel = VerifyGatewayResult.Failed("Ödeme bilgisi geçersiz.");
                return View("Fail", failModel);
            }

            //get transaction by identifier
            PaymentTransaction payment = await _paymentService.GetByOrderNumber(paymentId);
            if (payment == null)
            {
                VerifyGatewayResult failModel = VerifyGatewayResult.Failed("Ödeme bilgisi geçersiz.");
                return View("Fail", failModel);
            }

            PaymentGatewayRequest bankRequest = JsonConvert.DeserializeObject<PaymentGatewayRequest>(payment.BankRequest);
            if (bankRequest == null)
            {
                VerifyGatewayResult failModel = VerifyGatewayResult.Failed("Ödeme bilgisi geçersiz.");
                return View("Fail", failModel);
            }

            if (!IPAddress.TryParse(bankRequest.CustomerIpAddress, out IPAddress ipAddress))
            {
                bankRequest.CustomerIpAddress = HttpContext.Connection.RemoteIpAddress.ToString();
            }

            if (bankRequest.CustomerIpAddress == "::1")
            {
                bankRequest.CustomerIpAddress = "127.0.0.1";
            }

            IPaymentProvider provider = _paymentProviderFactory.Create(bankRequest.BankName);

            //set callback url
            bankRequest.CallbackUrl = new Uri($"{Request.GetHostUrl(false)}{Url.RouteUrl("Callback", new { paymentId = payment.OrderNumber })}");

            //gateway request
            PaymentGatewayResult gatewayResult = await provider.ThreeDGatewayRequest(bankRequest);

            //check result status
            if (!gatewayResult.Success)
            {
                VerifyGatewayResult failModel = VerifyGatewayResult.Failed(gatewayResult.ErrorMessage);
                return View("Fail", failModel);
            }

            //html content
            if (gatewayResult.HtmlContent)
            {
                return View(model: gatewayResult.HtmlFormContent);
            }

            //create form submit with parameters
            string model = _paymentProviderFactory.CreatePaymentFormHtml(gatewayResult.Parameters, gatewayResult.GatewayUrl);
            return View(model: model);
        }

        [HttpPost]
        public async Task<IActionResult> GetInstallments([FromBody] InstallmentViewModel model)
        {
            //add cash option
            model.AddCashRate(model.TotalAmount);

            //get card prefix by prefix
            CreditCard creditCard = await _bankService.GetCreditCardByPrefix(model.Prefix, true);
            if (creditCard == null)
            {
                //get default bank
                Bank defaultBank = await _bankService.GetDefaultBank();

                if (defaultBank == null || !defaultBank.Active)
                {
                    return Ok(new { errorMessage = "Ödeme için aktif banka bulunamadı." });
                }

                model.BankId = defaultBank.Id;
                model.BankLogo = defaultBank.LogoPath;
                model.BankName = defaultBank.Name;

                return Ok(model);
            }

            //get bank by identifier
            Bank bank = await _bankService.GetById(creditCard.BankId);

            //get default bank
            if (bank == null || !bank.Active)
            {
                bank = await _bankService.GetDefaultBank();
            }

            if (bank == null || !bank.Active)
            {
                return Ok(new { errorMessage = "Ödeme için aktif banka bulunamadı." });
            }

            //prepare installment model
            foreach (CreditCardInstallment installment in creditCard.Installments)
            {
                decimal installmentAmount = model.TotalAmount;
                decimal installmentTotalAmount = installmentAmount;

                if (installment.InstallmentRate > 0)
                {
                    installmentTotalAmount = Math.Round(model.TotalAmount + ((model.TotalAmount * installment.InstallmentRate) / 100), 2, MidpointRounding.AwayFromZero);
                }

                installmentAmount = Math.Round(installmentTotalAmount / installment.Installment, 2, MidpointRounding.AwayFromZero);

                model.InstallmentRates.Add(new InstallmentViewModel.InstallmentRate
                {
                    Text = $"{installment.Installment} Taksit",
                    Installment = installment.Installment,
                    Rate = installment.InstallmentRate,
                    Amount = installmentAmount.ToString("N2"),
                    AmountValue = installmentAmount,
                    TotalAmount = installmentTotalAmount.ToString("N2"),
                    TotalAmountValue = installmentTotalAmount
                });
            }

            //set manufacturer card flag
            model.BankId = bank.Id;
            model.BankLogo = bank.LogoPath;
            model.BankName = bank.Name;

            return Ok(model);
        }

        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> Callback(Guid paymentId, IFormCollection form)
        {
            if (paymentId == Guid.Empty)
            {
                VerifyGatewayResult failModel = VerifyGatewayResult.Failed("Ödeme bilgisi geçersiz.");
                return View("Fail", failModel);
            }

            //get transaction by identifier
            PaymentTransaction payment = await _paymentService.GetByOrderNumber(paymentId);
            if (payment == null)
            {
                VerifyGatewayResult failModel = VerifyGatewayResult.Failed("Ödeme bilgisi geçersiz.");
                return View("Fail", failModel);
            }

            PaymentGatewayRequest bankRequest = JsonConvert.DeserializeObject<PaymentGatewayRequest>(payment.BankRequest);
            if (bankRequest == null)
            {
                VerifyGatewayResult failModel = VerifyGatewayResult.Failed("Ödeme bilgisi geçersiz.");
                return View("Fail", failModel);
            }

            //create provider
            IPaymentProvider provider = _paymentProviderFactory.Create(bankRequest.BankName);
            VerifyGatewayRequest verifyRequest = new VerifyGatewayRequest
            {
                BankName = bankRequest.BankName,
                BankParameters = bankRequest.BankParameters
            };

            VerifyGatewayResult verifyResult = await provider.VerifyGateway(verifyRequest, bankRequest, form);
            verifyResult.OrderNumber = bankRequest.OrderNumber;

            //save bank response
            payment.BankResponse = JsonConvert.SerializeObject(new
            {
                verifyResult,
                parameters = form.Keys.ToDictionary(key => key, value => form[value].ToString())
            });

            payment.TransactionNumber = verifyResult.TransactionId;
            payment.ReferenceNumber = verifyResult.ReferenceNumber;
            payment.BankResponse = verifyResult.Message;

            if (verifyResult.Installment > 1)
            {
                payment.Installment = verifyResult.Installment;
            }

            if (verifyResult.ExtraInstallment > 1)
            {
                payment.ExtraInstallment = verifyResult.ExtraInstallment;
            }

            if (verifyResult.Success)
            {
                //mark as paid
                payment.MarkAsPaid(DateTime.Now);
                await _paymentService.Update(payment);

                return View("Success", verifyResult);
            }

            //mark as not failed(it's mean error)
            payment.MarkAsFailed(verifyResult.ErrorMessage, $"{verifyResult.Message} - {verifyResult.ErrorCode}");

            //update payment transaction
            await _paymentService.Update(payment);

            return View("Fail", verifyResult);
        }

        public async Task<IActionResult> Completed([FromRoute(Name = "id")] Guid orderNumber)
        {
            //get order by order number
            PaymentTransaction payment = await _paymentService.GetByOrderNumber(orderNumber, includeBank: true);
            if (payment == null)
            {
                return RedirectToAction("Index", "Home");
            }

            //create completed view model
            CompletedViewModel model = new CompletedViewModel
            {
                OrderNumber = payment.OrderNumber,
                TransactionNumber = payment.TransactionNumber,
                ReferenceNumber = payment.ReferenceNumber,
                BankId = payment.BankId,
                BankName = payment.Bank?.Name,
                CardHolderName = payment.CardHolderName,
                Installment = payment.Installment,
                TotalAmount = payment.TotalAmount,
                PaidDate = payment.PaidDate,
                CreateDate = payment.CreateDate
            };

            return View(model);
        }
    }
}