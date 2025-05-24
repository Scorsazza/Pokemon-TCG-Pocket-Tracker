using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/test123")]
public class TestController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok("Test endpoint is working!");
}
