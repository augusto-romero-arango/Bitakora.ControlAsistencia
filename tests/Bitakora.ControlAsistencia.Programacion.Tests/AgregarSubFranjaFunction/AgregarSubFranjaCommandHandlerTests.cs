// Issue #603: agregar un descanso o un extra a una franja ordinaria ya existente -- segundo paso
// del diseno de turno por pasos (CA-ADR-0033).

using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.AgregarSubFranjaFunction;
using Bitakora.ControlAsistencia.Programacion.AgregarSubFranjaFunction.CommandHandler;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.Programacion.Entities;
using Cosmos.EventSourcing.Abstractions.Commands;
using Cosmos.EventSourcing.Testing.Utilities;

namespace Bitakora.ControlAsistencia.Programacion.Tests.AgregarSubFranjaFunction;

public class AgregarSubFranjaCommandHandlerTests : CommandHandlerAsyncTest<AgregarSubFranja>
{
    private static readonly Guid TurnoId = Guid.Parse("019600a0-0000-7000-8000-000000000603");

    protected override ICommandHandlerAsync<AgregarSubFranja> Handler =>
        new AgregarSubFranjaCommandHandler(EventStore);

    private static TurnoCreado CrearEventoTurnoConFranjaNocturna() =>
        TurnoCreado.Crear(TurnoId, "Turno Manana",
            [new DatosFranja(new TimeOnly(22, 0), new TimeOnly(6, 0), [], [])]);

    private static TurnoCreado CrearEventoTurnoDeDescanso() =>
        TurnoCreado.CrearDescanso(TurnoId, "Descanso Compensatorio");

    // CA-5: camino feliz -- Tipo = Descanso emite DescansoAgregado con la franja resultante.
    [Fact]
    public async Task AgregarSubFranja_EmiteDescansoAgregado_CuandoElTipoEsDescanso()
    {
        Given(TurnoId.ToString(), CrearEventoTurnoConFranjaNocturna());

        await WhenAsync(new AgregarSubFranja(
            TurnoId, new TimeOnly(22, 0), TipoSubFranja.Descanso,
            new TimeOnly(2, 0), new TimeOnly(2, 30)));

        Then(TurnoId.ToString(), DescansoAgregado.Crear(TurnoId,
            FranjaOrdinaria.Crear(new TimeOnly(22, 0), new TimeOnly(6, 0))
                .ConDescanso(new TimeOnly(2, 0), new TimeOnly(2, 30))));
        And<CatalogoTurnos, int>(TurnoId.ToString(),
            c => c.ObtenerDetalle().FranjasOrdinarias[0].Descansos.Count, 1);
    }

    // CA-5: Tipo = Extra emite ExtraAgregado.
    [Fact]
    public async Task AgregarSubFranja_EmiteExtraAgregado_CuandoElTipoEsExtra()
    {
        Given(TurnoId.ToString(), CrearEventoTurnoConFranjaNocturna());

        await WhenAsync(new AgregarSubFranja(
            TurnoId, new TimeOnly(22, 0), TipoSubFranja.Extra,
            new TimeOnly(5, 0), new TimeOnly(6, 0)));

        Then(TurnoId.ToString(), ExtraAgregado.Crear(TurnoId,
            FranjaOrdinaria.Crear(new TimeOnly(22, 0), new TimeOnly(6, 0))
                .ConExtra(new TimeOnly(5, 0), new TimeOnly(6, 0))));
        And<CatalogoTurnos, int>(TurnoId.ToString(),
            c => c.ObtenerDetalle().FranjasOrdinarias[0].Extras.Count, 1);
    }

    // CA-3: turno inexistente -> 404.
    [Fact]
    public async Task AgregarSubFranja_LanzaKeyNotFoundException_CuandoElTurnoNoExisteEnElCatalogo()
    {
        var act = async () => await WhenAsync(new AgregarSubFranja(
            TurnoId, new TimeOnly(22, 0), TipoSubFranja.Descanso,
            new TimeOnly(2, 0), new TimeOnly(2, 30)));

        await act.Should().ThrowExactlyAsync<KeyNotFoundException>()
            .WithMessage($"*{AgregarSubFranjaCommandHandler.Mensajes.TurnoNoEncontrado}*");
        Then(TurnoId.ToString());
    }

