// Validacion del borde HTTP del endpoint QUERY ListarDirectorioColaboradores (MEF-ADR-0042, RFC
// 10008). Estos casos cortocircuitan ANTES de abrir la QuerySession -- store y tenantResolver se
// pasan nulos a proposito: si un cambio futuro moviera la validacion DESPUES de tocar Marten, estos
// tests se pondrian rojos por la razon correcta.
//
// La clasificacion completa-vs-numero de "identificaciones" y el containment por tokens de "nombre"
// son privados del endpoint y exigen Marten real para observarse -- los cubre el smoke test (CA-6),
// no este archivo.

using AwesomeAssertions;
using System.Text;
using System.Text.Json;
using Bitakora.ControlAsistencia.Colaboradores.ListarDirectorioColaboradores;
using Bitakora.ControlAsistencia.ReadModels.Colaboradores;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Bitakora.ControlAsistencia.Colaboradores.Tests.ListarDirectorioColaboradores;

public class FunctionEndpointTests
{
    private static FunctionEndpoint Endpoint() => new(store: null!, tenantResolver: null!);

    private static HttpRequest FakeHttpRequest(string? contentType, string? body)
    {
        var context = new DefaultHttpContext();

        if (contentType is not null)
            context.Request.ContentType = contentType;

        if (body is not null)
        {
            var bytes = Encoding.UTF8.GetBytes(body);
            context.Request.Body = new MemoryStream(bytes);
            context.Request.ContentLength = bytes.Length;
        }

        return context.Request;
    }

    // --- 415: Content-Type ausente o distinto de application/json (RFC 10008 seccion 2) ---

