using Bitakora.ControlAsistencia.Colaboradores.DomainEvents;
using Bitakora.ControlAsistencia.Colaboradores.Entities;
using Bitakora.ControlAsistencia.ReadModels.Colaboradores;
using Cosmos.MultiTenancy;
using Marten;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.Colaboradores.ObtenerFichaColaborador;

// Issue #356: Function GET del read model FichaColaborador (via (a) proyeccion materializada,
// skills/projections/naming.md, MEF-ADR-0006 enmienda #363). Feature folder sin sufijo Function, un
// namespace por query (skills/projections/read-apis.md): esta clase FunctionEndpoint no colisiona
// con ninguna otra del mismo ensamblado porque cada query vive en su propio namespace.
//
// La ruta recibe tipoIdentificacion/numero como los componentes tipados de la clave natural
// compuesta -- la clave se reconstruye con ColaboradorAggregateRoot.ComputarStreamId, nunca con una
// concatenacion propia del endpoint (MEF-ADR-0037). tipoIdentificacion invalido (fuera de la lista
// cerrada PILA) o numero vacio/whitespace -> 400 explicito (TipoIdentificacion.Desde/
// Identificacion.Crear rechazan con ArgumentException, capturada aqui como unico punto de
// traduccion a 400).
//
// CA-6: consulta puntual, INCLUYE no-vigentes (sin filtro de vigencia -- a diferencia del listado,
// que es responsabilidad del issue hermano). 404 sin body cuando la ficha no existe.
public class FunctionEndpoint(IDocumentStore store, ITenantResolver tenantResolver)
{
    [Function("ObtenerFichaColaborador")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "colaboradores/fichas/{tipoIdentificacion}/{numero}")]
        HttpRequest req,
        string tipoIdentificacion,
        string numero,
        CancellationToken ct)
    {
        Identificacion identificacionTipada;
        try
        {
            var tipo = TipoIdentificacion.Desde(tipoIdentificacion);
            identificacionTipada = Identificacion.Crear(tipo, numero);
        }
        catch (ArgumentException)
        {
            return new BadRequestObjectResult(
                "El tipoIdentificacion o el numero de la ruta son invalidos");
        }

        var streamKey = ColaboradorAggregateRoot.ComputarStreamId(identificacionTipada);

        // CA-6: la QuerySession se abre SIEMPRE acotada al tenant que resuelve ITenantResolver --
        // nunca a un tenant id que llegara por ruta o query string (mitigacion estructural contra
        // BOLA/IDOR, MEF-ADR-0028/skills/projections/read-apis.md). tipoIdentificacion/numero SI
        // vienen de la ruta: son el recurso, no el tenant.
        await using var session = store.QuerySession(tenantResolver.TenantId);
        var ficha = await session.LoadAsync<FichaColaborador>(streamKey, ct);

        if (ficha is null)
            return new NotFoundResult();

        return new OkObjectResult(FichaColaboradorRespuesta.DesdeVista(ficha));
    }
}

// Issue #356 CA-6: DTO de respuesta HTTP, excepcion bajo Rule of Three (MEF-ADR-0018,
// skills/projections/read-apis.md "El GET serializa la vista; el DTO de respuesta es excepcion"):
// el unico proposito de este tipo es ocultar el centinela de vigencia abierta (9999-12-31), que es
// estructura INTERNA de filtrado/indexacion del read model y jamas debe salir por la API (CA-6,
// "el centinela jamas aparece en la API"). Vive en el namespace del endpoint, no en ReadModels: el
// read model no conoce su presentacion HTTP.
public sealed record FichaColaboradorRespuesta(
    string Id,
    string NombreCompleto,
    string CodigoColaborador,
    DateOnly VigenteDesde,
    DateOnly? VigenteHasta,
    IReadOnlyList<EtiquetaFicha> Etiquetas,
    IReadOnlyDictionary<string, string> EtiquetasNormalizadas)
{
    private static readonly DateOnly CentinelaVigenciaAbierta = new(9999, 12, 31);

    public static FichaColaboradorRespuesta DesdeVista(FichaColaborador ficha) =>
        new(
            ficha.Id,
            ficha.NombreCompleto,
            ficha.CodigoColaborador,
            ficha.VigenteDesde,
            ficha.VigenteHasta == CentinelaVigenciaAbierta ? null : ficha.VigenteHasta,
            ficha.Etiquetas,
            ficha.EtiquetasNormalizadas);
}
