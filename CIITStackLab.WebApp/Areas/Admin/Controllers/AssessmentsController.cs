using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CIITStackLab.WebApp.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin,Super User")]
public sealed class AssessmentsController : Controller
{
    [HttpGet]
    public IActionResult Index()
    {
        return View();
    }

    [HttpGet]
    public IActionResult Mcqs()
    {
        return View();
    }

    [HttpGet]
    public IActionResult InterviewQuestions()
    {
        return View();
    }

    [HttpGet]
    public IActionResult PracticePrograms()
    {
        return View();
    }
}
