// Issue #427: smoke tests de ListarAsistenciasDiarias, verbo QUERY (RFC 10008, MEF-ADR-0042) sobre
// control-horas/asistencias-diarias -- pantalla 2 del Aprobador: los dias de UN colaborador en un
// rango, con el calendario COMPLETO (dias sin documento sintetizados, decision A: "no vino y no
// debia venir" se avala, no se aprueba). No hay proyeccion ni read model nuevos: esta clase consulta
// la MISMA vista materializada AsistenciaDiaria (#426) via (a') session.Query<AsistenciaDiaria>().
//
// Arrange via el bus interno, nunca sembrando el event store por fuera de el: cada dia real se crea
// publicando DiaDepurado al topic "dia-depurado" (mismo mecanismo que RecibirDepuracionViaSbSmokeTests,
// issue #425) -- ControlHoras persiste depuracion_dia_recibida en el stream "dc:{codigo}:{yyyyMMdd}" y
// el worker de proyecciones materializa AsistenciaDiaria de forma asincrona.
//
// Lifecycle Async (MEF-ADR-0034 seccion 3): los casos que dependen de datos sembrados envuelven la
// consulta en Polling.WaitUntilAsync (timeout estandar 30s) -- unica excepcion documentada al "no usar
// Polling directo en tests". Si el timeout se agota es un fallo real (worker no desplegado, o la
// proyeccion nunca materializo el efecto del arrange), nunca un caso para Assert.Skip.
//
// Formas locales DESACOPLADAS del read model de produccion (ReadModels.ControlHoras.AsistenciaDiaria)
// y del DTO de respuesta de produccion (ControlHoras.ListarAsistenciasDiarias.*): el smoke test no
// referencia ReadModels ni el Function App (isla, MEF-ADR-0034 seccion 5). Los enums locales
// (EstadoAsistenciaPresentadoSmoke/PlanDelDiaSmoke) replican el orden de valores de produccion porque
// STJ los serializa como el entero subyacente (ComposicionServicios no registra JsonStringEnumConverter
// para las respuestas HTTP) -- si produccion reordenara alguno de los dos enums, este test detectaria
// el cambio de contrato al fallar la comparacion.
//
// No se repite aqui el detalle de "el ultimo gana" ni el mapeo fino de las cuatro anomalias: esas
// reglas de negocio de la proyeccion companion ya las cubre el unit test de AsistenciaDiariaProjection
// (projection-test-writer). Este smoke test es black-box: solo verifica que el endpoint desplegado
// combina filas reales y sinteticas, recorta el rango, y responde los codigos correctos.
//
// CA-6 (tenant scoping de la QuerySession) no tiene superficie observable via HTTP negro-caja en un
// entorno de un solo tenant (CA-ADR-0027): lo cubre el test de composicion de la Function
// (test-writer/projection-implementer), no este archivo.
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

    // Case-insensitive: la respuesta viaja en camelCase (ComposicionServicios configura
    // JsonNamingPolicy.CamelCase), mientras que las formas locales de este archivo son PascalCase.
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

    private Task<HttpResponseMessage> ConsultarAsync(object filtro, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(MetodoQuery, RutaListado)
        {
            Content = JsonContent.Create(filtro)
        };
        return _client.SendAsync(request, ct);
    }

    // Reintenta la consulta hasta que la proyeccion asincrona satisfaga la condicion -- unica
    // excepcion documentada al "no usar Polling directo en tests" (lifecycle Async, MEF-ADR-0034
    // seccion 3). Si el timeout se agota, Polling.WaitUntilAsync lanza TimeoutException (fallo real).
    private Task<ListaAsistenciasDiariasSmoke> ConsultarHastaQueAsync(
        object filtro, Func<ListaAsistenciasDiariasSmoke, bool> condicion, CancellationToken ct) =>
        Polling.WaitUntilAsync(async () =>
        {
            var response = await ConsultarAsync(filtro, ct);
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var body = await response.Content.ReadFromJsonAsync<ListaAsistenciasDiariasSmoke>(
                JsonOptions, cancellationToken: ct);
            return body is not null && condicion(body) ? body : null;
        }, Timeout);

    // Arrange comun: publica DiaDepurado al bus interno -- mismo mecanismo de
    // RecibirDepuracionViaSbSmokeTests (issue #425). ControlHoras persiste depuracion_dia_recibida y
    // el worker de proyecciones materializa AsistenciaDiaria de forma asincrona.
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

    // Dia con jornada valida: nombreTurno + al menos una franja -- ClasificarPlan produce ConJornada.
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

    // Descanso programado: nombreTurno presente, sin franjas ni marcaciones -- ClasificarPlan produce
    // Descanso (issue #427, "Necesidad de lectura": el descanso programado tambien crea el stream).
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

    // CA-4: sin Content-Type: application/json -> 415, verificado ANTES de leer el body.
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

    // CA-4: body con Content-Type json pero sintacticamente invalido -> 400.
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

    // CA-4: body ausente (Content-Type json, cero bytes) -> 400.
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

    // CA-4: JSON valido sin CodigoColaborador -> 422 (obligatorio; pantalla de UN colaborador).
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

    // CA-4: JSON valido sin DesdeFecha/HastaFecha -> 422 (ambas obligatorias).
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarAsistenciasDiarias_Retorna422_CuandoLasFechasEstanAusentes()
    {
        var ct = TestContext.Current.CancellationToken;

        var filtro = new { codigoColaborador = Guid.CreateVersion7().ToString() };

        var response = await ConsultarAsync(filtro, ct);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    // CA-4: DesdeFecha posterior a HastaFecha -> 422 (rango invertido).
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

    // CA-2/CA-5: un colaborador sin ningun documento en el rango recibe 200 con el calendario
    // COMPLETO sintetizado -- nunca 404 (un rango sin documentos son N filas sinteticas, no un error).
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarAsistenciasDiarias_Retorna200ConTodasLasFilasSinteticas_CuandoElColaboradorNoTieneNingunDocumentoEnElRango()
    {
        var ct = TestContext.Current.CancellationToken;

        // Arrange: codigoColaborador nuevo, nunca sembrado por ningun test -- no puede tener documento.
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

        // CA-2/CA-5: 5 filas, una por dia del rango, orden Fecha ascendente.
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

    // CA-1/CA-2: un rango con SOLO algunos dias con documento combina, en la misma respuesta, filas
    // reales (mapeadas de AsistenciaDiaria) y filas sinteticas para el resto del rango -- el nucleo de
    // "Que devuelve" del issue #427. Se siembran DOS dias reales de forma distinta (jornada y
    // descanso), no uno solo, para distinguir "mapea el documento real" de "siempre sintetiza".
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarAsistenciasDiarias_CombinaFilasRealesYSinteticasEnElMismoRango_CuandoSoloAlgunosDiasTienenDocumento()
    {
        Assert.SkipWhen(!serviceBus.IsConfigured,
            "ServiceBus no configurado. Usa appsettings.local.json o variable ServiceBus__ConnectionString.");

        var ct = TestContext.Current.CancellationToken;

        var codigoColaborador = Guid.CreateVersion7().ToString();
        var desde = new DateOnly(2026, 7, 10);
        var hasta = new DateOnly(2026, 7, 14); // 5 dias: 10, 11, 12, 13, 14
        var fechaConJornada = new DateOnly(2026, 7, 11);
        var fechaDescanso = new DateOnly(2026, 7, 13);

        await PublicarDiaConJornadaAsync(codigoColaborador, fechaConJornada, "[TEST] Turno Real Uno");
        await PublicarDiaDeDescansoAsync(codigoColaborador, fechaDescanso, "[TEST] Turno Descanso Dos");

        var filtro = new { codigoColaborador, desdeFecha = desde, hastaFecha = hasta };

        // Act + Assert: reintentar hasta que AMBOS dias reales esten materializados por el worker.
        var respuesta = await ConsultarHastaQueAsync(filtro, lista =>
            lista.Filas.Any(f => f.Fecha == fechaConJornada && f.Estado != EstadoAsistenciaPresentadoSmoke.SinDatos)
            && lista.Filas.Any(f => f.Fecha == fechaDescanso && f.Estado != EstadoAsistenciaPresentadoSmoke.SinDatos),
            ct);

        // Assert: CA-1 -- exactamente una fila por dia del rango, orden Fecha ascendente.
        respuesta.Filas.Should().HaveCount(5);
        respuesta.Filas.Select(f => f.Fecha).Should().Equal(
            desde, desde.AddDays(1), desde.AddDays(2), desde.AddDays(3), desde.AddDays(4));

        // Assert: el dia con jornada mapea los campos reales de AsistenciaDiaria.
        var filaConJornada = respuesta.Filas.Single(f => f.Fecha == fechaConJornada);
        filaConJornada.Estado.Should().Be(EstadoAsistenciaPresentadoSmoke.Provisional);
        filaConJornada.Plan.Should().Be(PlanDelDiaSmoke.ConJornada);
        filaConJornada.NombreTurno.Should().Be("[TEST] Turno Real Uno");
        filaConJornada.NoSePresento.Should().BeFalse();
        filaConJornada.FranjasIncompletas.Should().BeFalse();
        filaConJornada.HorasPorConcepto.Should().ContainKey("OrdinariaDiurna").WhoseValue.Should().Be(8.00m);

        // Assert: el dia de descanso programado tambien mapea como fila real, sin horas.
        var filaDescanso = respuesta.Filas.Single(f => f.Fecha == fechaDescanso);
        filaDescanso.Estado.Should().Be(EstadoAsistenciaPresentadoSmoke.Provisional);
        filaDescanso.Plan.Should().Be(PlanDelDiaSmoke.Descanso);
        filaDescanso.NombreTurno.Should().Be("[TEST] Turno Descanso Dos");
        filaDescanso.HorasPorConcepto.Should().BeEmpty();

        // Assert: CA-2 -- el resto del rango (sin documento) son filas sinteticas.
        DateOnly[] fechasSinteticas = [desde, desde.AddDays(2), desde.AddDays(4)];
        foreach (var fecha in fechasSinteticas)
        {
            var filaSintetica = respuesta.Filas.Single(f => f.Fecha == fecha);
            filaSintetica.Estado.Should().Be(EstadoAsistenciaPresentadoSmoke.SinDatos);
            filaSintetica.Plan.Should().Be(PlanDelDiaSmoke.SinProgramar);
            filaSintetica.NombreTurno.Should().BeNull();
        }
    }

    // CA-3: rango que excede 31 dias se recorta hacia adelante desde DesdeFecha, la respuesta lo
    // declara, y la sintesis del calendario cubre SOLO el rango aplicado (no lo pedido).
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarAsistenciasDiarias_RecortaHaciaAdelanteYSintetizaSoloElRangoAplicado_CuandoElRangoExcedeLaCotaDe31Dias()
    {
        var ct = TestContext.Current.CancellationToken;

        var codigoColaborador = Guid.CreateVersion7().ToString();
        var desde = new DateOnly(2026, 8, 1);
        var hastaSolicitado = new DateOnly(2026, 12, 31); // ~152 dias, muy por encima de la cota
        var hastaAplicadaEsperada = desde.AddDays(30); // cota de 31 dias, inclusive

        var filtro = new { codigoColaborador, desdeFecha = desde, hastaFecha = hastaSolicitado };
        var response = await ConsultarAsync(filtro, ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var respuesta = await response.Content.ReadFromJsonAsync<ListaAsistenciasDiariasSmoke>(
            JsonOptions, cancellationToken: ct);

        respuesta.Should().NotBeNull();
        respuesta!.DesdeAplicado.Should().Be(desde);
        respuesta.HastaAplicado.Should().Be(hastaAplicadaEsperada);
        respuesta.RangoRecortado.Should().BeTrue();

        // Assert: la sintesis del calendario cubre EXACTAMENTE el rango aplicado (31 dias).
        respuesta.Filas.Should().HaveCount(31);
        respuesta.Filas.First().Fecha.Should().Be(desde);
        respuesta.Filas.Last().Fecha.Should().Be(hastaAplicadaEsperada);
    }
}
