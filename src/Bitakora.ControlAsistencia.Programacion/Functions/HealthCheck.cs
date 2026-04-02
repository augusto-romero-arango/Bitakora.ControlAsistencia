using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace Bitakora.ControlAsistencia.Programacion.Functions;

public class HealthCheck
{
    [Function("health")]
    public HttpResponseData Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "health")] HttpRequestData req)
    {
        var response = req.CreateResponse(System.Net.HttpStatusCode.OK);
        response.WriteString("OK");
        return response;
    }
}
