using Microsoft.AspNetCore.Mvc;

namespace ThreeDPayment.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            return RedirectToAction("Index", "Payment");
        }
    }
}