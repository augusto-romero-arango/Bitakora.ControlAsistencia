using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.SmokeTests.Fixtures;

namespace Bitakora.ControlAsistencia.Programacion.SmokeTests.CrearTurnoFunction;

public class CrearTurnoSmokeTests(ApiFixture api, PostgresFixture postgres)
{
    private const string SchemaProgramacion = "programacion";
    private const string TipoEventoTurnoCreado = "turno_creado";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    // Forma minima de SedeProgramada para asertar sobre el JSON persistido sin referenciar
    // Programacion.DomainEvents desde los smoke tests (mismo criterio que DeadLetterMinimos).
    private sealed record SedeMinima(string Id, string Nombre);

    private static readonly JsonSerializerOptions OpcionesLectura = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static SedeMinima? SedeDe(JsonElement franja) =>
        franja.TryGetProperty("sede", out var sede)
            ? sede.Deserialize<SedeMinima>(OpcionesLectura)
            : null;

    private readonly HttpClient _client = api.Client;

    // El nombre es unico en el catalogo (invariante del dominio): sufijar el default con el turnoId
    // mantiene estos smoke tests re-ejecutables contra el mismo entorno dev. Los tests de la propia
    // invariante pasan su nombre explicito, que si debe repetirse entre dos turnos distintos.
    private static object PayloadValido(Guid? turnoId = null, string? nombre = null)
    {
        var id = turnoId ?? Guid.CreateVersion7();
        return new
        {
            turnoId = id,
            nombre = nombre ?? $"[TEST] Turno Diurno {id}",
            ordinarias = new[]
            {
                new
                {
                    inicio = "08:00:00",
                    fin = "16:00:00",
                    descansos = Array.Empty<object>(),
                    extras = Array.Empty<object>()
                }
            }
        };
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task HealthCheck_DebeResponder200()
    {
        var ct = TestContext.Current.CancellationToken;
        var response = await _client.GetAsync("/api/health", ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task CrearTurno_DebeRetornar202_CuandoPayloadEsValido()
    {
        var ct = TestContext.Current.CancellationToken;
        var response = await _client.PostAsJsonAsync("/api/programacion/turnos", PayloadValido(), ct);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task CrearTurno_DebeRetornar409_CuandoTurnoYaExiste()
    {
        var ct = TestContext.Current.CancellationToken;
        var turnoId = Guid.CreateVersion7();
        var payload = PayloadValido(turnoId);

        await _client.PostAsJsonAsync("/api/programacion/turnos", payload, ct);
        var response = await _client.PostAsJsonAsync("/api/programacion/turnos", payload, ct);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // Issue #497: espera a que FichaTurno materialice el turno antes del POST duplicado -- sin
    // esto, un 202/409 inmediato no distingue "la comparacion contra el catalogo funciono" de "la
    // proyeccion Async (MEF-ADR-0034 seccion 3) aun no vio nada" (best-effort, CA-ADR-0030).
    private async Task EsperarTurnoMaterializadoAsync(Guid turnoId, CancellationToken ct) =>
        await Polling.WaitUntilTrueAsync(async () =>
        {
            var response = await _client.GetAsync($"/api/programacion/turnos/{turnoId}", ct);
            return response.StatusCode == HttpStatusCode.OK;
        }, Timeout);

    // CA-1: nombre coincide EXACTAMENTE con uno existente en el catalogo -> 409.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task CrearTurno_DebeRetornar409_CuandoNombreCoincideExactamenteConUnoDelCatalogo()
    {
        var ct = TestContext.Current.CancellationToken;
        var sufijo = Guid.CreateVersion7();
        var nombreExistente = $"[TEST] Limpieza Manana {sufijo}";
        var turnoExistenteId = Guid.CreateVersion7();

        var arrange = await _client.PostAsJsonAsync(
            "/api/programacion/turnos", PayloadValido(turnoExistenteId, nombreExistente), ct);
        arrange.StatusCode.Should().Be(HttpStatusCode.Accepted,
            "el arrange de este smoke test depende de que CrearTurno funcione");
        await EsperarTurnoMaterializadoAsync(turnoExistenteId, ct);

        var response = await _client.PostAsJsonAsync(
            "/api/programacion/turnos", PayloadValido(Guid.CreateVersion7(), nombreExistente), ct);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // CA-2: nombre difiere solo en mayusculas/espacios (trim + colapso + case-insensitive) de uno
    // existente -> 409.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task CrearTurno_DebeRetornar409_CuandoNombreDifiereSoloEnMayusculasYEspaciosDeUnoDelCatalogo()
    {
        var ct = TestContext.Current.CancellationToken;
        var sufijo = Guid.CreateVersion7();
        var nombreExistente = $"[TEST] Limpieza Manana {sufijo}";
        var nombreConEspaciosYMayusculas = $"  [TEST]  limpieza   MANANA {sufijo} ";
        var turnoExistenteId = Guid.CreateVersion7();

        var arrange = await _client.PostAsJsonAsync(
            "/api/programacion/turnos", PayloadValido(turnoExistenteId, nombreExistente), ct);
        arrange.StatusCode.Should().Be(HttpStatusCode.Accepted,
            "el arrange de este smoke test depende de que CrearTurno funcione");
        await EsperarTurnoMaterializadoAsync(turnoExistenteId, ct);

        var response = await _client.PostAsJsonAsync(
            "/api/programacion/turnos", PayloadValido(Guid.CreateVersion7(), nombreConEspaciosYMayusculas), ct);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // CA-3: nombre difiere solo en acentos de uno existente -> se crea normalmente (decision del
    // experto: normalizar acentos abre falsos positivos, los acentos SON significativos).
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task CrearTurno_DebeRetornar202_CuandoNombreDifiereSoloEnAcentosDeUnoDelCatalogo()
    {
        var ct = TestContext.Current.CancellationToken;
        var sufijo = Guid.CreateVersion7();
        var nombreConAcento = $"[TEST] Limpieza Mañana {sufijo}";
        var nombreSinAcento = $"[TEST] Limpieza Manana {sufijo}";
        var turnoExistenteId = Guid.CreateVersion7();

        var arrange = await _client.PostAsJsonAsync(
            "/api/programacion/turnos", PayloadValido(turnoExistenteId, nombreConAcento), ct);
        arrange.StatusCode.Should().Be(HttpStatusCode.Accepted,
            "el arrange de este smoke test depende de que CrearTurno funcione");
        await EsperarTurnoMaterializadoAsync(turnoExistenteId, ct);

        var turnoSinAcentoId = Guid.CreateVersion7();
        var response = await _client.PostAsJsonAsync(
            "/api/programacion/turnos", PayloadValido(turnoSinAcentoId, nombreSinAcento), ct);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        // El 202 solo dice que el comando fue aceptado: el efecto secundario real de este handler
        // (StartStream -> turno visible en el catalogo) es lo que cierra MEF-ADR-0013.
        await EsperarTurnoMaterializadoAsync(turnoSinAcentoId, ct);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task CrearTurno_DebeRetornar400_CuandoPayloadEsInvalido()
    {
        var ct = TestContext.Current.CancellationToken;
        var payload = new
        {
            turnoId = Guid.Empty,
            nombre = "",
            ordinarias = Array.Empty<object>()
        };

        var response = await _client.PostAsJsonAsync("/api/programacion/turnos", payload, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // Issue #335 CA-1/CA-2: turno "partido" con sede prearmada por franja (narrativa del issue:
    // "Vigilante partido" -> manana en Suba, tarde en Chapinero) mas una tercera franja SIN sede.
    // Verifica los dos efectos del handler: el 202 del endpoint y la persistencia en el event store
    // (CrearTurnoCommandHandler -> IEventStore.StartStream). turno_creado no cruza ningun bus, asi
    // que mt_events es la unica ventana black-box a lo que quedo grabado -- y es la que cierra el
    // riesgo real de este issue: que el resolver de serializacion del Function App desplegado no
    // registre la clave "sede" y el dato se pierda en silencio con un 202 igual de verde.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task CrearTurno_PersisteLaSedePrearmadaDeCadaFranja_CuandoAlgunasFranjasTraenSede()
    {
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");

        var ct = TestContext.Current.CancellationToken;
        var turnoId = Guid.CreateVersion7();
        var nombreTurno = $"[TEST] Vigilante Partido {turnoId}";
        var payload = new
        {
            turnoId,
            nombre = nombreTurno,
            ordinarias = new object[]
            {
                new
                {
                    inicio = "06:00:00",
                    fin = "12:00:00",
                    descansos = Array.Empty<object>(),
                    extras = Array.Empty<object>(),
                    sede = new { id = "SEDE-SUBA", nombre = "[TEST] Suba" }
                },
                new
                {
                    inicio = "13:00:00",
                    fin = "19:00:00",
                    descansos = Array.Empty<object>(),
                    extras = Array.Empty<object>(),
                    sede = new { id = "SEDE-CHAPINERO", nombre = "[TEST] Chapinero" }
                },
                new
                {
                    inicio = "20:00:00",
                    fin = "22:00:00",
                    descansos = Array.Empty<object>(),
                    extras = Array.Empty<object>()
                }
            }
        };

        var response = await _client.PostAsJsonAsync("/api/programacion/turnos", payload, ct);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        // CatalogoTurnos.Apply asigna Id = evento.TurnoId.ToString() -- el stream id es el guid
        // canonico, sin formato explicito (MEF-ADR-0037).
        var streamId = turnoId.ToString();

        var existe = await postgres.ExisteEventoAsync(
            SchemaProgramacion, streamId, TipoEventoTurnoCreado, Timeout);

        existe.Should().BeTrue(
            $"el evento {TipoEventoTurnoCreado} deberia existir en el stream {streamId} tras crear el turno");

        var eventoPersistido = await postgres.ObtenerEventoAsync<JsonElement>(
            SchemaProgramacion, streamId, TipoEventoTurnoCreado,
            campoJson: "Nombre", valorJson: nombreTurno, Timeout);

        var franjas = eventoPersistido.GetProperty("FranjasOrdinarias").EnumerateArray().ToList();
        franjas.Should().HaveCount(3);

        // CA-1: cada franja prearmada conserva SU sede (no la del vecino).
        SedeDe(franjas[0]).Should().Be(new SedeMinima("SEDE-SUBA", "[TEST] Suba"));
        SedeDe(franjas[1]).Should().Be(new SedeMinima("SEDE-CHAPINERO", "[TEST] Chapinero"));

        // CA-2/CA-4: la franja sin sede no agrega la clave al JSON persistido -- misma forma que
        // los streams escritos antes de este issue (ShouldSerialize omite el campo cuando es null).
        franjas[2].TryGetProperty("sede", out _).Should().BeFalse(
            "una franja sin sede prearmada no debe escribir la clave 'sede' en el evento persistido");
    }

    // Issue #335 CA-3: sede presente pero con Id vacio se rechaza junto a las demas invariantes
    // de la franja (error acumulado en TurnoCreado.Crear -> AggregateException -> 400).
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task CrearTurno_Retorna400_CuandoSedeDeFranjaTieneIdVacio()
    {
        var ct = TestContext.Current.CancellationToken;
        var turnoId = Guid.CreateVersion7();
        var payload = new
        {
            turnoId,
            nombre = $"[TEST] Turno Sede Incompleta {turnoId}",
            ordinarias = new object[]
            {
                new
                {
                    inicio = "08:00:00",
                    fin = "16:00:00",
                    descansos = Array.Empty<object>(),
                    extras = Array.Empty<object>(),
                    sede = new { id = "", nombre = "[TEST] Suba" }
                }
            }
        };

        var response = await _client.PostAsJsonAsync("/api/programacion/turnos", payload, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // Issue #335 CA-3: sede presente pero con Nombre en blanco se rechaza igual que Id vacio.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task CrearTurno_Retorna400_CuandoSedeDeFranjaTieneNombreEnBlanco()
    {
        var ct = TestContext.Current.CancellationToken;
        var turnoId = Guid.CreateVersion7();
        var payload = new
        {
            turnoId,
            nombre = $"[TEST] Turno Sede Incompleta {turnoId}",
            ordinarias = new object[]
            {
                new
                {
                    inicio = "08:00:00",
                    fin = "16:00:00",
                    descansos = Array.Empty<object>(),
                    extras = Array.Empty<object>(),
                    sede = new { id = "SEDE-SUBA", nombre = "   " }
                }
            }
        };

        var response = await _client.PostAsJsonAsync("/api/programacion/turnos", payload, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // turno_creado no cruza ningun bus: la persistencia en mt_events es el unico efecto
    // secundario verificable de este handler.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task CrearTurno_DebeRetornar202YPersistirCeroFranjas_CuandoEsDescanso()
    {
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");

        var ct = TestContext.Current.CancellationToken;
        var turnoId = Guid.CreateVersion7();
        var nombreTurno = $"[TEST] Descanso Dominical {turnoId}";
        var payload = new
        {
            turnoId,
            nombre = nombreTurno,
            ordinarias = Array.Empty<object>(),
            esDescanso = true
        };

        var response = await _client.PostAsJsonAsync("/api/programacion/turnos", payload, ct);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var streamId = turnoId.ToString();
        var eventoPersistido = await postgres.ObtenerEventoAsync<JsonElement>(
            SchemaProgramacion, streamId, TipoEventoTurnoCreado,
            campoJson: "Nombre", valorJson: nombreTurno, Timeout);

        eventoPersistido.GetProperty("FranjasOrdinarias").EnumerateArray().Should().BeEmpty(
            "TurnoCreado.CrearDescanso construye el evento con FranjasOrdinarias vacia");

        // Issue #599 CA-7: el evento persistido debe distinguir "descanso" de "incompleto" por
        // el campo EsDescanso, ya no por la ausencia de franjas (ver test de turno incompleto).
        eventoPersistido.GetProperty("EsDescanso").GetBoolean().Should().BeTrue(
            "TurnoCreado.CrearDescanso debe marcar EsDescanso = true en el evento persistido");
    }

    // Issue #599 CA-7: un turno sin ordinarias y sin marca de descanso ya no es 400 (se retira
    // la regla NotEmpty del validator) -- nace como turno incompleto, con EsDescanso = false y
    // FranjasOrdinarias vacia. mt_events es la unica ventana black-box para distinguirlo del
    // descanso, que persiste la misma lista vacia pero con EsDescanso = true.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task CrearTurno_DebeRetornar202YPersistirTurnoIncompleto_CuandoNoTraeFranjasNiMarca()
    {
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");

        var ct = TestContext.Current.CancellationToken;
        var turnoId = Guid.CreateVersion7();
        var nombreTurno = $"[TEST] Incompleto {turnoId}";
        var payload = new { turnoId, nombre = nombreTurno };

        var response = await _client.PostAsJsonAsync("/api/programacion/turnos", payload, ct);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var streamId = turnoId.ToString();
        var eventoPersistido = await postgres.ObtenerEventoAsync<JsonElement>(
            SchemaProgramacion, streamId, TipoEventoTurnoCreado,
            campoJson: "Nombre", valorJson: nombreTurno, Timeout);

        eventoPersistido.GetProperty("FranjasOrdinarias").EnumerateArray().Should().BeEmpty(
            "un turno sin ordinarias se persiste con FranjasOrdinarias vacia");
        eventoPersistido.GetProperty("EsDescanso").GetBoolean().Should().BeFalse(
            "sin la marca esDescanso, el evento persistido debe quedar EsDescanso = false (turno incompleto)");
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task CrearTurno_Retorna400_CuandoEsDescansoConFranjas()
    {
        var ct = TestContext.Current.CancellationToken;
        var turnoId = Guid.CreateVersion7();
        var payload = new
        {
            turnoId,
            nombre = $"[TEST] Descanso Contradictorio {turnoId}",
            ordinarias = new[]
            {
                new
                {
                    inicio = "08:00:00",
                    fin = "16:00:00",
                    descansos = Array.Empty<object>(),
                    extras = Array.Empty<object>()
                }
            },
            esDescanso = true
        };

        var response = await _client.PostAsJsonAsync("/api/programacion/turnos", payload, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // Los offsets del descanso no viajan en el body: los infiere el dominio desde su ordinaria.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task CrearTurno_PersisteDescansoDeMadrugada_CuandoOrdinariaEsNocturna()
    {
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");

        var ct = TestContext.Current.CancellationToken;
        var turnoId = Guid.CreateVersion7();
        var nombreTurno = $"[TEST] Nocturno Con Descanso {turnoId}";
        var payload = new
        {
            turnoId,
            nombre = nombreTurno,
            ordinarias = new object[]
            {
                new
                {
                    inicio = "22:00:00",
                    fin = "06:00:00",
                    descansos = new object[] { new { inicio = "02:00:00", fin = "02:30:00" } }
                }
            }
        };

        var response = await _client.PostAsJsonAsync("/api/programacion/turnos", payload, ct);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var streamId = turnoId.ToString();
        var eventoPersistido = await postgres.ObtenerEventoAsync<JsonElement>(
            SchemaProgramacion, streamId, TipoEventoTurnoCreado,
            campoJson: "Nombre", valorJson: nombreTurno, Timeout);

        var descanso = eventoPersistido.GetProperty("FranjasOrdinarias")[0].GetProperty("descansos")[0];

        descanso.GetProperty("horaInicio").GetString().Should().Be("02:00:00");
        descanso.GetProperty("horaFin").GetString().Should().Be("02:30:00");
        descanso.GetProperty("diaOffsetInicio").GetInt32().Should().Be(1);
        descanso.GetProperty("diaOffsetFin").GetInt32().Should().Be(1);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task CrearTurno_PersisteFranjaDe24Horas_CuandoDiaOffsetFinEsExplicito()
    {
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");

        var ct = TestContext.Current.CancellationToken;
        var turnoId = Guid.CreateVersion7();
        var nombreTurno = $"[TEST] Turno 24 Horas {turnoId}";
        var payload = new
        {
            turnoId,
            nombre = nombreTurno,
            ordinarias = new object[]
            {
                new
                {
                    inicio = "08:00:00",
                    fin = "08:00:00",
                    diaOffsetFin = 1
                }
            }
        };

        var response = await _client.PostAsJsonAsync("/api/programacion/turnos", payload, ct);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var streamId = turnoId.ToString();
        var eventoPersistido = await postgres.ObtenerEventoAsync<JsonElement>(
            SchemaProgramacion, streamId, TipoEventoTurnoCreado,
            campoJson: "Nombre", valorJson: nombreTurno, Timeout);

        eventoPersistido.GetProperty("FranjasOrdinarias")[0]
            .GetProperty("diaOffsetFin").GetInt32().Should().Be(1);
    }
}
