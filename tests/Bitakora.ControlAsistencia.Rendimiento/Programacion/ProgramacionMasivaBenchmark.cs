using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Rendimiento.Fixtures;
using Bitakora.ControlAsistencia.Rendimiento.Plataforma;
using Bitakora.ControlAsistencia.Rendimiento.Reporte;

namespace Bitakora.ControlAsistencia.Rendimiento.Programacion;

// SLI de CA-ADR-0044: desde la llamada a solicitar_programacion_turno_por_grupo hasta que la
// proyeccion AsistenciaDiaria tiene todos los dias del mes de todo el grupo. Cada corrida siembra
// su propia sede y sus propios colaboradores (siempre una primera programacion) en tenant-smoke,
// la identidad interina del MCP sin Bearer; la limpieza semanal los borra (CA-ADR-0040).
public sealed class ProgramacionMasivaBenchmark(McpFixture mcp, PostgresFixture postgres, AzureFixture azure)
    : IClassFixture<McpFixture>, IClassFixture<PostgresFixture>, IClassFixture<AzureFixture>
{
    private const string Tenant = "tenant-smoke";
    private const string SettingDelCamino = "Microsoft.Web/sites/func-asist-dev-mcp-asistente#appsetting:Programacion__SolicitudEnFechas";
    private static readonly TimeSpan IntervaloDeConteo = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan TimeoutDePreparacion = TimeSpan.FromMinutes(5);

    [Fact]
    [Trait("Benchmark", "programacion-masiva")]
    public async Task ProgramacionMasiva_ProyectaElMesDeTodoElGrupoDentroDelSlo_CuandoSeSolicitaPorSede()
    {
        var ct = TestContext.Current.CancellationToken;
        var parametros = Parametros.Leer();
        var corrida = DateTime.UtcNow.ToString("yyMMddHHmmss", CultureInfo.InvariantCulture);
        var prefijo = $"carga-{corrida}-";
        var reporte = new ReporteDeCorrida("programacion-masiva", corrida) { EtiquetaDelProgreso = "Filas de AsistenciaDiaria" };

        reporte.FotoInicial = await azure.FotografiarAsync(ct);
        reporte.Parametro("Colaboradores", parametros.Colaboradores);
        reporte.Parametro("Mes", $"{parametros.Desde:yyyy-MM} ({parametros.Dias} dias)");
        reporte.Parametro("Camino (Programacion__SolicitudEnFechas del MCP)", Camino(reporte.FotoInicial));
        reporte.Parametro("SLO", TimeSpan.FromSeconds(parametros.SloSegundos));
        reporte.Parametro("Tiempo maximo", TimeSpan.FromMinutes(parametros.TiempoMaximoMinutos));

        var preparacion = Stopwatch.StartNew();
        var sede = await RegistrarSedeAsync(corrida, ct);
        var turnoId = await CrearTurnoAsync(corrida, ct);
        await RegistrarColaboradoresAsync(prefijo, corrida, sede, parametros, ct);
        await EsperarGrupoAsync(sede, prefijo, parametros.Colaboradores, ct);
        reporte.Resultado("Preparacion (fuera del SLI)", preparacion.Elapsed);

        var esperado = (long)parametros.Colaboradores * parametros.Dias;
        var inicio = DateTimeOffset.UtcNow;
        var cronometro = Stopwatch.StartNew();

        // El muestreo arranca con el cronometro y no cuando responde la tool: la tool por grupo hace
        // todos sus POST dentro de una sola llamada y la cadena empieza a proyectar antes de que
        // esa llamada termine.
        using var corte = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var muestreo = MuestrearHastaProyectarAsync(prefijo, esperado, cronometro, parametros, reporte, corte.Token);
        try
        {
            var respuesta = await mcp.LlamarAsync("solicitar_programacion_turno_por_grupo", new Dictionary<string, object?>
            {
                ["desde"] = parametros.Desde.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                ["hasta"] = parametros.Hasta.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                ["turno_id"] = turnoId,
                ["sede_de_programacion"] = sede,
                ["sede"] = sede
            }, ct);
            reporte.Resultado("Llamada a la tool", cronometro.Elapsed);
            LeerSolicitud(respuesta, parametros, reporte);
        }
        catch
        {
            await corte.CancelAsync();
            try { await muestreo; } catch (OperationCanceledException) { }
            throw;
        }

        var (alcanzado, duracion) = await muestreo;
        var fin = DateTimeOffset.UtcNow;

        reporte.Resultado("Filas esperadas", esperado);
        reporte.Resultado("Filas alcanzadas", alcanzado);
        if (alcanzado < esperado)
            reporte.Hallazgos.Add($"Se agoto el tiempo maximo: faltaron {esperado - alcanzado} filas de {esperado}.");
        reporte.Sli = new IndicadorDeNivel(
            "Solicitud -> proyeccion", duracion.TotalSeconds, parametros.SloSegundos,
            alcanzado >= esperado && duracion.TotalSeconds <= parametros.SloSegundos);

        reporte.Metricas = await MetricasDeLaVentanaAsync(inicio, fin, ct);
        reporte.FotoFinal = await azure.FotografiarAsync(ct);
        await reporte.EscribirAsync(ct);

        alcanzado.Should().Be(esperado, "la proyeccion debe tener todos los dias de todo el grupo antes del tiempo maximo");
        duracion.TotalSeconds.Should().BeLessThanOrEqualTo(
            parametros.SloSegundos, "el SLI de programacion masiva es de {0} s", parametros.SloSegundos);
    }

    private async Task<string> RegistrarSedeAsync(string corrida, CancellationToken ct)
    {
        var codigo = $"carga-{corrida}";
        var respuesta = await mcp.LlamarAsync("registrar_sede", new Dictionary<string, object?>
        {
            ["codigo"] = codigo,
            ["nombre"] = $"[CARGA] Sede {corrida}"
        }, ct);
        ExigirJson("registrar_sede", respuesta)?.Dispose();
        return codigo;
    }

    private async Task<string> CrearTurnoAsync(string corrida, CancellationToken ct)
    {
        var respuesta = await mcp.LlamarAsync("crear_turno", new Dictionary<string, object?>
        {
            ["nombre"] = $"[CARGA] Turno {corrida}",
            ["franjas"] = """[{"franja":"08:00-16:00"}]"""
        }, ct);
        using var creado = ExigirJson("crear_turno", respuesta);
        var turnoId = creado!.RootElement.GetProperty("turno").GetProperty("id").GetString()!;

        // La tool por grupo resuelve el turno del catalogo, que es una proyeccion Async.
        await Polling.WaitUntilAsync(async () =>
        {
            using var turno = McpFixture.LeerJson(await mcp.LlamarAsync(
                "obtener_turno", new Dictionary<string, object?> { ["turno_id"] = turnoId }, ct));
            return turno?.RootElement.GetProperty("completo").GetBoolean() == true ? turnoId : null;
        }, TimeoutDePreparacion, ct);

        return turnoId;
    }

    private async Task RegistrarColaboradoresAsync(
        string prefijo, string corrida, string sede, Parametros parametros, CancellationToken ct)
    {
        var filas = Enumerable.Range(1, parametros.Colaboradores).Select(i => new Dictionary<string, string>
        {
            ["tipo_identificacion"] = "CC",
            ["numero_identificacion"] = $"{corrida}{i:D4}",
            ["primer_nombre"] = "[CARGA]",
            ["primer_apellido"] = $"Colaborador {i:D4}",
            ["codigo_colaborador"] = $"{prefijo}{i:D4}",
            ["fecha_inicio"] = parametros.Desde.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["codigo_sede"] = sede
        });

        foreach (var tanda in filas.Chunk(parametros.TandaDeRegistro))
        {
            var respuesta = await mcp.LlamarAsync("registrar_colaboradores", new Dictionary<string, object?>
            {
                ["colaboradores"] = JsonSerializer.Serialize(tanda)
            }, ct);
            using var resumen = ExigirJson("registrar_colaboradores", respuesta);
            if (resumen!.RootElement.TryGetProperty("rechazados", out var rechazados))
                throw new InvalidOperationException($"registrar_colaboradores rechazo filas de la preparacion: {rechazados.GetRawText()}");
        }
    }

    private async Task EsperarGrupoAsync(string sede, string prefijo, int colaboradores, CancellationToken ct)
    {
        await Polling.WaitUntilAsync(async () =>
        {
            var visibles = 0;
            string? cursor = null;
            do
            {
                using var pagina = ExigirJson("listar_colaboradores", await mcp.LlamarAsync(
                    "listar_colaboradores",
                    new Dictionary<string, object?> { ["sede"] = sede, ["cursor"] = cursor },
                    ct))!;
                visibles += pagina.RootElement.GetProperty("colaboradores").EnumerateArray()
                    .Count(c => c.TryGetProperty("codigoColaborador", out var codigo)
                                && codigo.GetString()?.StartsWith(prefijo, StringComparison.Ordinal) == true);
                cursor = pagina.RootElement.TryGetProperty("siguienteCursor", out var siguiente) ? siguiente.GetString() : null;
            } while (cursor is not null);

            return visibles >= colaboradores ? new object() : null;
        }, TimeoutDePreparacion, ct, TimeSpan.FromSeconds(2));
    }

    private static void LeerSolicitud(string respuesta, Parametros parametros, ReporteDeCorrida reporte)
    {
        using var documento = McpFixture.LeerJson(respuesta)
            ?? throw new InvalidOperationException($"solicitar_programacion_turno_por_grupo rechazo la solicitud: {respuesta}");
        var raiz = documento.RootElement;

        var programados = raiz.GetProperty("programados").EnumerateArray().ToList();
        reporte.Resultado("Grupo resuelto", raiz.GetProperty("grupoResuelto").GetInt32());
        reporte.Resultado("Colaboradores programados", programados.Count);
        reporte.Resultado("Dias programados", programados.Sum(p => p.GetProperty("dias").GetInt32()));
        reporte.Resultado("Omitidos", raiz.GetProperty("omitidos").GetInt32());

        if (programados.Count < parametros.Colaboradores)
            reporte.Hallazgos.Add($"Solo se programaron {programados.Count} de {parametros.Colaboradores} colaboradores.");
        if (raiz.TryGetProperty("fallidos", out var fallidos))
        {
            reporte.Hallazgos.Add($"{fallidos.GetArrayLength()} colaboradores fallaron (detalle en el JSON).");
            reporte.Detalle = new { fallidos = fallidos.Clone() };
        }
    }

    /// <summary>
    /// Cada 5 s: filas de AsistenciaDiaria y conexiones de Postgres por estado. Termina al llegar a
    /// las filas esperadas (el fin del SLI es el instante de esa muestra) o al tiempo maximo.
    /// </summary>
    private async Task<(long Filas, TimeSpan Momento)> MuestrearHastaProyectarAsync(
        string prefijo, long esperado, Stopwatch cronometro, Parametros parametros, ReporteDeCorrida reporte,
        CancellationToken ct)
    {
        var limite = TimeSpan.FromMinutes(parametros.TiempoMaximoMinutos);
        while (true)
        {
            var filas = await postgres.ContarAsistenciasDiariasAsync(Tenant, prefijo, ct);
            var momento = cronometro.Elapsed;
            reporte.Progreso.Add(new MuestraDeProgreso(
                momento.TotalSeconds, filas, await postgres.ContarConexionesPorEstadoAsync(ct)));
            if (filas >= esperado || momento >= limite)
                return (filas, momento);

            await Task.Delay(IntervaloDeConteo, ct);
        }
    }

    // Azure Monitor fecha la actividad hasta dos minutos despues del reloj del runner: en las corridas
    // del 2026-10-31 lo que termino a las 15:59:29 cayo en el minuto 16:01 (CPU de ControlHoras y
    // completados de la cola). Por eso la ventana cierra cuatro minutos despues del fin del SLI, y
    // ademas se espera a que las metricas lleguen antes de consultarla.
    private async Task<MetricasDeLaVentana> MetricasDeLaVentanaAsync(
        DateTimeOffset inicio, DateTimeOffset fin, CancellationToken ct)
    {
        var desde = Truncar(inicio).AddMinutes(-1);
        var hasta = Truncar(fin).AddMinutes(4);
        var espera = hasta.AddMinutes(Configuracion.Leer("Rendimiento:RetrasoMetricasMinutos", 3)) - DateTimeOffset.UtcNow;
        if (espera > TimeSpan.Zero)
            await Task.Delay(espera, ct);

        return await azure.MetricasAsync(desde, hasta, ct);
    }

    private static DateTimeOffset Truncar(DateTimeOffset momento) =>
        new(momento.Year, momento.Month, momento.Day, momento.Hour, momento.Minute, 0, TimeSpan.Zero);

    private static string Camino(FotoDeInfraestructura foto) =>
        foto.Valores.TryGetValue(SettingDelCamino, out var valor)
            ? bool.TryParse(valor, out var enFechas) && enFechas ? "en-fechas" : $"por-dia (valor '{valor}')"
            : foto.Errores.Count == 0 ? "por-dia (setting ausente)" : "desconocido (sin foto de app settings)";

    private static JsonDocument? ExigirJson(string tool, string respuesta) =>
        McpFixture.LeerJson(respuesta)
        ?? throw new InvalidOperationException($"{tool} rechazo la preparacion: {respuesta}");

    private sealed record Parametros(
        int Colaboradores, DateOnly Desde, DateOnly Hasta, int Dias, int SloSegundos, int TiempoMaximoMinutos,
        int TandaDeRegistro)
    {
        private const string Seccion = "Rendimiento:ProgramacionMasiva";

        public static Parametros Leer()
        {
            var mes = Configuracion.Actual[$"{Seccion}:Mes"];
            var desde = string.IsNullOrWhiteSpace(mes)
                ? new DateOnly(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1).AddMonths(1)
                : DateOnly.ParseExact($"{mes}-01", "yyyy-MM-dd", CultureInfo.InvariantCulture);
            var hasta = desde.AddMonths(1).AddDays(-1);

            return new Parametros(
                Configuracion.Leer($"{Seccion}:Colaboradores", 100),
                desde,
                hasta,
                hasta.DayNumber - desde.DayNumber + 1,
                Configuracion.Leer($"{Seccion}:SloSegundos", 60),
                Configuracion.Leer($"{Seccion}:TiempoMaximoMinutos", 30),
                Configuracion.Leer($"{Seccion}:TandaDeRegistro", 50));
        }
    }
}
