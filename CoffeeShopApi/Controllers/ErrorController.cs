using Microsoft.AspNetCore.Mvc;

namespace CoffeeShopApi.Controllers
{
    [ApiController]
    public class ErrorController : ControllerBase
    {
        [Route("/Home/Error")]
        [HttpGet]
        public IActionResult Error()
        {
            return StatusCode(500, new { message = "An error occurred processing your request." });
        }
    }
}
