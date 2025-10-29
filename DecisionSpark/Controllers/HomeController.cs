using Microsoft.AspNetCore.Mvc;

namespace DecisionSpark.Controllers;

/// <summary>
/// Home controller for web UI testing interface
/// </summary>
public class HomeController : Controller
{
  private readonly ILogger<HomeController> _logger;
    private readonly IConfiguration _configuration;

    public HomeController(ILogger<HomeController> logger, IConfiguration configuration)
    {
  _logger = logger;
        _configuration = configuration;
    }

    /// <summary>
    /// Main testing interface for DecisionSpark API
    /// </summary>
    [HttpGet("/")]
    public IActionResult Index()
    {
        var apiKey = _configuration["DecisionEngine:ApiKey"] ?? "dev-api-key-change-in-production";
        ViewBag.ApiKey = apiKey;
   ViewBag.BaseUrl = $"{Request.Scheme}://{Request.Host}";
        return View();
    }

 /// <summary>
    /// Demo testing interface (no API key required)
    /// </summary>
    [HttpGet("/demo")]
    public IActionResult Demo()
    {
        ViewBag.BaseUrl = $"{Request.Scheme}://{Request.Host}";
        return View();
    }

    /// <summary>
    /// About page with API documentation
    /// </summary>
    [HttpGet("/about")]
    public IActionResult About()
    {
return View();
    }
}
