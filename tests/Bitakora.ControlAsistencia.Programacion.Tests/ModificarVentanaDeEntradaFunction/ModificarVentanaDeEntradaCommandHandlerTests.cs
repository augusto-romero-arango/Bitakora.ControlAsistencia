using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.Programacion.Entities;
using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Bitakora.ControlAsistencia.Programacion.ModificarVentanaDeEntradaFunction;
using Bitakora.ControlAsistencia.Programacion.ModificarVentanaDeEntradaFunction.CommandHandler;
using Cosmos.EventSourcing.Abstractions.Commands;
using Cosmos.EventSourcing.Testing.Utilities;

namespace Bitakora.ControlAsistencia.Programacion.Tests.ModificarVentanaDeEntradaFunction;

public class ModificarVentanaDeEntradaCommandHandlerTests : CommandHandlerAsyncTest<ModificarVentanaDeEntrada>
{
    protected override ICommandHandlerAsync<ModificarVentanaDeEntrada> Handler =>
        new ModificarVentanaDeEntradaCommandHandler(EventStore);

    private static HorasYMinutos Horas(int h, int m = 0) => HorasYMinutos.Crear(h, m);

    private static DatosFranja Franja(int hi, int mi, int hf, int mf) =>
        new(new TimeOnly(hi, mi), new TimeOnly(hf, mf), [], []);

    private TurnoCreado ConFranjas(params DatosFranja[] franjas) =>
        TurnoCreado.Crear(GuidAggregateId, "Turno", franjas);

    private TurnoCreado Fijo() => ConFranjas(Franja(7, 0, 16, 0));

    private static ModificarVentanaDeEntrada Comando(Guid id, int horas, int minutos = 0) =>
        new(id, new(horas, minutos));

    [Fact]
    public async Task ModificarVentanaDeEntrada_EmiteVentanaDeEntradaModificada_CuandoElTurnoTieneFranjaYLaAmplitudEsValida()
    {
        Given(Fijo());
        await WhenAsync(Comando(GuidAggregateId, 3));
        Then(VentanaDeEntradaModificada.Crear(GuidAggregateId, Horas(3)));
        And<CatalogoTurnos, bool>(c => c.Turno.TieneVentanaDeEntrada(Horas(3)), true);
    }

    [Fact]
    public async Task ModificarVentanaDeEntrada_NoEmiteEventos_CuandoRepiteLaMismaAmplitud()
    {
        Given(Fijo(), VentanaDeEntradaModificada.Crear(GuidAggregateId, Horas(3)));
        await WhenAsync(Comando(GuidAggregateId, 3));
        Then();
        And<CatalogoTurnos, bool>(c => c.Turno.TieneVentanaDeEntrada(Horas(3)), true);
    }

    [Fact]
    public async Task ModificarVentanaDeEntrada_NoEmiteEventos_CuandoPideCeroSobreUnTurnoFijo()
    {
        Given(Fijo());
        await WhenAsync(Comando(GuidAggregateId, 0));
        Then();
        And<CatalogoTurnos, bool>(c => c.Turno.TieneVentanaDeEntrada(Horas(0)), true);
    }

    [Fact]
    public async Task ModificarVentanaDeEntrada_NoEmiteEventos_CuandoPideCeroSobreUnTurnoIncompleto()
    {
        Given(ConFranjas());
        await WhenAsync(Comando(GuidAggregateId, 0));
        Then();
        And<CatalogoTurnos, bool>(c => c.Turno.TieneVentanaDeEntrada(Horas(0)), true);
    }

    [Fact]
    public async Task ModificarVentanaDeEntrada_NoEmiteEventos_CuandoPideCeroSobreUnDescanso()
    {
        Given(TurnoCreado.CrearDescanso(GuidAggregateId, "Libre"));
        await WhenAsync(Comando(GuidAggregateId, 0));
        Then();
        And<CatalogoTurnos, bool>(c => c.Turno.TieneVentanaDeEntrada(Horas(0)), true);
    }

    [Fact]
    public async Task ModificarVentanaDeEntrada_LanzaReglaDeNegocioDeclinada_CuandoElTurnoFueRetirado()
    {
        Given(Fijo(), TurnoRetirado.Crear(GuidAggregateId));
        var act = async () => await WhenAsync(Comando(GuidAggregateId, 3));
        await act.Should().ThrowExactlyAsync<ReglaDeNegocioDeclinadaException>()
            .WithMessage($"*{ModificarVentanaDeEntradaCommandHandler.Mensajes.TurnoRetirado}*");
        Then();
        And<CatalogoTurnos, bool>(c => c.Turno.TieneVentanaDeEntrada(Horas(0)), true);
    }

    [Fact]
    public async Task ModificarVentanaDeEntrada_LanzaReglaDeNegocioDeclinada_CuandoElTurnoRetiradoYaTeniaLaAmplitud()
    {
        Given(Fijo(), TurnoRetirado.Crear(GuidAggregateId));
        var act = async () => await WhenAsync(Comando(GuidAggregateId, 0));
        await act.Should().ThrowExactlyAsync<ReglaDeNegocioDeclinadaException>()
            .WithMessage($"*{ModificarVentanaDeEntradaCommandHandler.Mensajes.TurnoRetirado}*");
        Then();
        And<CatalogoTurnos, bool>(c => c.Turno.TieneVentanaDeEntrada(Horas(0)), true);
    }

