using Bitakora.ControlAsistencia.Colaboradores.Entities;
using Bitakora.ControlAsistencia.Colaboradores.Infraestructura;
using Bitakora.ControlAsistencia.ReadModels.Colaboradores;
using Cosmos.MultiTenancy;
using Marten;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.Colaboradores.ObtenerFichaColaborador;

// Issue #356 (creacion): Function GET del read model FichaColaborador (via (a) proyeccion
// materializada, skills/projections/naming.md, MEF-ADR-0006 enmienda #363). Feature folder sin
// sufijo Function, un namespace por query (skills/projections/read-apis.md): esta clase
// FunctionEndpoint no colisiona con ninguna otra del mismo ensamblado porque cada query vive en su
// propio namespace.
//
// Issue #386 (esta revision): alinea el GET puntual a la forma unica de identidad en la ruta que ya
// usan los comandos del ciclo de vida del colaborador (#376/#377): {id} = Identificacion.ToString()
// ("CC-79543210"), la misma llave que devuelve FichaColaborador.Id -- cierra el round-trip completo
// del cliente (consulta y comando comparten forma de id, MEF-ADR-0037: punto unico de conversion de
// la identidad de stream via Identificacion.Parsear, nunca partiendo la llave por su cuenta).
// Reemplaza la ruta de dos segmentos {tipoIdentificacion}/{numero} (issue #356): la ruta vieja deja
// de existir (CA-5). Rename de un endpoint desplegado discutido con el humano (MEF-ADR-0043
// seccion 7, por analogia).
//
// CA-6 (issue #356, sin cambios): consulta puntual, INCLUYE no-vigentes (sin filtro de vigencia --
// a diferencia del listado, que es responsabilidad del issue hermano). 404 sin body cuando la ficha
// no existe.
//
// CA-3 (MEF-ADR-0037 seccion 2, precedente CorregirNombresFunction.FunctionEndpoint -- el unico
// endpoint que hoy parsea un {id} de ruta; AsignarEtiquetaFunction todavia recibe la identificacion
// en el body): Identificacion.Parsear es el UNICO punto de conversion
// string->Identificacion -- nunca se parte el {id} de ruta a mano. Un ArgumentException (sin guion,
// tipo fuera de la lista cerrada PILA, numero vacio tras la limpieza) se traduce aqui, y solo aqui,
// a 400 explicito, antes de tocar Marten.
public class FunctionEndpoint(IDocumentStore store, ITenantResolver tenantResolver)
{
    [Function("ObtenerFichaColaborador")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "colaboradores/fichas/{id}")]
        HttpRequest req,
        string id,
        CancellationToken ct)
    {
        if (!IdentificacionDeRuta.TryParsear(id, out var identificacion, out var errorDeId))
            return errorDeId;

        var streamKey = ColaboradorAggregateRoot.ComputarStreamId(identificacion);

        // CA-6: la QuerySession se abre SIEMPRE acotada al tenant que resuelve ITenantResolver --
        // nunca a un tenant id que llegara por ruta o query string (mitigacion estructural contra
        // BOLA/IDOR, MEF-ADR-0028/skills/projections/read-apis.md). El {id} de ruta es el recurso,
        // no el tenant.
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
// Issue #519 CA-3: CodigoSede se copia tal cual desde la vista -- a diferencia de VigenteHasta no
// hay centinela que ocultar, asi que no requiere traduccion. Va al final del record (opcional) para
// no correr las posiciones de los llamadores existentes.
public sealed record FichaColaboradorRespuesta(
    string Id,
    string NombreCompleto,
    string CodigoColaborador,
    DateOnly VigenteDesde,
    DateOnly? VigenteHasta,
    IReadOnlyList<EtiquetaFicha> Etiquetas,
    IReadOnlyDictionary<string, string> EtiquetasNormalizadas,
    string? CodigoSede = null)
{
    // El centinela se lee de la propia vista (FichaColaborador.CentinelaVigenciaAbierta), nunca de
    // un literal repetido aqui: quien lo escribe es el worker, en otro proceso, y ReadModels es el
    // unico ensamblado que ambos ven (cuarta isla, MEF-ADR-0041 decision 2). Con dos literales, un
    // cambio de un solo lado compila, pasa todos los tests unitarios y filtra 9999-12-31 por la API.
    public static FichaColaboradorRespuesta DesdeVista(FichaColaborador ficha) =>
        new(
            ficha.Id,
            ficha.NombreCompleto,
            ficha.CodigoColaborador,
            ficha.VigenteDesde,
            ficha.VigenteHasta == FichaColaborador.CentinelaVigenciaAbierta ? null : ficha.VigenteHasta,
            ficha.Etiquetas,
            ficha.EtiquetasNormalizadas,
            ficha.CodigoSede);
}
