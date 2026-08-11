// Issue #330 (gemela de AliasEventosControlHorasTests/AliasEventosProgramacionTests): congela el
// alias que Marten deriva del nombre de clase de cada evento persistido de Colaboradores
// (JasperFx.Events.EventTypeExtensions -> eventType.Name.ToTableAlias()). Es la garantia central de
// MEF-ADR-0036: si el alias resuelve, EventDocumentStorage.Resolve nunca cae al fallback por
// mt_dotnet_type. Este test hace visible el dia en que un rename de clase rompa la convencion,
// ANTES de desplegarlo.
//
// No necesita Postgres: EventGraph.AllKnownEventTypes() es calculo puro en memoria sobre un
// StoreOptions standalone.

using AwesomeAssertions;
using Bitakora.ControlAsistencia.Colaboradores.DomainEvents;
using Marten;

namespace Bitakora.ControlAsistencia.Colaboradores.Tests.Infraestructura;

public class AliasEventosColaboradoresTests
{
    private static StoreOptions CrearOpcionesConEventosDeColaboradoresRegistrados()
    {
        var options = new StoreOptions();
        options.Events.AddEventTypes(IdentidadEventosColaboradores.TiposPersistidos);
        return options;
    }

    private static string? AliasDe<TEvento>(StoreOptions options) =>
        ((IReadOnlyStoreOptions)options).Events.AllKnownEventTypes()
            .SingleOrDefault(evento => evento.EventType == typeof(TEvento))
            ?.Alias;

    // Rojo esperado (fase roja, issue #330): IdentidadEventosColaboradores.TiposPersistidos sigue
    // vacio hasta que el implementer registre ColaboradorRegistrado/VinculacionIniciada -- el
    // implementer los agrega ahi (no aqui).
    [Fact]
    public void ColaboradorRegistrado_TieneAliasColaboradorRegistrado()
    {
        var options = CrearOpcionesConEventosDeColaboradoresRegistrados();

        AliasDe<ColaboradorRegistrado>(options).Should().Be("colaborador_registrado");
    }

    [Fact]
    public void VinculacionIniciada_TieneAliasVinculacionIniciada()
    {
        var options = CrearOpcionesConEventosDeColaboradoresRegistrados();

        AliasDe<VinculacionIniciada>(options).Should().Be("vinculacion_iniciada");
    }
}
