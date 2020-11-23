using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using System;
using System.Linq;
using System.Collections;
using System.Text.Json;
using System.Threading.Tasks;
using ThreeDPayment.Models;
using ThreeDPayment.Sample.Models;

namespace ThreeDPayment.Controllers
{
    public class PaymentController : Controller
    {
        private const string PaymentSessionName = "PaymentInfo";
        private const string PaymentResultSessionName = "PaymentResult";

        private readonly IHtmlHelper _htmlHelper;
        private readonly IPaymentProviderFactory _paymentProviderFactory;
        private readonly ICreditCardResolver _creditCardResolver;

        public PaymentController(IHtmlHelper htmlHelper,
            IPaymentProviderFactory paymentProviderFactory,
            ICreditCardResolver creditCardResolver)
        {
            _htmlHelper = htmlHelper;
            _paymentProviderFactory = paymentProviderFactory;
            _creditCardResolver = creditCardResolver;
        }

        public IActionResult Index()
        {
            var model = new PaymentViewModel
            {
                Banks = _htmlHelper.GetEnumSelectList<BankNames>().ToList()
            };
            model.Banks.Insert(0, new SelectListItem("Seçiniz", string.Empty));

            return View(model);
        }

        [HttpPost]
        public IActionResult Index(PaymentViewModel model)
        {
            if (ModelState.IsValid)
            {
                HttpContext.Session.Set(PaymentSessionName, JsonSerializer.SerializeToUtf8Bytes(model));
                return RedirectToAction(nameof(ThreeDGate));
            }

            ModelState.AddModelError(string.Empty, "Lütfen bilgileri kontrol edin.");
            return View(model);
        }

        public async Task<IActionResult> ThreeDGate()
        {
            if (!HttpContext.Session.TryGetValue(PaymentSessionName, out byte[] paymentInfo))
                return RedirectToAction(nameof(Index));

            var paymentModel = JsonSerializer.Deserialize<PaymentViewModel>(paymentInfo);
            if (paymentModel == null)
                return RedirectToAction(nameof(Index));

            var bankName = _creditCardResolver.GetBankName(paymentModel.CardNumber);
            var paymentProvider = _paymentProviderFactory.Create(bankName);
            var paymentGatewayResult = await paymentProvider.ThreeDGatewayRequest(new PaymentGatewayRequest
            {
                CardHolderName = paymentModel.CardHolderName,
                CardNumber = paymentModel.CardNumber,
                ExpireMonth = paymentModel.ExpireMonth,
                ExpireYear = paymentModel.ExpireYear,
                CvvCode = paymentModel.CvvCode,
                Installment = paymentModel.Installment,
                TotalAmount = 1.00m,
                CustomerIpAddress = HttpContext.Connection.RemoteIpAddress,
                CurrencyIsoCode = "949",
                LanguageIsoCode = "tr",
                CardType = "1",
                OrderNumber = Guid.NewGuid().ToString(),
                BankName = paymentModel.SelectedBank,
                CallbackUrl = new Uri($"{Request.Scheme}://{Request.Host}{Request.PathBase}" + Url.Action("Callback", "Payment")),
                BankParameters = paymentProvider.TestParameters //canli ortam bilgileri veritabanından gelecek
            });

            var paymentForm = _paymentProviderFactory.CreatePaymentFormHtml(paymentGatewayResult.Parameters, paymentGatewayResult.GatewayUrl);
            return View(model: paymentForm);
        }

        public async Task<IActionResult> Callback(IFormCollection form)
        {
            if (HttpContext.Session.TryGetValue(PaymentSessionName, out byte[] paymentInfo))
                return RedirectToAction(nameof(Index));

            var paymentModel = JsonSerializer.Deserialize<PaymentViewModel>(paymentInfo);
            if (paymentModel == null)
                return RedirectToAction(nameof(Index));

            //clear payment info session
            HttpContext.Session.Remove(PaymentSessionName);

            var paymentProvider = _paymentProviderFactory.Create(paymentModel.SelectedBank);
            var verifyGatewayResult = await paymentProvider.VerifyGateway(new VerifyGatewayRequest
            {

            }, form);
            HttpContext.Session.Set(PaymentResultSessionName, JsonSerializer.SerializeToUtf8Bytes(verifyGatewayResult));

            if (verifyGatewayResult.Success)
            {
                return RedirectToAction(nameof(Success));
            }

            return RedirectToAction(nameof(Fail));
        }

        public IActionResult Success()
        {
            if (HttpContext.Session.TryGetValue(PaymentResultSessionName, out byte[] result))
                return RedirectToAction(nameof(Index));

            var paymentResult = JsonSerializer.Deserialize<VerifyGatewayResult>(result);
            if (paymentResult == null)
                return RedirectToAction(nameof(Index));

            return View(paymentResult);
        }

        public IActionResult Fail()
        {
            if (HttpContext.Session.TryGetValue(PaymentResultSessionName, out byte[] result))
                return RedirectToAction(nameof(Index));

            var paymentResult = JsonSerializer.Deserialize<VerifyGatewayResult>(result);
            if (paymentResult == null)
                return RedirectToAction(nameof(Index));

            return View(paymentResult);
        }
    }
}