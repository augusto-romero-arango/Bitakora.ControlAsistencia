using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;
using Bitakora.ControlAsistencia.ControlHoras.SmokeTests.Fixtures;
using Bitakora.ControlAsistencia.PublicEvents.ControlHoras;

namespace Bitakora.ControlAsistencia.ControlHoras.SmokeTests.AsignarTurnoCuandoProgramacionTurnoDiarioSolicitadaFunction;

public class AsignarTurnoViaSbSmokeTests(ServiceBusFixture serviceBus, PostgresFixture postgres)
{
    private const string TopicEntrada = "programacion-turno-diario-solicitada";
    private const string SuscripcionConsumidor = "control-horas-escucha-programacion";
    private const string TopicDiaCalculado = "dia-calculado";
    private const string SuscripcionSmokeTests = "smoke-tests";
    private const string SchemaControlHoras = "control_horas";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task DebeAsignarTurnoDiario_CuandoSeBusPublicaProgramacionTurnoDiarioSolicitada()
    {
        Assert.SkipWhen(!serviceBus.IsConfigured,
            "ServiceBus no configurado. Usa appsettings.local.json o variable ServiceBus__ConnectionString.");
        Assert.SkipWhen(!postgres.IsConfigured,
            postgres.SkipReason ?? "Postgres no disponible.");

        // Arrange
        var correlationId = Guid.CreateVersion7().ToString();
        var solicitudId = Guid.CreateVersion7();
        var empleadoId = Guid.CreateVersion7().ToString();
        var fecha = new DateOnly(2026, 4, 9);

        var evento = new
        {
            SolicitudId = solicitudId,
            Empleado = new
            {
                EmpleadoId = empleadoId,
                TipoIdentificacion = "CC",
                NumeroIdentificacion = "999888777",
                Nombres = "[TEST] Smoke ServiceBus",
                Apellidos = "[TEST] Verificacion"
            },
            Fecha = fecha.ToString("yyyy-MM-dd"),
            DetalleTurno = new
            {
                Nombre = "[TEST] Turno Smoke SB",
                FranjasOrdinarias = new[]
                {
                    new
                    {
                        HoraInicio = "08:00:00",
                        HoraFin = "16:00:00",
                        DiaOffsetFin = 0,
                        Descansos = Array.Empty<object>(),
                        Extras = Array.Empty<object>()
                    }
                }
            }
        };

        // Arrange: purgar suscripcion smoke-tests de dia-calculado para evitar falsos positivos
        // de ejecuciones anteriores (patron purge-before-act, ADR-0016).
        await serviceBus.PurgeAsync(TopicDiaCalculado, SuscripcionSmokeTests);

        // Act: publicar al topic de Service Bus
        await serviceBus.PublishAsync(TopicEntrada, evento, correlationId);

        // Assert: verificar que el evento TurnoDiarioAsignado fue persistido en PostgreSQL
        var streamId = $"{empleadoId}:{fecha:yyyy-MM-dd}";
        var tipoEvento = "turno_diario_asignado";

        var existe = await postgres.ExisteEventoAsync(
            SchemaControlHoras, streamId, tipoEvento, Timeout,
            campoJson: "SolicitudId", valorJson: solicitudId.ToString());

        existe.Should().BeTrue(
            $"el evento {tipoEvento} con SolicitudId {solicitudId} deberia existir en el stream {streamId}");

        // Assert detallado: obtener el evento especifico y comparar value objects.
        // Issue #322: InformacionEmpleado/DetalleTurno son las CLAVES JSON persistidas (no cambian,
        // MEF-ADR-0036), pero el evento persistido ya no tipa esos campos con InformacionEmpleado
        // (PublicEvents) ni DetalleTurno (PrivateEvents) -- ahora son Empleado y TurnoDiario, propios
        // de ControlHoras.DomainEvents (payload por rol). El smoke test deserializa con el tipo que
        // realmente posee el payload persistido.
        var eventoPersistido = await postgres.ObtenerEventoAsync<JsonElement>(
            SchemaControlHoras, streamId, tipoEvento,
            "SolicitudId", solicitudId.ToString(), TimeSpan.FromSeconds(5));

        var empleadoEsperado = new ColaboradorProgramado(
            empleadoId, "CC", "999888777", "[TEST] Smoke ServiceBus", "[TEST] Verificacion");
        var empleadoPersistido = eventoPersistido
            .GetProperty("InformacionEmpleado").Deserialize<ColaboradorProgramado>();
        empleadoPersistido.Should().Be(empleadoEsperado);

        // Issue #288: el mensaje crudo publicado arriba (objeto anonimo) no lleva "Descripcion" -- el
        // dato derivado solo lo asigna Programacion en produccion (CatalogoTurnos/FranjaOrdinaria/
        // SubFranja), no este payload sintetico. Los DTOs lo normalizan a cadena vacia; el campo se
        // excluye de la comparacion estructural porque aqui no hay texto real que verificar (esa
        // normalizacion la cubre ProgramacionTurnoDiarioSolicitadaPortabilidadTests).
        var turnoDiarioEsperado = new TurnoDiario("[TEST] Turno Smoke SB", [
            new FranjaProgramada(
                new TimeOnly(8, 0), new TimeOnly(16, 0), 0,
                Array.Empty<SubFranjaProgramada>(), Array.Empty<SubFranjaProgramada>(), "")
        ], "");
        var turnoDiarioPersistido = eventoPersistido
            .GetProperty("DetalleTurno").Deserialize<TurnoDiario>();
        turnoDiarioPersistido.Should().BeEquivalentTo(turnoDiarioEsperado,
            opciones => opciones.ExcludingMembersNamed("Descripcion"));

        // Assert HU-131 CA-1/CA-2: DiaCalculado publicado al topic dia-calculado.
        // Se emite siempre, incluso si ControlesDeFranja queda vacio tras la depuracion reactiva.
        var diaCalculado = await serviceBus.WaitForMessageAsync<DiaCalculado>(
            TopicDiaCalculado, SuscripcionSmokeTests,
            e => e.InformacionEmpleado != null && e.InformacionEmpleado.EmpleadoId == empleadoId,
            Timeout);

        diaCalculado.Fecha.Should().Be(fecha);
        diaCalculado.InformacionEmpleado!.EmpleadoId.Should().Be(empleadoId);
        // Issue #183 CA-6: el payload viaja plano (HorasDiscriminadas), deserializado con el serializador
        // POR DEFECTO del fixture (sin resolver custom). El turno se asigno sin marcaciones previas: la
        // franja queda anomala -> sin minutos calculables -> MinutosPorConcepto vacio.
        diaCalculado.HorasDiscriminadas.MinutosPorConcepto.Should().BeEmpty(
            "el turno sin marcaciones deja la franja anomala, sin minutos por concepto");

        // Assert: verificar ausencia de dead letter de ESTA corrida en la suscripcion del consumidor
        // de entrada (issue #223: acotado por SolicitudId, no "DLQ globalmente vacio").
        var existeDeadLetterProgramacion = await serviceBus.ExisteDeadLetterDeEstaCorridaAsync<ProgramacionTurnoDiarioSolicitadaMinimo>(
            TopicEntrada, SuscripcionConsumidor, e => e.SolicitudId == solicitudId);

        existeDeadLetterProgramacion.Should().BeFalse(
            "no deberia haber un dead letter de esta corrida (SolicitudId {0}) en '{1}' - si lo hay, el consumidor fallo al procesar el evento",
            solicitudId, SuscripcionConsumidor);

        // Assert: verificar ausencia de dead letter de ESTA corrida en la suscripcion smoke-tests
        // del topic dia-calculado (issue #223: acotado por EmpleadoId).
        var existeDeadLetterDiaCalculado = await serviceBus.ExisteDeadLetterDeEstaCorridaAsync<DiaCalculadoMinimo>(
            TopicDiaCalculado, SuscripcionSmokeTests, e => e.InformacionEmpleado?.EmpleadoId == empleadoId);

        existeDeadLetterDiaCalculado.Should().BeFalse(
            "no deberia haber un dead letter de esta corrida (EmpleadoId {0}) en '{1}' del topic '{2}'",
            empleadoId, SuscripcionSmokeTests, TopicDiaCalculado);
    }

