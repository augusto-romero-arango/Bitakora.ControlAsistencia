// Smoke tests de ListarResumenesAsistencia -- QUERY control-horas/resumenes-asistencia: una fila
// ResumenAsistencia por colaborador con los tres ejes del Aprobador (programacion, aprobacion,
// anomalias) mas totales de horas, agregados en query-time sobre AsistenciaDiaria (#426).
//
// Quedan ROJOS hasta que el deploy publique la Function en dev: mientras tanto la ruta no existe y
// el host responde 404 a todo. El CI de PR no los ejecuta (solo corre *.Tests); su veredicto real
// se lee despues del deploy.
//
// Arrange via el bus interno, nunca sembrando el event store por fuera de el: cada dia real se
// publica como DiaDepurado al topic "dia-depurado" (mismo mecanismo que
// ListarAsistenciasDiariasSmokeTests, #427). La proyeccion AsistenciaDiaria tiene lifecycle Async
// (MEF-ADR-0034 seccion 3), asi que los casos que dependen del arrange envuelven la consulta en
// Polling.WaitUntilAsync -- agotar el timeout es un fallo real, nunca un skip.
//
// La mayoria de los casos con datos reales fijan CodigosColaborador explicito en el filtro: ese modo
// pagina sobre la lista pedida sin descubrir el universo con un Distinct() sobre Marten, asi que el
// resultado no se mezcla con colaboradores de otras corridas de este mismo smoke test que compartan
// el mismo rango de fechas fijo en ejecuciones futuras. Solo el caso dedicado a la rama "sin filtro"
// (universo descubierto) corre ese riesgo, y lo hace a proposito -- es lo unico que ejercita esa
// rama sin sembrar por fuera del API.
//
// Formas locales DESACOPLADAS del read model y del DTO de respuesta de produccion (isla,
// MEF-ADR-0034 seccion 5): el smoke test no referencia ReadModels ni el Function App.
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.SmokeTests.Fixtures;

namespace Bitakora.ControlAsistencia.ControlHoras.SmokeTests.ListarResumenesAsistencia;

public class ListarResumenesAsistenciaSmokeTests(ApiFixture api, ServiceBusFixture serviceBus)
{
    private readonly HttpClient _client = api.Client;

