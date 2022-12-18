/*
   Support: fsefacan@gmail.com
*/

using Microsoft.AspNetCore.Mvc;

namespace ThreeDPayment.Sample.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            return RedirectToAction("Index", "Payment");
        }
    }
}