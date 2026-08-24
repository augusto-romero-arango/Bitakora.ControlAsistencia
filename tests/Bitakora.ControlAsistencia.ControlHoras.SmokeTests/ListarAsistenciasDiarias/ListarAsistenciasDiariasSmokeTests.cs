// Smoke tests de ListarAsistenciasDiarias -- QUERY control-horas/asistencias-diarias: el calendario
// completo de UN colaborador en un rango, con los dias sin documento sintetizados.
//
// Quedan ROJOS hasta que el deploy publique la Function en dev: mientras tanto la ruta no existe y
// el host responde 404 a todo. El CI de PR no los ejecuta (solo corre *.Tests); su veredicto real
// se lee despues del deploy. Ese mismo 404 es ademas el gate NO VERIFICADO de MEF-ADR-0042 seccion
// 6 -- si persiste tras un deploy exitoso, el sospechoso es el borde filtrando un verbo no
// estandar, no el endpoint.
//
// Arrange via el bus interno, nunca sembrando el event store por fuera de el: cada dia real se
// publica como DiaDepurado al topic "dia-depurado" (mismo mecanismo que
// RecibirDepuracionViaSbSmokeTests). La proyeccion tiene lifecycle Async (MEF-ADR-0034 seccion 3),
// asi que los casos que dependen del arrange envuelven la consulta en Polling.WaitUntilAsync --
// agotar el timeout es un fallo real (worker no desplegado, proyeccion sin materializar), nunca un
// skip.
//
// Formas locales DESACOPLADAS del read model y del DTO de respuesta de produccion: el smoke test no
// referencia ReadModels ni el Function App (isla, MEF-ADR-0034 seccion 5). Los enums locales
// replican el ORDEN de valores de produccion porque STJ los serializa como el entero subyacente --
// si produccion reordenara alguno, la comparacion falla y delata el cambio de contrato.
//
// CA-6 (tenant scoping de la QuerySession) no tiene superficie observable via HTTP negro-caja en un
// entorno de un solo tenant (CA-ADR-0027): lo cubre el test de composicion de la Function.
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.SmokeTests.Fixtures;

namespace Bitakora.ControlAsistencia.ControlHoras.SmokeTests.ListarAsistenciasDiarias;

public class ListarAsistenciasDiariasSmokeTests(ApiFixture api, ServiceBusFixture serviceBus)
{
    private readonly HttpClient _client = api.Client;

