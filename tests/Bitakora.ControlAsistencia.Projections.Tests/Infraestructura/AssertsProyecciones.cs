using AwesomeAssertions;
using Marten;

namespace Bitakora.ControlAsistencia.Projections.Tests.Infraestructura;

/// <summary>
/// Helper reutilizable del config-test del worker (MEF-ADR-0034 seccion 6, guarda 3): verifica que
/// el named store resuelto replique exactamente la configuracion de metadata de evento que exige el
/// write-side de ese mismo dominio (MEF-ADR-0034 seccion 7). Invocar sobre cada named store que
/// projection-test-writer (issue #365) resuelva del contenedor: store.AssertOpcionesDeEvento().
/// </summary>
public static class AssertsProyecciones
{
    public static void AssertOpcionesDeEvento(this IDocumentStore store)
    {
        var metadata = store.Options.Events.MetadataConfig;

        metadata.CorrelationIdEnabled.Should().BeTrue();
        metadata.CausationIdEnabled.Should().BeTrue();
        metadata.HeadersEnabled.Should().BeTrue();
    }
}
