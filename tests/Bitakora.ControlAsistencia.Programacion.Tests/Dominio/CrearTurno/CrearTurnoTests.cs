// HU-4: Implementar comando CrearTurno con aggregate, handler y endpoint HTTP
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.Dominio.CrearTurno;
using Bitakora.ControlAsistencia.Programacion.Dominio.Entities;
using Bitakora.ControlAsistencia.Programacion.Dominio.Eventos;
using Cosmos.EventSourcing.Abstractions.Commands;
using Cosmos.EventSourcing.Testing.Utilities;
using FluentValidation;

using ComandoCrearTurno = Bitakora.ControlAsistencia.Programacion.Dominio.Comandos.CrearTurno;

namespace Bitakora.ControlAsistencia.Programacion.Tests.Dominio.Handlers;

/// <summary>
/// Tests de CrearTurno: command handler y validator.
/// Interfaz publica del aggregate: Apply(TurnoCreado), ToString().
/// Estado interno del aggregate (no accesible directamente): nombre, franjas, activo.
/// </summary>
public class CrearTurnoTests
{
    public const string NombreTurno = "Turno Manana";

    // Factory method compartido entre las clases anidadas
    public static ComandoCrearTurno.Franja FranjaDiurnaSimple() =>
        new(new TimeOnly(8, 0), new TimeOnly(16, 0), [], []);

    public static ComandoCrearTurno ComandoConUnaFranja(Guid turnoId) =>
        new(turnoId, NombreTurno, [FranjaDiurnaSimple()]);

    // ---- CA-3, CA-4: CrearTurnoCommandHandler ----

    public class CrearTurnoCommandHandlerTests : CommandHandlerAsyncTest<ComandoCrearTurno>
    {
        protected override ICommandHandlerAsync<ComandoCrearTurno> Handler =>
            new CrearTurnoCommandHandler(EventStore);

        // CA-3: handler persiste evento cuando turno no existe
        // CA-1: aggregate aplica TurnoCreado y establece Id (AggregateRoot.Id = turnoId.ToString())
        // CA-2: ToString produce "{nombre} (franja1)" usando el ToString() de cada FranjaOrdinaria
        [Fact]
        public async Task DebeEmitirTurnoCreadoYEstablecerEstado_CuandoTurnoNoExiste()
        {
            var comando = CrearTurnoTests.ComandoConUnaFranja(GuidAggregateId);
            var eventoEsperado = TurnoCreado.Crear(comando);

            Given();
            await WhenAsync(comando);

            Then(eventoEsperado);
            And<CatalogoTurnos, string>(c => c.Id, GuidAggregateId.ToString());
            And<CatalogoTurnos, string>(c => c.ToString()!, $"{NombreTurno} (08:00-16:00)");
        }

        // CA-4: handler lanza excepcion cuando turno ya existe (idempotencia -> 409 Conflict)
        [Fact]
        public async Task DebeLanzarExcepcion_CuandoTurnoYaExiste()
        {
            var comando = CrearTurnoTests.ComandoConUnaFranja(GuidAggregateId);
            var eventoPrevio = TurnoCreado.Crear(comando);

            Given(eventoPrevio);

            var act = async () => await WhenAsync(comando);
            await act.Should().ThrowExactlyAsync<InvalidOperationException>()
                .WithMessage($"*{CrearTurnoCommandHandler.Mensajes.TurnoYaExiste}*");
        }
    }

    // ---- CA-5: CrearTurnoValidator ----

    public class CrearTurnoValidatorTests
    {
        private readonly IValidator<ComandoCrearTurno> _validator = new CrearTurnoValidator();

        // CA-5 (camino feliz): todos los campos validos pasan la validacion
        [Fact]
        public async Task DebeSerValido_CuandoDatosCompletos()
        {
            var comando = CrearTurnoTests.ComandoConUnaFranja(Guid.NewGuid());
            var resultado = await _validator.ValidateAsync(
                comando, TestContext.Current.CancellationToken);
            resultado.IsValid.Should().BeTrue();
        }

        // CA-5: TurnoId no puede ser Guid vacio
        [Fact]
        public async Task DebeRechazar_CuandoTurnoIdEsGuidVacio()
        {
            var comando = new ComandoCrearTurno(
                Guid.Empty, CrearTurnoTests.NombreTurno, [CrearTurnoTests.FranjaDiurnaSimple()]);
            var resultado = await _validator.ValidateAsync(
                comando, TestContext.Current.CancellationToken);
            resultado.IsValid.Should().BeFalse();
            resultado.Errors.Should()
                .Contain(e => e.PropertyName == nameof(ComandoCrearTurno.TurnoId));
        }

        // CA-5: Nombre no puede estar vacio
        [Fact]
        public async Task DebeRechazar_CuandoNombreEstaVacio()
        {
            var comando = new ComandoCrearTurno(
                Guid.NewGuid(), string.Empty, [CrearTurnoTests.FranjaDiurnaSimple()]);
            var resultado = await _validator.ValidateAsync(
                comando, TestContext.Current.CancellationToken);
            resultado.IsValid.Should().BeFalse();
            resultado.Errors.Should()
                .Contain(e => e.PropertyName == nameof(ComandoCrearTurno.Nombre));
        }

        // CA-5: Ordinarias no puede estar vacia
        [Fact]
        public async Task DebeRechazar_CuandoOrdinariaEstaVacia()
        {
            var comando = new ComandoCrearTurno(Guid.NewGuid(), CrearTurnoTests.NombreTurno, []);
            var resultado = await _validator.ValidateAsync(
                comando, TestContext.Current.CancellationToken);
            resultado.IsValid.Should().BeFalse();
            resultado.Errors.Should()
                .Contain(e => e.PropertyName == nameof(ComandoCrearTurno.Ordinarias));
        }
    }
}