    [Fact]
    public async Task ModificarVentanaDeEntrada_LanzaReglaDeNegocioDeclinada_CuandoElTurnoEsDescansoYLaAmplitudEsMayorACero()
    {
        Given(TurnoCreado.CrearDescanso(GuidAggregateId, "Libre"));
        var act = async () => await WhenAsync(Comando(GuidAggregateId, 1));
        await act.Should().ThrowExactlyAsync<ReglaDeNegocioDeclinadaException>()
            .WithMessage($"*{ModificarVentanaDeEntradaCommandHandler.Mensajes.TurnoSinFranjas}*");
        Then();
        And<CatalogoTurnos, bool>(c => c.Turno.TieneVentanaDeEntrada(Horas(0)), true);
    }

    [Fact]
    public async Task ModificarVentanaDeEntrada_LanzaReglaDeNegocioDeclinada_CuandoElTurnoEstaIncompletoYLaAmplitudEsMayorACero()
    {
        Given(ConFranjas());
        var act = async () => await WhenAsync(Comando(GuidAggregateId, 1));
        await act.Should().ThrowExactlyAsync<ReglaDeNegocioDeclinadaException>()
            .WithMessage($"*{ModificarVentanaDeEntradaCommandHandler.Mensajes.TurnoSinFranjas}*");
        Then();
        And<CatalogoTurnos, bool>(c => c.Turno.TieneVentanaDeEntrada(Horas(0)), true);
    }

    [Fact]
    public async Task ModificarVentanaDeEntrada_LanzaReglaDeNegocioDeclinada_CuandoElInicioEsAnteriorALas0400()
    {
        Given(ConFranjas(Franja(3, 30, 12, 0)));
        var act = async () => await WhenAsync(Comando(GuidAggregateId, 1));
        await act.Should().ThrowExactlyAsync<ReglaDeNegocioDeclinadaException>()
            .WithMessage($"*{ModificarVentanaDeEntradaCommandHandler.Mensajes.InicioAntesDeHoraCorte}*");
        Then();
        And<CatalogoTurnos, bool>(c => c.Turno.TieneVentanaDeEntrada(Horas(0)), true);
    }

    [Fact]
    public async Task ModificarVentanaDeEntrada_LanzaReglaDeNegocioDeclinada_CuandoLaUltimaFranjaLlegariaALas2400()
    {
        Given(ConFranjas(Franja(6, 0, 10, 0), Franja(20, 0, 23, 0)));
        var act = async () => await WhenAsync(Comando(GuidAggregateId, 4));
        await act.Should().ThrowExactlyAsync<ReglaDeNegocioDeclinadaException>()
            .WithMessage($"*{ModificarVentanaDeEntradaCommandHandler.Mensajes.CierrePasaMedianoche}*");
        Then();
        And<CatalogoTurnos, bool>(c => c.Turno.TieneVentanaDeEntrada(Horas(0)), true);
    }

    [Fact]
    public async Task ModificarVentanaDeEntrada_EmiteVentanaDeEntradaModificada_CuandoLaUltimaFranjaQuedaUnMinutoAntesDeLas2400()
    {
        Given(ConFranjas(Franja(6, 0, 10, 0), Franja(20, 0, 23, 0)));
        await WhenAsync(Comando(GuidAggregateId, 3, 59));
        Then(VentanaDeEntradaModificada.Crear(GuidAggregateId, Horas(3, 59)));
        And<CatalogoTurnos, bool>(c => c.Turno.TieneVentanaDeEntrada(Horas(3, 59)), true);
    }

    [Fact]
    public async Task ModificarVentanaDeEntrada_LanzaRecursoNoEncontradoException_CuandoElTurnoNoExiste()
    {
        var act = async () => await WhenAsync(Comando(GuidAggregateId, 3));
        await act.Should().ThrowExactlyAsync<RecursoNoEncontradoException>()
            .WithMessage($"*{ModificarVentanaDeEntradaCommandHandler.Mensajes.TurnoNoEncontrado}*");
        Then(GuidAggregateId.ToString());
    }

    [Fact]
    public async Task ModificarVentanaDeEntrada_LanzaAggregateException_CuandoLosMinutosEstanFueraDeRango()
    {
        Given(Fijo());
        var act = async () => await WhenAsync(Comando(GuidAggregateId, 3, 60));
        var errores = (await act.Should().ThrowExactlyAsync<AggregateException>()).Which.InnerExceptions;
        errores.Select(e => e.Message).Should().Contain(HorasYMinutos.Mensajes.MinutosFueraDeRango);
        Then();
        And<CatalogoTurnos, bool>(c => c.Turno.TieneVentanaDeEntrada(Horas(0)), true);
    }

    [Fact]
    public async Task ModificarVentanaDeEntrada_NoEmiteEventos_CuandoLosPasosDeDisenoConservaronLaAmplitud()
    {
        Given(Fijo(), VentanaDeEntradaModificada.Crear(GuidAggregateId, Horas(3)),
            DescansoAgregado.Crear(GuidAggregateId,
                FranjaOrdinaria.Crear(new TimeOnly(7, 0), new TimeOnly(16, 0))
                    .ConDescanso(new TimeOnly(12, 0), new TimeOnly(12, 30))));
        await WhenAsync(Comando(GuidAggregateId, 3));
        Then();
        And<CatalogoTurnos, bool>(c => c.Turno.TieneVentanaDeEntrada(Horas(3)), true);
    }
}
