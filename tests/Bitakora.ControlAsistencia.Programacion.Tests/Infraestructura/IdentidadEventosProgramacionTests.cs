// Issue #277 CA-1: IdentidadEventosProgramacion.TiposPersistidos debe listar exactamente los
// tipos que se persisten en el event store de Programacion -- ni de mas (ningun evento de bus),
// ni de menos (el olvido que este issue corrige). No usa el harness Given/When/Then: se verifica un
// dato estatico de configuracion -- el que consumen ComposicionServicios (write-side) y
// ConfiguracionMartenProjectionsProgramacion (read-side) --, no el comportamiento de un handler.

using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.Programacion.Entities;
using Cosmos.EventSourcing.Abstractions;

namespace Bitakora.ControlAsistencia.Programacion.Tests.Infraestructura;

public class IdentidadEventosProgramacionTests
{
    // Issue #602: FranjaAgregada se persiste en el mismo stream de CatalogoTurnos -- debe
    // listarse aqui igual que TurnoCreado/TurnoRetirado.
    [Fact]
    public void TiposPersistidos_ContieneExactamenteLosCincoEventosPersistidosDeProgramacion()
    {
        IdentidadEventosProgramacion.TiposPersistidos.Should().BeEquivalentTo(
        [
            typeof(TurnoCreado),
            typeof(ProgramacionTurnoSolicitada),
            typeof(TurnoRetirado),
            typeof(CancelacionProgramacionSolicitada),
            typeof(FranjaAgregada)
        ]);
    }

    // Oraculo derivado del write-side, complementario del literal de arriba: la asercion literal
    // congela la lista de hoy, pero un evento persistido que se agregue manana solo la pone roja si
    // alguien recuerda editar el test -- justo el olvido que el #237 dejo armado. Esta guarda no
    // necesita ese recuerdo: enumera por reflexion los eventos que los aggregate roots del dominio
    // aplican al rehidratar y exige que todos esten registrados.
    [Fact]
    public void TiposPersistidos_IncluyeTodoEventoQueAplicaUnAggregateRootDelDominio()
    {
        var eventosAplicados = EventosAplicadosPorLosAggregateRoots();

        eventosAplicados.Should().NotBeEmpty();
        IdentidadEventosProgramacion.TiposPersistidos.Should().Contain(eventosAplicados);
    }

    // Un aggregate root aplica un evento persistido con la firma convencional de Marten
    // "public void Apply(TEvento)", y ese evento vive en el ensamblado DomainEvents del dominio
    // (CA-ADR-0029). Los eventos que solo cruzan el bus llegan por un endpoint, no por un Apply,
    // asi que quedan fuera por construccion.
    private static IReadOnlyList<Type> EventosAplicadosPorLosAggregateRoots() =>
        typeof(CatalogoTurnos).Assembly.GetTypes()
            .Where(tipo => typeof(AggregateRoot).IsAssignableFrom(tipo))
            .SelectMany(tipo => tipo.GetMethods())
            .Where(metodo => metodo.Name == nameof(CatalogoTurnos.Apply)
                             && metodo.GetParameters().Length == 1)
            .Select(metodo => metodo.GetParameters()[0].ParameterType)
            .Where(evento => evento.Assembly == typeof(TurnoCreado).Assembly)
            .Distinct()
            .ToList();
}
