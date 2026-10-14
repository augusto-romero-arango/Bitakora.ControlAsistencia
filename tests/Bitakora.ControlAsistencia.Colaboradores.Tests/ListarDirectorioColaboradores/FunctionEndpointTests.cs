// Validacion del borde HTTP del endpoint QUERY ListarDirectorioColaboradores (MEF-ADR-0042, RFC
// 10008). Estos casos cortocircuitan ANTES de abrir la QuerySession -- store y tenantContext se
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
    private static FunctionEndpoint Endpoint() => new(store: null!, tenantContext: null!);

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
    public async Task ListarDirectorioColaboradores_NoRechazaPorCantidad_CuandoIdentificacionesTraeMasDe200Valores()
    {
        var masDe200 = Enumerable.Range(1, 250).Select(i => $"CC-{i:D8}").ToList();
        var body = JsonSerializer.Serialize(new { identificaciones = masDe200 });
        var request = FakeHttpRequest(contentType: "application/json", body: body);

        var act = async () => await Endpoint().Run(request, CancellationToken.None);

        // store nulo: superar la validacion del borde equivale a llegar a la QuerySession.
        await act.Should().ThrowExactlyAsync<NullReferenceException>();
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

    // Issue #860 CA-3: JornadaId viaja tal cual desde la vista, null incluido (sin centinela).
    [Fact]
    public void DesdeVista_CopiaJornadaId_CuandoElColaboradorTieneJornadaAsignada()
    {
        var jornadaId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        var respuesta = DirectorioColaboradorRespuesta.DesdeVista(VistaConJornada(jornadaId));

        respuesta.JornadaId.Should().Be(jornadaId);
    }

    [Fact]
    public void DesdeVista_DejaJornadaIdNulo_CuandoElColaboradorNoTieneJornadaAsignada()
    {
        var respuesta = DirectorioColaboradorRespuesta.DesdeVista(VistaConJornada(null));

        respuesta.JornadaId.Should().BeNull();
    }

    private static DirectorioColaborador VistaConJornada(Guid? jornadaId) => new(
        Id: "CC-79879078",
        TipoDocumento: "CC",
        NumeroDocumento: "79879078",
        NombreCompleto: "Juan Pablo Bermudez",
        TokensNombre: ["juan", "pablo", "bermudez"],
        CodigoColaborador: "COL-001",
        VigenteDesde: new DateOnly(2024, 1, 15),
        VigenteHasta: new DateOnly(2025, 6, 30),
        CodigoSede: "SEDE-BOG",
        JornadaId: jornadaId);


    // --- CA-4: entrada invalida del sobre unico (CA-ADR-0039) -> 422 ---

    [Fact]
    public async Task ListarDirectorioColaboradores_Retorna422_CuandoTakeEsCero()
    {
        var request = FakeHttpRequest("application/json", """{"nombre":"bermudez","take":0}""");

        var resultado = await Endpoint().Run(request, CancellationToken.None);

        resultado.Should().BeOfType<ObjectResult>().Which.StatusCode
            .Should().Be(StatusCodes.Status422UnprocessableEntity);
    }

    [Fact]
    public async Task ListarDirectorioColaboradores_Retorna422_CuandoTakeSuperaElMaximoDe200ConIdentificaciones()
    {
        var request = FakeHttpRequest("application/json", """{"identificaciones":["CC-1"],"take":201}""");

        var resultado = await Endpoint().Run(request, CancellationToken.None);

        resultado.Should().BeOfType<ObjectResult>().Which.StatusCode
            .Should().Be(StatusCodes.Status422UnprocessableEntity);
    }

    [Fact]
    public async Task ListarDirectorioColaboradores_Retorna422_CuandoTakeSuperaElMaximoDe200PorNombre()
    {
        var request = FakeHttpRequest("application/json", """{"nombre":"bermudez","take":201}""");

        var resultado = await Endpoint().Run(request, CancellationToken.None);

        resultado.Should().BeOfType<ObjectResult>().Which.StatusCode
            .Should().Be(StatusCodes.Status422UnprocessableEntity);
    }

    [Fact]
    public async Task ListarDirectorioColaboradores_Retorna422_CuandoElCursorEsIndescifrable()
    {
        var request = FakeHttpRequest("application/json", """{"nombre":"bermudez","cursor":"esto-no-es-un-cursor"}""");

        var resultado = await Endpoint().Run(request, CancellationToken.None);

        resultado.Should().BeOfType<ObjectResult>().Which.StatusCode
            .Should().Be(StatusCodes.Status422UnprocessableEntity);
    }

    [Fact]
    public async Task ListarDirectorioColaboradores_Retorna422_CuandoElCursorTraeLaFormaVieja()
    {
        var request = FakeHttpRequest(
            "application/json",
            """{"nombre":"bermudez","cursor":{"nombreCompleto":"Ana Torres","identificacion":"CC-1"}}""");

        var resultado = await Endpoint().Run(request, CancellationToken.None);

        resultado.Should().BeOfType<ObjectResult>().Which.StatusCode
            .Should().Be(StatusCodes.Status422UnprocessableEntity);
    }

    [Fact]
    public async Task ListarDirectorioColaboradores_NoRechazaElFiltro_CuandoTakeEsElMaximoDe200ConIdentificaciones()
    {
        var request = FakeHttpRequest("application/json", """{"identificaciones":["CC-1"],"take":200}""");

        var act = async () => await Endpoint().Run(request, CancellationToken.None);

        await act.Should().ThrowExactlyAsync<NullReferenceException>();
    }

    // --- Cursor opaco ---

    [Fact]
    public void CursorDirectorio_RecuperaLasClavesKeyset_CuandoSeDecodificaLoCodificado()
    {
        var cursor = CursorDirectorio.Codificar("Ana María Torres", "CC-1098765432");

        CursorDirectorio.TryDecodificar(cursor, out var decodificado).Should().BeTrue();
        decodificado.Should().Be(new CursorDirectorio("Ana María Torres", "CC-1098765432"));
        cursor.Should().MatchRegex("^[A-Za-z0-9_-]+$");
    }

    [Fact]
    public void CursorDirectorio_NoDecodifica_CuandoElTextoEsIndescifrable()
    {
        CursorDirectorio.TryDecodificar("esto no es base64url!", out var decodificado).Should().BeFalse();
        decodificado.Should().BeNull();
    }

    // --- Sobre { elementos, siguienteCursor } (CA-3) ---

    private static DirectorioColaborador Vista(string id, string nombre) => new(
        Id: id, TipoDocumento: "CC", NumeroDocumento: id[3..], NombreCompleto: nombre,
        TokensNombre: [nombre.ToLowerInvariant()], CodigoColaborador: "COL-" + id,
        VigenteDesde: new DateOnly(2025, 1, 1), VigenteHasta: DirectorioColaborador.CentinelaVigenciaAbierta);

    [Fact]
    public void Componer_DevuelveTodoSinCursor_CuandoNoHayTake()
    {
        var lista = FunctionEndpoint.Componer([Vista("CC-1", "Ana"), Vista("CC-2", "Beto")], take: null);

        lista.Elementos.Select(e => e.Identificacion).Should().Equal("CC-1", "CC-2");
        lista.SiguienteCursor.Should().BeNull();
    }

    [Fact]
    public void Componer_NoTraeCursor_CuandoLaPaginaNoTieneMasFilas()
    {
        var lista = FunctionEndpoint.Componer([Vista("CC-1", "Ana"), Vista("CC-2", "Beto")], take: 2);

        lista.Elementos.Should().HaveCount(2);
        lista.SiguienteCursor.Should().BeNull();
    }

    [Fact]
    public void Componer_RecortaALaPaginaYApuntaALaUltimaFilaEntregada_CuandoHayUnaFilaDeMas()
    {
        var lista = FunctionEndpoint.Componer(
            [Vista("CC-1", "Ana"), Vista("CC-2", "Beto"), Vista("CC-3", "Carla")], take: 2);

        lista.Elementos.Select(e => e.Identificacion).Should().Equal("CC-1", "CC-2");
        CursorDirectorio.TryDecodificar(lista.SiguienteCursor!, out var cursor).Should().BeTrue();
        cursor.Should().Be(new CursorDirectorio("Beto", "CC-2"));
    }

    [Fact]
    public void Componer_DevuelveListaVacia_CuandoNoHayCoincidencias()
    {
        var lista = FunctionEndpoint.Componer([], take: 10);

        lista.Elementos.Should().BeEmpty();
        lista.SiguienteCursor.Should().BeNull();
    }
}
