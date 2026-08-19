// HU-106 / issue #270: Adicionar marcacion a ControlDiario cuando se crea un Registro de Marcacion.
// El evento privado que dispara el flujo cambia de MarcacionRegistrada a RegistroDeMarcacionCreado
// (CA-3, CA-5); el comportamiento del handler no cambia, solo el tipo del evento consumido.

using Bitakora.ControlAsistencia.ControlHoras.AdicionarMarcacionCuandoRegistroDeMarcacionCreado.EventHandler;
using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;
using Bitakora.ControlAsistencia.ControlHoras.Entities;
using Bitakora.ControlAsistencia.PrivateEvents.ControlHoras;
using Cosmos.EventDriven.Abstractions;
using Cosmos.EventSourcing.Testing.Utilities;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.AdicionarMarcacionCuandoRegistroDeMarcacionCreado;

public class RegistroDeMarcacionCreadoEventHandlerTests
    : PrivateEventHandlerAsyncTest<RegistroDeMarcacionCreado>
{
    // Datos de prueba fijos
    private const string CodigoColaborador = "EMP-001";

    // CA-1: hora fuera de ventana nocturna (>= 04:00) - solo dia calendario
    private static readonly DateTime TimestampFueraDeVentana = new DateTime(2026, 3, 15, 8, 15, 0);

    // CA-2: hora dentro de ventana nocturna (< 04:00) - dia calendario + dia anterior
    private static readonly DateTime TimestampDentroDeVentana = new DateTime(2026, 3, 15, 2, 30, 0);

    // Issue #420: stream ID renotado con prefijo "cd" y fecha en ISO 8601 basico (CA-ADR-0031).
    // CA-7, CA-8: stream IDs computados desde TimestampNormalizado
    private static readonly string StreamIdDia15 = $"cd:{CodigoColaborador}:20260315";
    private static readonly string StreamIdDia14 = $"cd:{CodigoColaborador}:20260314";

    protected override IPrivateEventHandlerAsync<RegistroDeMarcacionCreado> Handler =>
        new RegistroDeMarcacionCreadoEventHandler(EventStore, PublicEventSender);

    // Factory para RegistroDeMarcacionCreado; el timestamp decide si cae en la ventana nocturna.
    private static RegistroDeMarcacionCreado CrearRegistroDeMarcacionCreado(
        DateTime timestampNormalizado,
        string? tipoMarcacion = "ENTRADA",
        string? dispositivoId = "DEV-001") =>
        new(CodigoColaborador, timestampNormalizado, tipoMarcacion, dispositivoId);

    // Factory para MarcacionAdicionada (evento emitido al stream del ControlDiario)
    private static MarcacionAdicionada CrearMarcacionAdicionada(
        string streamId,
        DateTime timestampNormalizado,
        string? tipoMarcacion = "ENTRADA",
        string? dispositivoId = "DEV-001") =>
        new(streamId, CodigoColaborador, timestampNormalizado, tipoMarcacion, dispositivoId);

    // CA-1: hora fuera de ventana nocturna (08:15) -> un solo ControlDiario para dia calendario
    // CA-5: si no existe ControlDiario, se crea con Iniciar(MarcacionAdicionada)
    // CA-6: ControlDiario creado solo por marcacion tiene InformacionColaborador y DetalleTurno nulos
    // CA-7: stream ID es "cd:{CodigoColaborador}:{Fecha:yyyyMMdd}" (issue #420)
    // CA-8: la fecha se extrae del TimestampNormalizado del evento
    // Issue #420 CA-3: ExtraerFechaDeStreamId hidrata Fecha desde la notacion nueva del Id
    [Fact]
    public async Task RegistroDeMarcacionCreado_CreaControlDiarioConMarcacion_CuandoNoExisteYHoraFueraDeVentanaNocturna()
    {
        // Sin Given - el stream no existe para cd:EMP-001:20260315
        await WhenAsync(CrearRegistroDeMarcacionCreado(TimestampFueraDeVentana));

        Then(StreamIdDia15, CrearMarcacionAdicionada(StreamIdDia15, TimestampFueraDeVentana));
        And<ControlDiarioAggregateRoot, ColaboradorProgramado?>(
            StreamIdDia15, c => c.InformacionColaborador, null);
        And<ControlDiarioAggregateRoot, TurnoDiario?>(
            StreamIdDia15, c => c.DetalleTurno, null);
        And<ControlDiarioAggregateRoot, int>(
            StreamIdDia15, c => c.Marcaciones.Count, 1);
        And<ControlDiarioAggregateRoot, DateOnly>(
            StreamIdDia15, c => c.Fecha, new DateOnly(2026, 3, 15));
    }

    // CA-2: hora dentro de ventana nocturna (02:30) -> dos ControlDiarios
    //   - dia calendario (2026-03-15) y dia anterior (2026-03-14)
    // CA-9: la ventana de traslape tiene corte a las 04:00 AM (constante del handler)
    [Fact]
    public async Task RegistroDeMarcacionCreado_CreaDosControlDiarios_CuandoHoraEstaEnVentanaNocturna()
    {
        // Sin Given - ninguno de los dos streams existe
        await WhenAsync(CrearRegistroDeMarcacionCreado(TimestampDentroDeVentana));

        // Dia calendario: 2026-03-15
        Then(StreamIdDia15, CrearMarcacionAdicionada(StreamIdDia15, TimestampDentroDeVentana));
        And<ControlDiarioAggregateRoot, int>(
            StreamIdDia15, c => c.Marcaciones.Count, 1);
        And<ControlDiarioAggregateRoot, DateOnly>(
            StreamIdDia15, c => c.Fecha, new DateOnly(2026, 3, 15));

        // Dia anterior: 2026-03-14 (traslape nocturno)
        // Issue #420 CA-3: dos fechas distintas hidratadas desde dos Ids con la notacion nueva.
        Then(StreamIdDia14, CrearMarcacionAdicionada(StreamIdDia14, TimestampDentroDeVentana));
        And<ControlDiarioAggregateRoot, int>(
            StreamIdDia14, c => c.Marcaciones.Count, 1);
        And<ControlDiarioAggregateRoot, DateOnly>(
            StreamIdDia14, c => c.Fecha, new DateOnly(2026, 3, 14));
    }

    // CA-3: lista de marcaciones crece al adicionar una marcacion nueva
    //        ControlDiario ya existe con una marcacion previa (distinto minuto)
    [Fact]
    public async Task RegistroDeMarcacionCreado_AgregaMarcacionALaLista_CuandoControlDiarioYaExisteConMarcacionPrevia()
    {
        // Timestamp diferente: 07:00 ya existia
        var marcacionPrevia = CrearMarcacionAdicionada(
            StreamIdDia15, new DateTime(2026, 3, 15, 7, 0, 0));

        Given(StreamIdDia15, marcacionPrevia);

        // Nueva marcacion a las 08:15 (distinto minuto)
        await WhenAsync(CrearRegistroDeMarcacionCreado(TimestampFueraDeVentana));

        Then(StreamIdDia15, CrearMarcacionAdicionada(StreamIdDia15, TimestampFueraDeVentana));
        And<ControlDiarioAggregateRoot, int>(
            StreamIdDia15, c => c.Marcaciones.Count, 2);
    }

    // CA-9: borde superior exclusivo de la ventana nocturna.
    //        La hora de corte (04:00:00) ya NO esta dentro de la ventana,
    //        por lo que la marcacion va solo al dia calendario (un stream).
    [Fact]
    public async Task RegistroDeMarcacionCreado_CreaUnSoloControlDiario_CuandoHoraEsIgualALaHoraDeCorte()
    {
        var timestampEnCorte = new DateTime(2026, 3, 15, 4, 0, 0);

        await WhenAsync(CrearRegistroDeMarcacionCreado(timestampEnCorte));

        Then(StreamIdDia15, CrearMarcacionAdicionada(StreamIdDia15, timestampEnCorte));
        And<ControlDiarioAggregateRoot, int>(
            StreamIdDia15, c => c.Marcaciones.Count, 1);

        // El dia anterior no se toca: no hay eventos emitidos para StreamIdDia14
        Then(StreamIdDia14);
    }

    // CA-4: marcacion duplicada por minuto normalizado se ignora
    //        el aggregate no produce nuevo evento ni lanza excepcion
    [Fact]
    public async Task RegistroDeMarcacionCreado_IgnoraMarcacion_CuandoExisteDuplicadoPorMinutoNormalizado()
    {
        // Mismo minuto normalizado: 08:15:00 ya existia (ENTRADA desde DEV-001)
        var marcacionExistente = CrearMarcacionAdicionada(StreamIdDia15, TimestampFueraDeVentana);
        Given(StreamIdDia15, marcacionExistente);

        // Llega otra marcacion con el mismo minuto pero distinto tipo y dispositivo
        await WhenAsync(CrearRegistroDeMarcacionCreado(TimestampFueraDeVentana, "SALIDA", "DEV-002"));

        // Sin nuevos eventos - duplicado ignorado silenciosamente
        Then(StreamIdDia15);
        And<ControlDiarioAggregateRoot, int>(
            StreamIdDia15, c => c.Marcaciones.Count, 1);
    }
}
