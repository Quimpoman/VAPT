using EVOLVE_VAPT.Models;
using EVOLVE_VAPT.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EVOLVE_VAPT.Controllers
{
    [Authorize(Policy = "CanViewReports")]
    public class ReportController : Controller
    {
        private readonly EVOLVE_VAPTContext
            _context;

        private readonly ReportService
            _reportService;

        private readonly UserManager<IdentityUser>
            _userManager;


        public ReportController(
    EVOLVE_VAPTContext context,
    ReportService reportService,
    UserManager<IdentityUser> userManager)
        {
            _context =
                context;

            _reportService =
                reportService;

            _userManager =
                userManager;
        }


        public async Task<IActionResult> Index()
        {
            var assessments =
                await _context.vwGet_AssessmentData
                    .AsNoTracking()

                    .OrderByDescending(x =>
                        x.AssessmentID)

                    .ToListAsync();


            return View(assessments);
        }


        public async Task<IActionResult> Assessment(
            int id)
        {
            var model =
                await _reportService
                    .GetAssessmentReportAsync(id);


            if (model == null)
            {
                return NotFound();
            }


            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> PrintAssessment(int id)
        {
            var model =
                await _reportService
                    .GetAssessmentReportAsync(id);


            if (model == null)
            {
                return NotFound();
            }


            var user =
                await _userManager.GetUserAsync(User);


            model.GeneratedDate =
                DateTime.Now;

            model.GeneratedBy =
                user?.Email
                ?? user?.UserName
                ?? "EVOLVE_VAPT";


            return View(model);
        }
    }
}