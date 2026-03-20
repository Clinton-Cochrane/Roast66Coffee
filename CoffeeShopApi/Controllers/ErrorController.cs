using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace CoffeeShopApi.Controllers
{
    [ApiController]
    public class ErrorController : ControllerBase
    {
        [Route("/Home/Error")]
        [AcceptVerbs("GET", "POST")]
        public IActionResult Error([FromServices] ILogger<ErrorController> logger)
        {
            var feature = HttpContext.Features.Get<IExceptionHandlerPathFeature>();
            if (feature?.Error != null)
            {
                logger.LogError(feature.Error, "Unhandled exception: {Message}", feature.Error.Message);
            }
            return StatusCode(500, new { message = "An error occurred processing your request." });
        }
    }
}