    // Issue #336 CA-1/CA-3: el evento de bus trae la sede EFECTIVA ya resuelta por la cascada del
    // lado de Programacion (#341) en cada franja -- el handler la propaga (DetalleSede ->
    // SedeProgramada, mapeo mecanico) al persistir TurnoDiarioAsignado. La primera franja trae
    // sede, la segunda no: mismo escenario "Turno Partido" que
    // ProgramacionTurnoDiarioSolicitadaEventHandlerTests, verificado aqui contra el entorno real
    // (persistencia en Postgres + cadena de eventos de Service Bus intacta).
    //
    // Nombre segun MEF-ADR-0016 (<Sujeto>_<LoQuePasa>_Cuando<Condicion>, que el ADR aplica tambien
    // a los smoke tests): el sujeto es el evento de bus que dispara el flujo, igual que en los unit
    // tests del handler. Los dos tests hermanos de esta clase conservan el patron "Debe..." previo
    // al ADR -- son parte de los 60 casos que el ADR deja para un refactor dedicado, no un
    // precedente a replicar en tests nuevos.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ProgramacionTurnoDiarioSolicitada_PersisteElTurnoConSedePorFranja_CuandoElEventoTraeSedesEfectivas()
    {
        Assert.SkipWhen(!serviceBus.IsConfigured,
            "ServiceBus no configurado. Usa appsettings.local.json o variable ServiceBus__ConnectionString.");
        Assert.SkipWhen(!postgres.IsConfigured,
            postgres.SkipReason ?? "Postgres no disponible.");

        // Arrange
        var correlationId = Guid.CreateVersion7().ToString();
        var solicitudId = Guid.CreateVersion7();
        var empleadoId = Guid.CreateVersion7().ToString();
        var fecha = new DateOnly(2026, 4, 11);

        var evento = new
        {
            SolicitudId = solicitudId,
            Empleado = new
            {
                EmpleadoId = empleadoId,
                TipoIdentificacion = "CC",
                NumeroIdentificacion = "222333444",
                Nombres = "[TEST] Smoke Sede",
                Apellidos = "[TEST] Por Franja"
            },
            Fecha = fecha.ToString("yyyy-MM-dd"),
            DetalleTurno = new
            {
                Nombre = "[TEST] Turno Partido Sede",
                // Primera franja con sede efectiva resuelta; segunda sin sede (turno multi-sede
                // parcialmente resuelto) -- distingue "una franja con sede" de "todas comparten
                // la misma sede a nivel turno".
                FranjasOrdinarias = new object[]
                {
                    new
                    {
                        HoraInicio = "06:00:00",
                        HoraFin = "10:00:00",
                        DiaOffsetFin = 0,
                        Descansos = Array.Empty<object>(),
                        Extras = Array.Empty<object>(),
                        Sede = new { Id = "SEDE-SUBA-TEST", Nombre = "[TEST] Suba" }
                    },
                    new
                    {
                        HoraInicio = "14:00:00",
                        HoraFin = "18:00:00",
                        DiaOffsetFin = 0,
                        Descansos = Array.Empty<object>(),
                        Extras = Array.Empty<object>()
                    }
                }
            }
        };

        // Arrange: purgar suscripcion smoke-tests de dia-calculado para evitar falsos positivos
        // de ejecuciones anteriores (patron purge-before-act, ADR-0016).
        await serviceBus.PurgeAsync(TopicDiaCalculado, SuscripcionSmokeTests);

        // Act: publicar al topic de Service Bus
        await serviceBus.PublishAsync(TopicEntrada, evento, correlationId);

        // Assert: verificar que el evento TurnoDiarioAsignado fue persistido en PostgreSQL
        var streamId = $"{empleadoId}:{fecha:yyyy-MM-dd}";
        var tipoEvento = "turno_diario_asignado";

        var existe = await postgres.ExisteEventoAsync(
            SchemaControlHoras, streamId, tipoEvento, Timeout,
            campoJson: "SolicitudId", valorJson: solicitudId.ToString());

        existe.Should().BeTrue(
            $"el evento {tipoEvento} con SolicitudId {solicitudId} deberia existir en el stream {streamId}");

        var eventoPersistido = await postgres.ObtenerEventoAsync<JsonElement>(
            SchemaControlHoras, streamId, tipoEvento,
            "SolicitudId", solicitudId.ToString(), TimeSpan.FromSeconds(5));

        // CA-1: la primera franja persiste con la sede efectiva mapeada a SedeProgramada; CA-3 (a
        // nivel de bloque, cubierto por unit tests de Segmentar) parte de este mismo campo. La
        // segunda franja (sin sede en el evento) queda null -- CA-2 de regresion, ya cubierto por
        // el test anterior de esta clase, se reafirma aqui dentro de un mismo turno multi-sede.
        var sedeSubaEsperada = new SedeProgramada("SEDE-SUBA-TEST", "[TEST] Suba");
        var turnoDiarioEsperado = new TurnoDiario("[TEST] Turno Partido Sede", [
            new FranjaProgramada(
                new TimeOnly(6, 0), new TimeOnly(10, 0), 0,
                Array.Empty<SubFranjaProgramada>(), Array.Empty<SubFranjaProgramada>(), "",
                sedeSubaEsperada),
            new FranjaProgramada(
                new TimeOnly(14, 0), new TimeOnly(18, 0), 0,
                Array.Empty<SubFranjaProgramada>(), Array.Empty<SubFranjaProgramada>(), "")
        ], "");
        var turnoDiarioPersistido = eventoPersistido
            .GetProperty("DetalleTurno").Deserialize<TurnoDiario>();
        turnoDiarioPersistido.Should().BeEquivalentTo(turnoDiarioEsperado,
            opciones => opciones.ExcludingMembersNamed("Descripcion"));

        // Efecto secundario (HU-131): DiaCalculado se sigue publicando con normalidad cuando las
        // franjas traen sede -- el mapeo nuevo no interrumpe la cadena reactiva existente.
        var diaCalculado = await serviceBus.WaitForMessageAsync<DiaCalculado>(
            TopicDiaCalculado, SuscripcionSmokeTests,
            e => e.InformacionEmpleado != null && e.InformacionEmpleado.EmpleadoId == empleadoId,
            Timeout);

        diaCalculado.Fecha.Should().Be(fecha);
        diaCalculado.InformacionEmpleado!.EmpleadoId.Should().Be(empleadoId);

        // Assert: ausencia de dead letter de ESTA corrida en la suscripcion del consumidor de
        // entrada -- confirma que el mapeo DetalleSede -> SedeProgramada no rompe el handler
        // cuando el evento trae sedes efectivas por franja.
        var existeDeadLetterProgramacion = await serviceBus.ExisteDeadLetterDeEstaCorridaAsync<ProgramacionTurnoDiarioSolicitadaMinimo>(
            TopicEntrada, SuscripcionConsumidor, e => e.SolicitudId == solicitudId);

        existeDeadLetterProgramacion.Should().BeFalse(
            "no deberia haber un dead letter de esta corrida (SolicitudId {0}) en '{1}' - si lo hay, el mapeo de sede fallo al procesar el evento",
            solicitudId, SuscripcionConsumidor);
    }

