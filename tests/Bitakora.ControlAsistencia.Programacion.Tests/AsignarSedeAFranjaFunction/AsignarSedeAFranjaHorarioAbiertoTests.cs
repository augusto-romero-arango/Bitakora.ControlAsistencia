using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.AsignarSedeAFranjaFunction;
using Bitakora.ControlAsistencia.Programacion.AsignarSedeAFranjaFunction.CommandHandler;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.Programacion.Entities;
using Bitakora.ControlAsistencia.Programacion.Tests.CrearTurnoDeHorarioAbiertoFunction;
using Cosmos.EventSourcing.Abstractions.Commands;
using Cosmos.EventSourcing.Testing.Utilities;

namespace Bitakora.ControlAsistencia.Programacion.Tests.AsignarSedeAFranjaFunction;

public class AsignarSedeAFranjaHorarioAbiertoTests : CommandHandlerAsyncTest<AsignarSedeAFranja>
{
    private static readonly Guid TurnoId = TurnoDeHorarioAbiertoEventos.TurnoId;
    private static readonly SedeProgramada Sede = new("SEDE-OCOA", "Ocoa");

    protected override ICommandHandlerAsync<AsignarSedeAFranja> Handler =>
        new AsignarSedeAFranjaCommandHandler(EventStore, PrivateEventSender);

    [Fact]
    public async Task AsignarSedeAFranja_EmiteSedeDeFranjaAsignada_CuandoElTurnoEsDeHorarioAbierto()
    {
        Given(TurnoId.ToString(), TurnoDeHorarioAbiertoEventos.Creado(), TurnoDeHorarioAbiertoEventos.Ventana());

        await WhenAsync(new AsignarSedeAFranja(TurnoId, new TimeOnly(4, 0), Sede));

        Then(TurnoId.ToString(), SedeDeFranjaAsignada.Crear(TurnoId,
            FranjaOrdinaria.Crear(new TimeOnly(4, 0), new TimeOnly(10, 0), sede: Sede)));
        And<CatalogoTurnos, SedeProgramada?>(TurnoId.ToString(),
            c => c.ObtenerDetalle().FranjasOrdinarias.Single().Sede, Sede);
    }

    [Fact]
    public async Task AsignarSedeAFranja_EmiteSedeDeFranjaRetirada_CuandoElTurnoEsDeHorarioAbierto()
    {
        Given(TurnoId.ToString(), TurnoDeHorarioAbiertoEventos.Creado(), TurnoDeHorarioAbiertoEventos.Ventana(),
            SedeDeFranjaAsignada.Crear(TurnoId,
                FranjaOrdinaria.Crear(new TimeOnly(4, 0), new TimeOnly(10, 0), sede: Sede)));

        await WhenAsync(new AsignarSedeAFranja(TurnoId, new TimeOnly(4, 0), null));

        Then(TurnoId.ToString(), SedeDeFranjaRetirada.Crear(TurnoId,
            FranjaOrdinaria.Crear(new TimeOnly(4, 0), new TimeOnly(10, 0))));
        And<CatalogoTurnos, SedeProgramada?>(TurnoId.ToString(),
            c => c.ObtenerDetalle().FranjasOrdinarias.Single().Sede, null);
    }
}