    // CA-3: precedencia -- retirado gana sobre descanso y sobre franja-no-existe.
    [Fact]
    public async Task AgregarSubFranja_LanzaInvalidOperationException_CuandoElTurnoFueRetirado()
    {
        Given(TurnoId.ToString(), CrearEventoTurnoConFranjaNocturna(), TurnoRetirado.Crear(TurnoId));

        var act = async () => await WhenAsync(new AgregarSubFranja(
            TurnoId, new TimeOnly(22, 0), TipoSubFranja.Descanso,
            new TimeOnly(2, 0), new TimeOnly(2, 30)));

        await act.Should().ThrowExactlyAsync<InvalidOperationException>()
            .WithMessage($"*{AgregarSubFranjaCommandHandler.Mensajes.TurnoRetirado}*");
        Then(TurnoId.ToString());
        And<CatalogoTurnos, ResultadoAsignabilidadTurno>(TurnoId.ToString(),
            c => c.EvaluarAsignabilidad(), ResultadoAsignabilidadTurno.Retirado);
    }

    [Fact]
    public async Task AgregarSubFranja_LanzaInvalidOperationException_CuandoElTurnoEsDescanso()
    {
        Given(TurnoId.ToString(), CrearEventoTurnoDeDescanso());

        var act = async () => await WhenAsync(new AgregarSubFranja(
            TurnoId, new TimeOnly(22, 0), TipoSubFranja.Descanso,
            new TimeOnly(2, 0), new TimeOnly(2, 30)));

        await act.Should().ThrowExactlyAsync<InvalidOperationException>()
            .WithMessage($"*{AgregarSubFranjaCommandHandler.Mensajes.TurnoEsDescanso}*");
        Then(TurnoId.ToString());
        And<CatalogoTurnos, int>(TurnoId.ToString(),
            c => c.ObtenerDetalle().FranjasOrdinarias.Count, 0);
    }

    [Fact]
    public async Task AgregarSubFranja_LanzaInvalidOperationException_CuandoNingunaFranjaEmpiezaAEsaHora()
    {
        Given(TurnoId.ToString(), CrearEventoTurnoConFranjaNocturna());

        var act = async () => await WhenAsync(new AgregarSubFranja(
            TurnoId, new TimeOnly(23, 0), TipoSubFranja.Descanso,
            new TimeOnly(2, 0), new TimeOnly(2, 30)));

        await act.Should().ThrowExactlyAsync<InvalidOperationException>()
            .WithMessage($"*{AgregarSubFranjaCommandHandler.Mensajes.FranjaNoExiste}*");
        Then(TurnoId.ToString());
        And<CatalogoTurnos, int>(TurnoId.ToString(),
            c => c.ObtenerDetalle().FranjasOrdinarias[0].Descansos.Count, 0);
    }

    // CA-4: invariante estructural del VO (hija fuera del contenedor) sube sin capturarse -- dos
    // canales de error nunca mezclados en el mismo metodo (CA-ADR-0030).
    [Fact]
    public async Task AgregarSubFranja_DejaSubirArgumentException_CuandoLaHijaQuedaFueraDeLaFranjaContenedora()
    {
        Given(TurnoId.ToString(), CrearEventoTurnoConFranjaNocturna());

        var act = async () => await WhenAsync(new AgregarSubFranja(
            TurnoId, new TimeOnly(22, 0), TipoSubFranja.Descanso,
            new TimeOnly(5, 0), new TimeOnly(7, 0)));

        await act.Should().ThrowExactlyAsync<ArgumentException>()
            .WithMessage($"*{FranjaTemporal.Mensajes.FranjaHijaFueraDeContenedor}*");
        Then(TurnoId.ToString());
        And<CatalogoTurnos, int>(TurnoId.ToString(),
            c => c.ObtenerDetalle().FranjasOrdinarias[0].Descansos.Count, 0);
    }
}