    [Fact]
    public async Task ListarDirectorioColaboradores_Retorna415_CuandoElContentTypeNoEsJson()
    {
        var request = FakeHttpRequest(contentType: "text/plain", body: "{}");

        var resultado = await Endpoint().Run(request, CancellationToken.None);

        var objectResult = resultado.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status415UnsupportedMediaType);
        objectResult.Value.Should().BeOfType<string>();
    }

    [Fact]
    public async Task ListarDirectorioColaboradores_Retorna415_CuandoElContentTypeEstaAusente()
    {
        var request = FakeHttpRequest(contentType: null, body: "{}");

        var resultado = await Endpoint().Run(request, CancellationToken.None);

        var objectResult = resultado.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status415UnsupportedMediaType);
    }

    // --- 400: body ausente, JSON invalido o literal "null" (RFC 10008 seccion 2.1) ---

    [Fact]
    public async Task ListarDirectorioColaboradores_Retorna400_CuandoElBodyEstaAusente()
    {
        var request = FakeHttpRequest(contentType: "application/json", body: null);

        var resultado = await Endpoint().Run(request, CancellationToken.None);

        var badRequest = resultado.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequest.Value.Should().BeOfType<string>();
    }

    [Fact]
    public async Task ListarDirectorioColaboradores_Retorna400_CuandoElBodyNoEsJsonValido()
    {
        var request = FakeHttpRequest(contentType: "application/json", body: "{ esto no es json");

        var resultado = await Endpoint().Run(request, CancellationToken.None);

        resultado.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task ListarDirectorioColaboradores_Retorna400_CuandoElBodyEsElLiteralJsonNull()
    {
        var request = FakeHttpRequest(contentType: "application/json", body: "null");

        var resultado = await Endpoint().Run(request, CancellationToken.None);

        resultado.Should().BeOfType<BadRequestObjectResult>();
    }

    // --- 422: JSON valido pero no procesable (CA-1) ---

    [Fact]
    public async Task ListarDirectorioColaboradores_Retorna422_CuandoFaltanIdentificacionesYNombreALaVez()
    {
        var request = FakeHttpRequest(contentType: "application/json", body: "{}");

        var resultado = await Endpoint().Run(request, CancellationToken.None);

        var unprocessable = resultado.Should().BeOfType<ObjectResult>().Subject;
        unprocessable.StatusCode.Should().Be(StatusCodes.Status422UnprocessableEntity);
        unprocessable.Value.Should().BeOfType<string>();
    }

    [Fact]
    public async Task ListarDirectorioColaboradores_Retorna422_CuandoIdentificacionesVieneVacia()
    {
        var request = FakeHttpRequest(
            contentType: "application/json",
            body: """{"identificaciones":[]}""");

        var resultado = await Endpoint().Run(request, CancellationToken.None);

        var unprocessable = resultado.Should().BeOfType<ObjectResult>().Subject;
        unprocessable.StatusCode.Should().Be(StatusCodes.Status422UnprocessableEntity);
    }

    [Fact]
    public async Task ListarDirectorioColaboradores_Retorna422_CuandoIdentificacionesTraeUnValorNulo()
    {
        // El body es entrada del cliente: STJ acepta un elemento null dentro del array pese a la
        // anotacion IReadOnlyList<string> -- mismo gotcha que FiltroEtiqueta en el hermano.
        var request = FakeHttpRequest(
            contentType: "application/json",
            body: """{"identificaciones":[null]}""");

        var resultado = await Endpoint().Run(request, CancellationToken.None);

        var unprocessable = resultado.Should().BeOfType<ObjectResult>().Subject;
        unprocessable.StatusCode.Should().Be(StatusCodes.Status422UnprocessableEntity);
        unprocessable.Value.Should().BeOfType<string>();
    }

    [Fact]
    public async Task ListarDirectorioColaboradores_Retorna422_CuandoIdentificacionesTraeUnValorEnBlanco()
    {
        var request = FakeHttpRequest(
            contentType: "application/json",
            body: """{"identificaciones":["   "]}""");

        var resultado = await Endpoint().Run(request, CancellationToken.None);

        var unprocessable = resultado.Should().BeOfType<ObjectResult>().Subject;
        unprocessable.StatusCode.Should().Be(StatusCodes.Status422UnprocessableEntity);
    }

    [Fact]
    public async Task ListarDirectorioColaboradores_Retorna422_CuandoIdentificacionesTraeMasDe200Valores()
    {
        var masDe200 = Enumerable.Range(1, 201).Select(i => $"CC-{i:D8}").ToList();
        var body = JsonSerializer.Serialize(new { identificaciones = masDe200 });
        var request = FakeHttpRequest(contentType: "application/json", body: body);

        var resultado = await Endpoint().Run(request, CancellationToken.None);

        var unprocessable = resultado.Should().BeOfType<ObjectResult>().Subject;
        unprocessable.StatusCode.Should().Be(StatusCodes.Status422UnprocessableEntity);
        unprocessable.Value.Should().BeOfType<string>();
    }

    [Fact]
    public async Task ListarDirectorioColaboradores_Retorna422_CuandoNombreVienePresenteEnBlanco()
    {
        var request = FakeHttpRequest(
            contentType: "application/json",
            body: """{"nombre":"   "}""");

        var resultado = await Endpoint().Run(request, CancellationToken.None);

        var unprocessable = resultado.Should().BeOfType<ObjectResult>().Subject;
        unprocessable.StatusCode.Should().Be(StatusCodes.Status422UnprocessableEntity);
        unprocessable.Value.Should().BeOfType<string>();
    }

    [Fact]
    public async Task ListarDirectorioColaboradores_Retorna422_CuandoNombreSoloTraePuntuacion()
    {
        // "..." no produce ningun token. Sin este 422 el filtro seria un containment contra un array
        // jsonb vacio, que esta contenido en TODA fila: el endpoint devolveria el directorio completo
        // justo cuando el cliente pidio buscar por nombre (verificado contra Postgres 16).
        var request = FakeHttpRequest(
            contentType: "application/json",
            body: """{"nombre":"..."}""");

        var resultado = await Endpoint().Run(request, CancellationToken.None);

        var unprocessable = resultado.Should().BeOfType<ObjectResult>().Subject;
        unprocessable.StatusCode.Should().Be(StatusCodes.Status422UnprocessableEntity);
        unprocessable.Value.Should().BeOfType<string>();
    }

    [Fact]
    public async Task ListarDirectorioColaboradores_Retorna422_CuandoElCursorTraeUnSoloCampo()
    {
        // Cursor con NombreCompleto pero sin Identificacion (el otro campo del cursor keyset) --
        // nombre valido presente para que la rama ejercitada sea especificamente la del cursor.
        var request = FakeHttpRequest(
            contentType: "application/json",
            body: """{"nombre":"bermudez","cursor":{"nombreCompleto":"Ana Torres"}}""");

        var resultado = await Endpoint().Run(request, CancellationToken.None);

        var unprocessable = resultado.Should().BeOfType<ObjectResult>().Subject;
        unprocessable.StatusCode.Should().Be(StatusCodes.Status422UnprocessableEntity);
    }

    // --- Forma de la respuesta (DTO puro, sin Marten -- CA-4) ---

    [Fact]
    public void DesdeVista_MapeaLosOchoCampos_CuandoLaVinculacionEstaTerminada()
    {
        var vista = new DirectorioColaborador(
            Id: "CC-79879078",
            TipoDocumento: "CC",
            NumeroDocumento: "79879078",
            NombreCompleto: "Juan Pablo Bermudez",
            TokensNombre: ["juan", "pablo", "bermudez"],
            CodigoColaborador: "COL-001",
            VigenteDesde: new DateOnly(2024, 1, 15),
            VigenteHasta: new DateOnly(2025, 6, 30),
            CodigoSede: "SEDE-BOG");

        var respuesta = DirectorioColaboradorRespuesta.DesdeVista(vista);

        // Oraculo independiente armado a mano (MEF-ADR-0002): nunca derivado de la logica del SUT.
        respuesta.Should().Be(new DirectorioColaboradorRespuesta(
            Identificacion: "CC-79879078",
            TipoDocumento: "CC",
            NumeroDocumento: "79879078",
            NombreCompleto: "Juan Pablo Bermudez",
            CodigoColaborador: "COL-001",
            CodigoSede: "SEDE-BOG",
            VigenteDesde: new DateOnly(2024, 1, 15),
            VigenteHasta: new DateOnly(2025, 6, 30)));
    }

    [Fact]
    public void DesdeVista_TraduceElCentinelaDeVigenciaAbierta_ANuloYCodigoSedeNuloSiNoTiene()
    {
        var vista = new DirectorioColaborador(
            Id: "CE-12345678",
            TipoDocumento: "CE",
            NumeroDocumento: "12345678",
            NombreCompleto: "Ana Torres",
            TokensNombre: ["ana", "torres"],
            CodigoColaborador: "COL-002",
            VigenteDesde: new DateOnly(2026, 1, 1),
            VigenteHasta: DirectorioColaborador.CentinelaVigenciaAbierta,
            CodigoSede: null);

        var respuesta = DirectorioColaboradorRespuesta.DesdeVista(vista);

        // El centinela jamas sale por la API -- se lee de DirectorioColaborador
        // .CentinelaVigenciaAbierta, nunca de un literal repetido aqui (mismo criterio que
        // FichaColaboradorRespuesta.DesdeVista).
        respuesta.Should().Be(new DirectorioColaboradorRespuesta(
            Identificacion: "CE-12345678",
            TipoDocumento: "CE",
            NumeroDocumento: "12345678",
            NombreCompleto: "Ana Torres",
            CodigoColaborador: "COL-002",
            CodigoSede: null,
            VigenteDesde: new DateOnly(2026, 1, 1),
            VigenteHasta: null));
    }
}