    /// <summary>
    /// Regression test para el bug del issue #29.
    /// Wolverine serializa con camelCase. El endpoint consumidor usaba ToObjectFromJson
    /// con opciones default (case-sensitive), lo que causaba que todas las propiedades
    /// quedaran null y se lanzara NullReferenceException.
    /// Este test verifica que ServiceBusDeserializador con PropertyNameCaseInsensitive=true
    /// resuelve el problema en produccion.
    /// </summary>
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task DebeAsignarTurnoDiario_CuandoMensajeTieneFormatoCamelCaseDeWolverine()
    {
        Assert.SkipWhen(!serviceBus.IsConfigured,
            "ServiceBus no configurado. Usa appsettings.local.json o variable ServiceBus__ConnectionString.");
        Assert.SkipWhen(!postgres.IsConfigured,
            postgres.SkipReason ?? "Postgres no disponible.");

        // Arrange: mensaje en camelCase, exactamente como lo serializa Wolverine
        var correlationId = Guid.CreateVersion7().ToString();
        var solicitudId = Guid.CreateVersion7();
        var empleadoId = Guid.CreateVersion7().ToString();
        var fecha = new DateOnly(2026, 4, 10);

        // Propiedades en camelCase simulan la serializacion real de Wolverine.
        // Antes del fix este formato causaba NullReferenceException en el handler
        // porque ToObjectFromJson usa case-sensitive por defecto.
        var eventoEnFormatoWolverine = new
        {
            solicitudId = solicitudId,
            empleado = new
            {
                empleadoId = empleadoId,
                tipoIdentificacion = "CC",
                numeroIdentificacion = "111222333",
                nombres = "[TEST] Smoke Wolverine",
                apellidos = "[TEST] CamelCase Fix"
            },
            fecha = fecha.ToString("yyyy-MM-dd"),
            detalleTurno = new
            {
                nombre = "[TEST] Turno Wolverine CamelCase",
                franjasOrdinarias = new[]
                {
                    new
                    {
                        horaInicio = "07:00:00",
                        horaFin = "15:00:00",
                        diaOffsetFin = 0,
                        descansos = Array.Empty<object>(),
                        extras = Array.Empty<object>()
                    }
                }
            }
        };

        // Arrange: purgar suscripcion smoke-tests de dia-calculado para evitar falsos positivos
        await serviceBus.PurgeAsync(TopicDiaCalculado, SuscripcionSmokeTests);

        // Act: publicar al topic en formato camelCase
        await serviceBus.PublishAsync(TopicEntrada, eventoEnFormatoWolverine, correlationId);

        // Assert: verificar persistencia en Postgres.
        // Si la deserializacion falla (propiedades null), el handler lanza
        // NullReferenceException, el mensaje va a dead-letter y NUNCA se persiste.
        var streamId = $"{empleadoId}:{fecha:yyyy-MM-dd}";
        var tipoEvento = "turno_diario_asignado";

        var existe = await postgres.ExisteEventoAsync(
            SchemaControlHoras, streamId, tipoEvento, Timeout,
            campoJson: "SolicitudId", valorJson: solicitudId.ToString());

        existe.Should().BeTrue(
            $"el evento {tipoEvento} con SolicitudId {solicitudId} deberia existir. " +
            $"Si falla, ServiceBusDeserializador no esta usando PropertyNameCaseInsensitive=true.");

        // Assert detallado: verificar que los datos se mapearon correctamente.
        // Issue #322: el evento persistido tipa InformacionEmpleado/DetalleTurno con Empleado/
        // TurnoDiario (ControlHoras.DomainEvents), no con los tipos de bus -- ver comentario del
        // primer test de esta clase.
        var eventoPersistido = await postgres.ObtenerEventoAsync<JsonElement>(
            SchemaControlHoras, streamId, tipoEvento,
            "SolicitudId", solicitudId.ToString(), TimeSpan.FromSeconds(5));

        var empleadoEsperado = new ColaboradorProgramado(
            empleadoId, "CC", "111222333", "[TEST] Smoke Wolverine", "[TEST] CamelCase Fix");
        var empleadoPersistido = eventoPersistido
            .GetProperty("InformacionEmpleado").Deserialize<ColaboradorProgramado>();
        empleadoPersistido.Should().Be(empleadoEsperado);

        // Issue #288: mismo motivo que el test anterior -- el mensaje crudo en camelCase no lleva
        // "descripcion", asi que se excluye de la comparacion estructural.
        var turnoDiarioEsperado = new TurnoDiario("[TEST] Turno Wolverine CamelCase", [
            new FranjaProgramada(
                new TimeOnly(7, 0), new TimeOnly(15, 0), 0,
                Array.Empty<SubFranjaProgramada>(), Array.Empty<SubFranjaProgramada>(), "")
        ], "");
        var turnoDiarioPersistido = eventoPersistido
            .GetProperty("DetalleTurno").Deserialize<TurnoDiario>();
        turnoDiarioPersistido.Should().BeEquivalentTo(turnoDiarioEsperado,
            opciones => opciones.ExcludingMembersNamed("Descripcion"));

        // Assert HU-131: DiaCalculado publicado incluso cuando el mensaje llega en camelCase.
        // Valida que la cadena completa (deserializacion case-insensitive -> handler -> publicacion) funciona.
        var diaCalculado = await serviceBus.WaitForMessageAsync<DiaCalculado>(
            TopicDiaCalculado, SuscripcionSmokeTests,
            e => e.InformacionEmpleado != null && e.InformacionEmpleado.EmpleadoId == empleadoId,
            Timeout);

        diaCalculado.Fecha.Should().Be(fecha);
        diaCalculado.InformacionEmpleado!.EmpleadoId.Should().Be(empleadoId);
        // Issue #183 CA-6: el payload plano (HorasDiscriminadas) se deserializa con el serializador POR
        // DEFECTO del fixture (sin resolver custom) incluso cuando el mensaje llega en camelCase. El turno
        // se asigno sin marcaciones: franja anomala -> MinutosPorConcepto vacio.
        diaCalculado.HorasDiscriminadas.MinutosPorConcepto.Should().BeEmpty(
            "el turno sin marcaciones deja la franja anomala, sin minutos por concepto");
    }
}