    private const string RutaListado = "/api/control-horas/resumenes-asistencia";
    private const string TopicDiaDepurado = "dia-depurado";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);
    private static readonly HttpMethod MetodoQuery = new("QUERY");

    // La respuesta viaja en camelCase (ComposicionServicios fija JsonNamingPolicy.CamelCase) y las
    // formas locales de este archivo son PascalCase.
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private sealed record ResumenAsistenciaSmoke(
        string CodigoColaborador,
        int DiasConTurno,
        int DiasConDescanso,
        int DiasSinProgramar,
        int NoSePresento,
        int FranjasIncompletas,
        int VinoEnDescanso,
        int TrabajoSinProgramacion,
        int Aprobados,
        int Pendientes,
        int SinDatos,
        IReadOnlyDictionary<string, decimal> TotalHorasPorConcepto);

    private sealed record ListaResumenesAsistenciaSmoke(
        DateOnly DesdeAplicado,
        DateOnly HastaAplicado,
        bool RangoRecortado,
        IReadOnlyList<ResumenAsistenciaSmoke> Filas);

    private async Task<HttpResponseMessage> ConsultarAsync(object filtro, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(MetodoQuery, RutaListado)
        {
            Content = JsonContent.Create(filtro)
        };
        return await _client.SendAsync(request, ct);
    }

    private async Task<ListaResumenesAsistenciaSmoke> ConsultarOkAsync(object filtro, CancellationToken ct)
    {
        var response = await ConsultarAsync(filtro, ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<ListaResumenesAsistenciaSmoke>(
            JsonOptions, cancellationToken: ct);
        body.Should().NotBeNull();
        return body!;
    }

    private Task<ListaResumenesAsistenciaSmoke> ConsultarHastaQueAsync(
        object filtro, Func<ListaResumenesAsistenciaSmoke, bool> condicion, CancellationToken ct) =>
        Polling.WaitUntilAsync(async () =>
        {
            using var response = await ConsultarAsync(filtro, ct);
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var body = await response.Content.ReadFromJsonAsync<ListaResumenesAsistenciaSmoke>(
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
                NombreCompleto = "[TEST] Smoke Resumenes Asistencia"
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

    // ClasificarPlan produce ConJornada con nombreTurno Y al menos una franja.
    private Task PublicarDiaConJornadaAsync(string codigoColaborador, DateOnly fecha, string nombreTurno) =>
        PublicarDiaDepuradoAsync(
            codigoColaborador, fecha, nombreTurno,
            franjas:
            [
                new
                {
                    HoraInicioProgramada = "08:00:00",
                    HoraFinProgramada = "16:00:00",
                    DiaOffsetFin = 0,
                    Entrada = $"{fecha:yyyy-MM-dd}T08:00:00",
                    Salida = $"{fecha:yyyy-MM-dd}T16:00:00",
                    EsAnomala = false
                }
            ],
            marcaciones:
            [
                new { Timestamp = $"{fecha:yyyy-MM-dd}T08:00:00", Tipo = "ENTRADA" },
                new { Timestamp = $"{fecha:yyyy-MM-dd}T16:00:00", Tipo = "SALIDA" }
            ],
            horasPorConcepto: new Dictionary<string, decimal> { ["OrdinariaDiurna"] = 8.00m });

    // ClasificarPlan produce Descanso con nombreTurno presente y sin franjas; una marcacion ese dia
    // dispara la bandera VinoEnDescanso (AsistenciaDiariaProjection.EsVinoEnDescanso).
    private Task PublicarDiaVinoEnDescansoAsync(string codigoColaborador, DateOnly fecha, string nombreTurno) =>
        PublicarDiaDepuradoAsync(
            codigoColaborador, fecha, nombreTurno,
            franjas: [],
            marcaciones: [new { Timestamp = $"{fecha:yyyy-MM-dd}T09:00:00", Tipo = "ENTRADA" }],
            horasPorConcepto: new Dictionary<string, decimal>());

    // ClasificarPlan produce SinProgramar con nombreTurno null; una marcacion ese dia dispara la
    // bandera TrabajoSinProgramacion (AsistenciaDiariaProjection.EsTrabajoSinProgramacion).
    private Task PublicarDiaTrabajoSinProgramacionAsync(string codigoColaborador, DateOnly fecha) =>
        PublicarDiaDepuradoAsync(
            codigoColaborador, fecha, nombreTurno: null,
            franjas: [],
            marcaciones: [new { Timestamp = $"{fecha:yyyy-MM-dd}T10:00:00", Tipo = "ENTRADA" }],
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
    public async Task ListarResumenesAsistencia_Retorna415_CuandoContentTypeNoEsJson()
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
    public async Task ListarResumenesAsistencia_Retorna400_CuandoElBodyNoEsJsonValido()
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
    public async Task ListarResumenesAsistencia_Retorna400_CuandoElBodyEstaVacio()
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
    public async Task ListarResumenesAsistencia_Retorna422_CuandoLasFechasEstanAusentes()
    {
        var ct = TestContext.Current.CancellationToken;

        var filtro = new { codigosColaborador = new[] { Guid.CreateVersion7().ToString() } };

        var response = await ConsultarAsync(filtro, ct);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarResumenesAsistencia_Retorna422_CuandoElRangoEstaInvertido()
    {
        var ct = TestContext.Current.CancellationToken;

        var filtro = new
        {
            desdeFecha = new DateOnly(2026, 7, 10),
            hastaFecha = new DateOnly(2026, 7, 1)
        };

        var response = await ConsultarAsync(filtro, ct);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarResumenesAsistencia_Retorna200ConListaVacia_CuandoNoHayCodigosColaboradorNiDatosEnElRango()
    {
        var ct = TestContext.Current.CancellationToken;

        // Rango exclusivo de este caso: sin CodigosColaborador el universo se descubre entre TODOS
        // los documentos del rango, asi que debe quedar libre de cualquier otro arrange del archivo.
        var desde = new DateOnly(2025, 9, 1);
        var hasta = new DateOnly(2025, 9, 3);

        var filtro = new { desdeFecha = desde, hastaFecha = hasta };
        var respuesta = await ConsultarOkAsync(filtro, ct);

        respuesta.DesdeAplicado.Should().Be(desde);
        respuesta.HastaAplicado.Should().Be(hasta);
        respuesta.RangoRecortado.Should().BeFalse();
        respuesta.Filas.Should().BeEmpty();
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarResumenesAsistencia_Retorna200ConFilaSinteticaPorCadaCodigo_CuandoCodigosColaboradorNoTienenDatos()
    {
        var ct = TestContext.Current.CancellationToken;

        // Codigos nunca sembrados por ningun test: no pueden tener documento en dev.
        var codigoUno = Guid.CreateVersion7().ToString();
        var codigoDos = Guid.CreateVersion7().ToString();
        var desde = new DateOnly(2025, 9, 10);
        var hasta = new DateOnly(2025, 9, 12);

        var filtro = new
        {
            desdeFecha = desde,
            hastaFecha = hasta,
            codigosColaborador = new[] { codigoUno, codigoDos }
        };
        var respuesta = await ConsultarOkAsync(filtro, ct);

        respuesta.Filas.Should().HaveCount(2);
        respuesta.Filas.Select(f => f.CodigoColaborador).Should().Equal(codigoUno, codigoDos);

        respuesta.Filas.Should().OnlyContain(f =>
            f.DiasSinProgramar == 3
            && f.SinDatos == 3
            && f.DiasConTurno == 0
            && f.DiasConDescanso == 0
            && f.NoSePresento == 0
            && f.FranjasIncompletas == 0
            && f.VinoEnDescanso == 0
            && f.TrabajoSinProgramacion == 0
            && f.Aprobados == 0
            && f.Pendientes == 0
            && f.TotalHorasPorConcepto.Count == 0);
    }

    // Cubre CA-1 y CA-2: los tres dias reales (jornada, vino en descanso, trabajo sin programacion)
    // mas los tres dias vacios del rango deben cerrar los tres ejes exactamente contra los 6 dias.
    // CodigosColaborador explicito evita que el resultado dependa de descubrir el universo.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarResumenesAsistencia_CalculaTresEjesAnomaliasYTotales_CuandoElColaboradorTieneDiasRealesYVaciosEnElRango()
    {
        Assert.SkipWhen(!serviceBus.IsConfigured,
            "ServiceBus no configurado. Usa appsettings.local.json o variable ServiceBus__ConnectionString.");

        var ct = TestContext.Current.CancellationToken;

        var codigoColaborador = Guid.CreateVersion7().ToString();
        var desde = new DateOnly(2026, 7, 20);
        var hasta = new DateOnly(2026, 7, 25);
        var fechaJornada = new DateOnly(2026, 7, 21);
        var fechaVinoEnDescanso = new DateOnly(2026, 7, 22);
        var fechaTrabajoSinProgramacion = new DateOnly(2026, 7, 23);

        await PublicarDiaConJornadaAsync(codigoColaborador, fechaJornada, "[TEST] Turno Jornada");
        await PublicarDiaVinoEnDescansoAsync(codigoColaborador, fechaVinoEnDescanso, "[TEST] Turno Descanso");
        await PublicarDiaTrabajoSinProgramacionAsync(codigoColaborador, fechaTrabajoSinProgramacion);

        var filtro = new
        {
            desdeFecha = desde,
            hastaFecha = hasta,
            codigosColaborador = new[] { codigoColaborador }
        };

        // Los tres dias reales son Provisional, asi que SinDatos baja de 6 (todo vacio) a 3 una vez
        // que el worker materializa las tres filas.
        var respuesta = await ConsultarHastaQueAsync(
            filtro, lista => lista.Filas.Single().SinDatos == 3, ct);

        var fila = respuesta.Filas.Single();
        fila.CodigoColaborador.Should().Be(codigoColaborador);

        // Eje programacion: turno + descanso + sin programar (1 real + 3 vacios) = 6 dias del rango.
        fila.DiasConTurno.Should().Be(1);
        fila.DiasConDescanso.Should().Be(1);
        fila.DiasSinProgramar.Should().Be(4);

        // Eje anomalias: solo las banderas sembradas a proposito, los vacios no aportan ninguna.
        fila.NoSePresento.Should().Be(0);
        fila.FranjasIncompletas.Should().Be(0);
        fila.VinoEnDescanso.Should().Be(1);
        fila.TrabajoSinProgramacion.Should().Be(1);

        // Eje aprobacion: Aprobado todavia no lo produce ningun evento (EstadoAsistencia); los tres
        // dias reales son Pendientes y los tres vacios se avalan como SinDatos = 6 dias del rango.
        fila.Aprobados.Should().Be(0);
        fila.Pendientes.Should().Be(3);
        fila.SinDatos.Should().Be(3);

        fila.TotalHorasPorConcepto.Should().ContainKey("OrdinariaDiurna").WhoseValue.Should().Be(8.00m);
    }

    // CA-3: con CodigosColaborador explicito hay una fila por codigo pedido, EN EL ORDEN pedido,
    // incluida la sintetica del codigo sin datos -- nunca solo la del codigo que si tiene filas.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarResumenesAsistencia_DevuelveSoloLosCodigosPedidosEnOrden_CuandoUnoTieneDatosYOtroNo()
    {
        Assert.SkipWhen(!serviceBus.IsConfigured,
            "ServiceBus no configurado. Usa appsettings.local.json o variable ServiceBus__ConnectionString.");

        var ct = TestContext.Current.CancellationToken;

        var codigoConDatos = Guid.CreateVersion7().ToString();
        var codigoSinDatos = Guid.CreateVersion7().ToString();
        var desde = new DateOnly(2026, 7, 30);
        var hasta = new DateOnly(2026, 8, 2);
        var fechaJornada = new DateOnly(2026, 7, 31);

        await PublicarDiaConJornadaAsync(codigoConDatos, fechaJornada, "[TEST] Turno Mezcla");

        var filtro = new
        {
            desdeFecha = desde,
            hastaFecha = hasta,
            codigosColaborador = new[] { codigoConDatos, codigoSinDatos }
        };

        var respuesta = await ConsultarHastaQueAsync(
            filtro, lista => lista.Filas.First().DiasConTurno == 1, ct);

        respuesta.Filas.Should().HaveCount(2);
        respuesta.Filas.Select(f => f.CodigoColaborador).Should().Equal(codigoConDatos, codigoSinDatos);

        var filaConDatos = respuesta.Filas[0];
        filaConDatos.DiasConTurno.Should().Be(1);
        filaConDatos.DiasSinProgramar.Should().Be(3);
        filaConDatos.Pendientes.Should().Be(1);
        filaConDatos.SinDatos.Should().Be(3);
        filaConDatos.TotalHorasPorConcepto.Should().ContainKey("OrdinariaDiurna").WhoseValue.Should().Be(8.00m);

        var filaSinDatos = respuesta.Filas[1];
        filaSinDatos.DiasConTurno.Should().Be(0);
        filaSinDatos.DiasSinProgramar.Should().Be(4);
        filaSinDatos.SinDatos.Should().Be(4);
        filaSinDatos.TotalHorasPorConcepto.Should().BeEmpty();
    }

    // CA-3, rama sin filtro: el universo se descubre entre los documentos del rango. Take=200 (el
    // maximo) reduce el riesgo de que el colaborador sembrado quede fuera de la pagina si el rango
    // acumula colaboradores de otras corridas futuras de este mismo test -- si eso llegara a pasar,
    // el timeout de Polling lo delata como fallo real, no como skip.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarResumenesAsistencia_ApareceEnElUniversoDescubierto_CuandoNoSeEnviaCodigosColaboradorYElColaboradorTieneDatos()
    {
        Assert.SkipWhen(!serviceBus.IsConfigured,
            "ServiceBus no configurado. Usa appsettings.local.json o variable ServiceBus__ConnectionString.");

        var ct = TestContext.Current.CancellationToken;

        var codigoColaborador = Guid.CreateVersion7().ToString();
        var desde = new DateOnly(2026, 8, 10);
        var hasta = new DateOnly(2026, 8, 11);

        await PublicarDiaConJornadaAsync(codigoColaborador, desde, "[TEST] Turno Universo Descubierto");

        var filtro = new { desdeFecha = desde, hastaFecha = hasta, take = 200 };

        var respuesta = await ConsultarHastaQueAsync(
            filtro, lista => lista.Filas.Any(f => f.CodigoColaborador == codigoColaborador), ct);

        var fila = respuesta.Filas.Single(f => f.CodigoColaborador == codigoColaborador);
        fila.DiasConTurno.Should().Be(1);
        fila.Pendientes.Should().Be(1);
    }

    // CA-4: keyset por CodigoColaborador ascendente sobre la lista pedida -- cursor ">" y fin de
    // lista = pagina con menos filas que Take. No siembra ningun documento real: el modo con
    // CodigosColaborador explicito pagina sobre la lista dada sin descubrir nada en Marten, asi que
    // el mecanismo de paginacion es verificable sin depender de la consistencia eventual del worker.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarResumenesAsistencia_PaginaConKeysetPorCodigoColaborador_CuandoSeEnviaCursor()
    {
        var ct = TestContext.Current.CancellationToken;

        var codigos = new[]
            {
                Guid.CreateVersion7().ToString(),
                Guid.CreateVersion7().ToString(),
                Guid.CreateVersion7().ToString()
            }
            .OrderBy(codigo => codigo, StringComparer.Ordinal)
            .ToArray();

        var desde = new DateOnly(2025, 2, 1);
        var hasta = new DateOnly(2025, 2, 1);

        var filtroPagina1 = new
        {
            desdeFecha = desde,
            hastaFecha = hasta,
            codigosColaborador = codigos,
            take = 2
        };
        var pagina1 = await ConsultarOkAsync(filtroPagina1, ct);

        pagina1.Filas.Should().HaveCount(2);
        pagina1.Filas.Select(f => f.CodigoColaborador).Should().Equal(codigos[0], codigos[1]);

        var filtroPagina2 = new
        {
            desdeFecha = desde,
            hastaFecha = hasta,
            codigosColaborador = codigos,
            cursor = codigos[1],
            take = 2
        };
        var pagina2 = await ConsultarOkAsync(filtroPagina2, ct);

        // Fin de lista: la pagina trae menos filas que el Take pedido.
        pagina2.Filas.Should().HaveCount(1);
        pagina2.Filas.Single().CodigoColaborador.Should().Be(codigos[2]);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarResumenesAsistencia_RecortaRangoHaciaAdelante_CuandoElRangoExcedeLaCotaDe31Dias()
    {
        var ct = TestContext.Current.CancellationToken;

        var desde = new DateOnly(2026, 1, 1);
        var hastaSolicitado = new DateOnly(2026, 12, 31);
        // La cota son 31 dias INCLUSIVE; el literal se afirma a mano, nunca leyendo CotaDias.
        var hastaAplicadaEsperada = desde.AddDays(30);

        var filtro = new { desdeFecha = desde, hastaFecha = hastaSolicitado };
        var respuesta = await ConsultarOkAsync(filtro, ct);

        respuesta.DesdeAplicado.Should().Be(desde);
        respuesta.HastaAplicado.Should().Be(hastaAplicadaEsperada);
        respuesta.RangoRecortado.Should().BeTrue();
    }
}
