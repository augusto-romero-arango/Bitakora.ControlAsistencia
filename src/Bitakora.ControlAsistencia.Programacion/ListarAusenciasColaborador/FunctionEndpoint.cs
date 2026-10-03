using System.Text.Json;
using System.Text.RegularExpressions;
using Bitakora.ControlAsistencia.Programacion.Entities;
using Cosmos.MultiTenancy;
using Marten;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.Programacion.ListarAusenciasColaborador;

// Function QUERY (MEF-ADR-0042) via (b1): hidrata AusenciasColaborador en vivo (MEF-ADR-0035).
public partial class FunctionEndpoint(IDocumentStore store, ITenantContext tenantContext)
{
    [GeneratedRegex(@"\A[A-Za-z0-9._~-]+\z")]
    private static partial Regex CodigoUrlSafe();

    [Function("ListarAusenciasColaborador")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "query", Route = "programacion/colaboradores/{codigo}/ausencias")]
        HttpRequest req,
        string codigo,
        CancellationToken ct)
    {
        if (!req.HasJsonContentType())
            return new ObjectResult("La query exige Content-Type: application/json")
            { StatusCode = StatusCodes.Status415UnsupportedMediaType };

        if (!CodigoUrlSafe().IsMatch(codigo ?? string.Empty))
            return new BadRequestObjectResult(
                "El codigo del colaborador solo admite letras sin tilde, digitos y los caracteres - . _ ~");

        FiltroListarAusenciasColaborador? filtro;
        try
        {
            filtro = await req.ReadFromJsonAsync<FiltroListarAusenciasColaborador>(ct);
        }
        catch (JsonException)
        {
            return new BadRequestObjectResult("El body de la query no es un JSON valido");
        }

        if (filtro is null)
            return new BadRequestObjectResult("El body de la query es obligatorio");

        if (filtro.Desde is not { } desde || filtro.Hasta is not { } hasta)
            return new ObjectResult("Desde y Hasta son obligatorios")
            { StatusCode = StatusCodes.Status422UnprocessableEntity };

        if (desde > hasta)
            return new ObjectResult("Desde no puede ser posterior a Hasta")
            { StatusCode = StatusCodes.Status422UnprocessableEntity };

        await using var session = store.QuerySession(tenantContext.TenantId);
        var ausencias = await session.Events.AggregateStreamAsync<AusenciasColaborador>(
            AusenciasColaborador.ComputarStreamId(codigo!), token: ct);

        return new OkObjectResult(ausencias?.ListarAusenciasVigentes(desde, hasta) ?? []);
    }
}
