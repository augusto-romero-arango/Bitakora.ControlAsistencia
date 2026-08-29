// Issue #277 CA-1: IdentidadEventosControlHoras.TiposPersistidos debe listar exactamente los
// tipos que se persisten en el event store de ControlHoras -- ni de mas (ningun evento que solo
// cruce el bus, p.ej. DiaDepurado o ProgramacionTurnoDiarioSolicitada), ni de menos (el olvido
// que este issue corrige). MarcacionRegistrada SI entra: ademas de IPrivateEvent, se persiste en
// el stream de RegistroDeMarcacionAggregateRoot, que la aplica. No usa el harness Given/When/Then:
// se verifica un dato estatico de configuracion, no el comportamiento de un command handler.
// Issue #425: DepuracionDiaRecibida se persiste en el stream de DiaCalculadoAggregateRoot (CA-5)
// y se suma a la lista literal.
// Issue #463 CA-5: SedeDeMarcacionIdentificada se persiste en el stream de ControlDiarioAggregateRoot
// (estampado de sede sobre una marcacion ya adicionada) y se suma a la lista literal.

using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;
using Bitakora.ControlAsistencia.ControlHoras.Entities;
using Cosmos.EventSourcing.Abstractions;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.Infraestructura;

public class IdentidadEventosControlHorasTests
{
    [Fact]
    public void TiposPersistidos_ContieneExactamenteLosCincoEventosPersistidosDeControlHoras()
    {
        IdentidadEventosControlHoras.TiposPersistidos.Should().BeEquivalentTo(
        [
            typeof(MarcacionRegistrada),
            typeof(MarcacionAdicionada),
            typeof(TurnoDiarioAsignado),
            typeof(DepuracionDiaRecibida),
            typeof(SedeDeMarcacionIdentificada)
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
        IdentidadEventosControlHoras.TiposPersistidos.Should().Contain(eventosAplicados);
    }

    // Un aggregate root aplica un evento persistido con la firma convencional de Marten
    // "public void Apply(TEvento)", y ese evento vive en el ensamblado DomainEvents del dominio
    // (CA-ADR-0029). Los eventos que solo cruzan el bus llegan por un endpoint, no por un Apply,
    // asi que quedan fuera por construccion.
    private static IReadOnlyList<Type> EventosAplicadosPorLosAggregateRoots() =>
        typeof(ControlDiarioAggregateRoot).Assembly.GetTypes()
            .Where(tipo => typeof(AggregateRoot).IsAssignableFrom(tipo))
            .SelectMany(tipo => tipo.GetMethods())
            .Where(metodo => metodo.Name == nameof(RegistroDeMarcacionAggregateRoot.Apply)
                             && metodo.GetParameters().Length == 1)
            .Select(metodo => metodo.GetParameters()[0].ParameterType)
            .Where(evento => evento.Assembly == typeof(MarcacionRegistrada).Assembly)
            .Distinct()
            .ToList();
}
