using AwesomeAssertions;
using Marten;

namespace Bitakora.ControlAsistencia.Colaboradores.Tests.Infraestructura;

public static class AssertsIdentidadEventos
{
    // Sin AddEventTypes, la primera rehidratacion de un stream con datos preexistentes cae al fallback
    // por mt_dotnet_type y lanza UnknownEventTypeException.
    public static void AssertEventosPersistidosRegistrados(
        this IDocumentStore store, IReadOnlyList<Type> tiposEsperados) =>
        store.Options.Events.AllKnownEventTypes()
            .Select(evento => evento.EventType)
            .Should().Contain(tiposEsperados);

    /// <summary>
    /// Congela el alias (columna "type" de mt_events) sobre el store que compuso el contenedor:
    /// AliasEventosColaboradoresTests usa un StoreOptions standalone y no veria un MapEventType o un
    /// EventNamingStyle introducido en ComposicionServicios.
    /// </summary>
    public static void AssertAliasDeEventosPersistidos(
        this IDocumentStore store, IReadOnlyDictionary<Type, string> aliasEsperados) =>
        store.Options.Events.AllKnownEventTypes()
            .Where(evento => aliasEsperados.ContainsKey(evento.EventType))
            .ToDictionary(evento => evento.EventType, evento => evento.Alias)
            .Should().Equal(aliasEsperados);
}