    private const string RutaListado = "/api/control-horas/asistencias-diarias";
    private const string TopicDiaDepurado = "dia-depurado";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);
    private static readonly HttpMethod MetodoQuery = new("QUERY");

    // La respuesta viaja en camelCase (ComposicionServicios fija JsonNamingPolicy.CamelCase) y las
    // formas locales de este archivo son PascalCase.
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private enum EstadoAsistenciaPresentadoSmoke
    {
        Provisional,
        Aprobado,
        SinDatos
    }

    private enum PlanDelDiaSmoke
    {
        ConJornada,
        Descanso,
        SinProgramar
    }

    private sealed record FilaAsistenciaDiariaSmoke(
        DateOnly Fecha,
        EstadoAsistenciaPresentadoSmoke Estado,
        PlanDelDiaSmoke Plan,
        string? NombreTurno,
        bool NoSePresento,
        bool FranjasIncompletas,
        bool VinoEnDescanso,
        bool TrabajoSinProgramacion,
        IReadOnlyDictionary<string, decimal> HorasPorConcepto);

    private sealed record ListaAsistenciasDiariasSmoke(
        DateOnly DesdeAplicado,
        DateOnly HastaAplicado,
        bool RangoRecortado,
        IReadOnlyList<FilaAsistenciaDiariaSmoke> Filas);

    private async Task<HttpResponseMessage> ConsultarAsync(object filtro, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(MetodoQuery, RutaListado)
        {
            Content = JsonContent.Create(filtro)
        };
        return await _client.SendAsync(request, ct);
    }

    private Task<ListaAsistenciasDiariasSmoke> ConsultarHastaQueAsync(
        object filtro, Func<ListaAsistenciasDiariasSmoke, bool> condicion, CancellationToken ct) =>
        Polling.WaitUntilAsync(async () =>
        {
            using var response = await ConsultarAsync(filtro, ct);
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var body = await response.Content.ReadFromJsonAsync<ListaAsistenciasDiariasSmoke>(
                JsonOptions, cancellationToken: ct);
            return body is not null && condicion(body) ? body : null;
        }, Timeout);

    private async Task PublicarDiaDepuradoAsync(
        string codigoColaborador, DateOnly fecha, string? nombreTurno,
        object[] franjas, object[] marcaciones, IReadOnlyDictionary<string, decimal> horasPorConcepto)
    {
        var evento = new
        {
            CodigoColaborador = codigoColaborador,
            Fecha = fecha.ToString("yyyy-MM-dd"),
            Colaborador = new
            {
                Identificacion = "CC-777888999",
                CodigoColaborador = codigoColaborador,
                NombreCompleto = "[TEST] Smoke Asistencias Diarias"
            },
            NombreTurno = nombreTurno,
            Franjas = franjas,
            Marcaciones = marcaciones,
            HorasDiscriminadas = new
            {
                HorasPorConcepto = horasPorConcepto,
                Trazabilidad = Array.Empty<string>()
            }
        };

        await serviceBus.PublishAsync(TopicDiaDepurado, evento, Guid.CreateVersion7().ToString());
    }

    // ClasificarPlan produce ConJornada solo con nombreTurno Y al menos una franja.
    private Task PublicarDiaConJornadaAsync(
        string codigoColaborador, DateOnly fecha, string nombreTurno) =>
        PublicarDiaDepuradoAsync(
            codigoColaborador, fecha, nombreTurno,
            franjas:
            [
                new
                {
                    HoraInicioProgramada = "06:00:00",
                    HoraFinProgramada = "14:00:00",
                    DiaOffsetFin = 0,
                    Entrada = $"{fecha:yyyy-MM-dd}T06:00:00",
                    Salida = $"{fecha:yyyy-MM-dd}T14:00:00",
                    EsAnomala = false
                }
            ],
            marcaciones:
            [
                new { Timestamp = $"{fecha:yyyy-MM-dd}T06:00:00", Tipo = "ENTRADA" },
                new { Timestamp = $"{fecha:yyyy-MM-dd}T14:00:00", Tipo = "SALIDA" }
            ],
            horasPorConcepto: new Dictionary<string, decimal> { ["OrdinariaDiurna"] = 8.00m });

    // ClasificarPlan produce Descanso con nombreTurno presente y sin franjas ni marcaciones -- ese
    // dia tambien crea el stream, por eso no es un dia sintetico.
    private Task PublicarDiaDeDescansoAsync(
        string codigoColaborador, DateOnly fecha, string nombreTurno) =>
        PublicarDiaDepuradoAsync(
            codigoColaborador, fecha, nombreTurno,
            franjas: [],
            marcaciones: [],
            horasPorConcepto: new Dictionary<string, decimal>());

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task DebeEstarDisponible_CuandoSeConsultaHealthCheck()
    {
        var ct = TestContext.Current.CancellationToken;
        var response = await _client.GetAsync("/api/health", ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarAsistenciasDiarias_Retorna415_CuandoContentTypeNoEsJson()
    {
        var ct = TestContext.Current.CancellationToken;

        using var request = new HttpRequestMessage(MetodoQuery, RutaListado)
        {
            Content = new StringContent("{}", Encoding.UTF8, "text/plain")
        };

        var response = await _client.SendAsync(request, ct);

        response.StatusCode.Should().Be(HttpStatusCode.UnsupportedMediaType);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarAsistenciasDiarias_Retorna400_CuandoElBodyNoEsJsonValido()
    {
        var ct = TestContext.Current.CancellationToken;

        using var request = new HttpRequestMessage(MetodoQuery, RutaListado)
        {
            Content = new StringContent("{ esto no es json valido", Encoding.UTF8, "application/json")
        };

        var response = await _client.SendAsync(request, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarAsistenciasDiarias_Retorna400_CuandoElBodyEstaVacio()
    {
        var ct = TestContext.Current.CancellationToken;

        using var request = new HttpRequestMessage(MetodoQuery, RutaListado)
        {
            Content = new StringContent(string.Empty, Encoding.UTF8, "application/json")
        };

        var response = await _client.SendAsync(request, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarAsistenciasDiarias_Retorna422_CuandoCodigoColaboradorEstaAusente()
    {
        var ct = TestContext.Current.CancellationToken;

        var filtro = new
        {
            desdeFecha = new DateOnly(2026, 7, 1),
            hastaFecha = new DateOnly(2026, 7, 5)
        };

        var response = await ConsultarAsync(filtro, ct);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarAsistenciasDiarias_Retorna422_CuandoLasFechasEstanAusentes()
    {
        var ct = TestContext.Current.CancellationToken;

        var filtro = new { codigoColaborador = Guid.CreateVersion7().ToString() };

        var response = await ConsultarAsync(filtro, ct);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarAsistenciasDiarias_Retorna422_CuandoElRangoEstaInvertido()
    {
        var ct = TestContext.Current.CancellationToken;

        var filtro = new
        {
            codigoColaborador = Guid.CreateVersion7().ToString(),
            desdeFecha = new DateOnly(2026, 7, 10),
            hastaFecha = new DateOnly(2026, 7, 1)
        };

        var response = await ConsultarAsync(filtro, ct);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarAsistenciasDiarias_Retorna200ConTodasLasFilasSinteticas_CuandoElColaboradorNoTieneNingunDocumentoEnElRango()
    {
        var ct = TestContext.Current.CancellationToken;

        // Codigo nunca sembrado por ningun test: no puede tener documento en dev.
        var codigoColaborador = Guid.CreateVersion7().ToString();
        var desde = new DateOnly(2026, 7, 1);
        var hasta = new DateOnly(2026, 7, 5);

        var filtro = new { codigoColaborador, desdeFecha = desde, hastaFecha = hasta };
        var response = await ConsultarAsync(filtro, ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var respuesta = await response.Content.ReadFromJsonAsync<ListaAsistenciasDiariasSmoke>(
            JsonOptions, cancellationToken: ct);

        respuesta.Should().NotBeNull();
        respuesta!.DesdeAplicado.Should().Be(desde);
        respuesta.HastaAplicado.Should().Be(hasta);
        respuesta.RangoRecortado.Should().BeFalse();

        respuesta.Filas.Should().HaveCount(5);
        respuesta.Filas.Select(f => f.Fecha).Should().Equal(
            desde, desde.AddDays(1), desde.AddDays(2), desde.AddDays(3), desde.AddDays(4));

        respuesta.Filas.Should().OnlyContain(f =>
            f.Estado == EstadoAsistenciaPresentadoSmoke.SinDatos
            && f.Plan == PlanDelDiaSmoke.SinProgramar
            && f.NombreTurno == null
            && !f.NoSePresento
            && !f.FranjasIncompletas
            && !f.VinoEnDescanso
            && !f.TrabajoSinProgramacion
            && f.HorasPorConcepto.Count == 0);
    }

    // Se siembran DOS dias reales de forma distinta (jornada y descanso), no uno solo, para
    // distinguir "mapea el documento real" de "siempre sintetiza".
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarAsistenciasDiarias_CombinaFilasRealesYSinteticasEnElMismoRango_CuandoSoloAlgunosDiasTienenDocumento()
    {
        Assert.SkipWhen(!serviceBus.IsConfigured,
            "ServiceBus no configurado. Usa appsettings.local.json o variable ServiceBus__ConnectionString.");

        var ct = TestContext.Current.CancellationToken;

        var codigoColaborador = Guid.CreateVersion7().ToString();
        var desde = new DateOnly(2026, 7, 10);
        var hasta = new DateOnly(2026, 7, 14);
        var fechaConJornada = new DateOnly(2026, 7, 11);
        var fechaDescanso = new DateOnly(2026, 7, 13);

        await PublicarDiaConJornadaAsync(codigoColaborador, fechaConJornada, "[TEST] Turno Real Uno");
        await PublicarDiaDeDescansoAsync(codigoColaborador, fechaDescanso, "[TEST] Turno Descanso Dos");

        var filtro = new { codigoColaborador, desdeFecha = desde, hastaFecha = hasta };

        var respuesta = await ConsultarHastaQueAsync(filtro, lista =>
            lista.Filas.Any(f => f.Fecha == fechaConJornada && f.Estado != EstadoAsistenciaPresentadoSmoke.SinDatos)
            && lista.Filas.Any(f => f.Fecha == fechaDescanso && f.Estado != EstadoAsistenciaPresentadoSmoke.SinDatos),
            ct);

        respuesta.Filas.Should().HaveCount(5);
        respuesta.Filas.Select(f => f.Fecha).Should().Equal(
            desde, desde.AddDays(1), desde.AddDays(2), desde.AddDays(3), desde.AddDays(4));

        var filaConJornada = respuesta.Filas.Single(f => f.Fecha == fechaConJornada);
        filaConJornada.Estado.Should().Be(EstadoAsistenciaPresentadoSmoke.Provisional);
        filaConJornada.Plan.Should().Be(PlanDelDiaSmoke.ConJornada);
        filaConJornada.NombreTurno.Should().Be("[TEST] Turno Real Uno");
        filaConJornada.NoSePresento.Should().BeFalse();
        filaConJornada.FranjasIncompletas.Should().BeFalse();
        filaConJornada.HorasPorConcepto.Should().ContainKey("OrdinariaDiurna").WhoseValue.Should().Be(8.00m);

        var filaDescanso = respuesta.Filas.Single(f => f.Fecha == fechaDescanso);
        filaDescanso.Estado.Should().Be(EstadoAsistenciaPresentadoSmoke.Provisional);
        filaDescanso.Plan.Should().Be(PlanDelDiaSmoke.Descanso);
        filaDescanso.NombreTurno.Should().Be("[TEST] Turno Descanso Dos");
        filaDescanso.HorasPorConcepto.Should().BeEmpty();

        DateOnly[] fechasSinteticas = [desde, desde.AddDays(2), desde.AddDays(4)];
        foreach (var fecha in fechasSinteticas)
        {
            var filaSintetica = respuesta.Filas.Single(f => f.Fecha == fecha);
            filaSintetica.Estado.Should().Be(EstadoAsistenciaPresentadoSmoke.SinDatos);
            filaSintetica.Plan.Should().Be(PlanDelDiaSmoke.SinProgramar);
            filaSintetica.NombreTurno.Should().BeNull();
        }
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarAsistenciasDiarias_RecortaHaciaAdelanteYSintetizaSoloElRangoAplicado_CuandoElRangoExcedeLaCotaDe31Dias()
    {
        var ct = TestContext.Current.CancellationToken;

        var codigoColaborador = Guid.CreateVersion7().ToString();
        var desde = new DateOnly(2026, 8, 1);
        var hastaSolicitado = new DateOnly(2026, 12, 31);
        // La cota son 31 dias INCLUSIVE; el literal se afirma a mano, nunca leyendo CotaDias.
        var hastaAplicadaEsperada = desde.AddDays(30);

        var filtro = new { codigoColaborador, desdeFecha = desde, hastaFecha = hastaSolicitado };
        var response = await ConsultarAsync(filtro, ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var respuesta = await response.Content.ReadFromJsonAsync<ListaAsistenciasDiariasSmoke>(
            JsonOptions, cancellationToken: ct);

        respuesta.Should().NotBeNull();
        respuesta!.DesdeAplicado.Should().Be(desde);
        respuesta.HastaAplicado.Should().Be(hastaAplicadaEsperada);
        respuesta.RangoRecortado.Should().BeTrue();

        respuesta.Filas.Should().HaveCount(31);
        respuesta.Filas.First().Fecha.Should().Be(desde);
        respuesta.Filas.Last().Fecha.Should().Be(hastaAplicadaEsperada);
    }
}
