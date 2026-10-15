using EVOLVE_VAPT.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EVOLVE_VAPT.Controllers
{
    [Authorize(Policy = "CanManageAdministration")]

    public class AdministrationController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}