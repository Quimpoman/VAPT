using EVOLVE_VAPT.Services;
using EVOLVE_VAPT.ViewModels.AuditTrail;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EVOLVE_VAPT.Controllers
{
    [Authorize(
        Policy = "CanViewAuditTrail")]
    public class AuditTrailController
        : Controller
    {
        private readonly AuditTrailQueryService
            _auditTrailQueryService;


        public AuditTrailController(
            AuditTrailQueryService
                auditTrailQueryService)
        {
            _auditTrailQueryService =
                auditTrailQueryService;
        }


        [HttpGet]
        public async Task<IActionResult> Index(
            AuditTrailIndexViewModel model)
        {
            var result =
                await _auditTrailQueryService
                    .GetAuditTrailAsync(
                        model);


            return View(result);
        }
        [HttpGet]
        public async Task<IActionResult> Details(long id)
        {
            var audit =
                await _auditTrailQueryService
                    .GetAuditTrailDetailsAsync(
                        id);


            if (audit == null)
            {
                return NotFound();
            }


            return View(audit);
        }
    }
}