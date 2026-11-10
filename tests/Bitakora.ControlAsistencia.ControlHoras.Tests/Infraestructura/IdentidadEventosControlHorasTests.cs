// IdentidadEventosControlHoras.TiposPersistidos debe incluir todo tipo que se persiste en el event
// store de ControlHoras. Sin harness Given/When/Then: verifica configuracion estatica.

using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;
using Bitakora.ControlAsistencia.ControlHoras.Entities;
using Cosmos.EventSourcing.Abstractions;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.Infraestructura;

public class IdentidadEventosControlHorasTests
{
    // Oraculo derivado por reflexion: no depende de que alguien recuerde editar una lista literal al
    // agregar un evento persistido.
    [Fact]
    public void TiposPersistidos_IncluyeTodoEventoQueAplicaUnAggregateRootDelDominio()
    {
        var eventosAplicados = EventosAplicadosPorLosAggregateRoots();

        eventosAplicados.Should().NotBeEmpty();
        IdentidadEventosControlHoras.TiposPersistidos.Should().Contain(eventosAplicados);
    }

    // Firma convencional de Marten "public void Apply(TEvento)", filtrada al ensamblado DomainEvents:
    // los eventos que solo cruzan el bus llegan por un endpoint, no por un Apply.
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
