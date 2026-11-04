using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace DominicShawNet;

public sealed class HealthFunction
{
    [Function("Health")]
    public IActionResult Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "healthz")] HttpRequest? request = null) =>
        new ContentResult
        {
            StatusCode = StatusCodes.Status200OK,
            ContentType = "text/plain",
            Content = "Healthy"
        };
}
