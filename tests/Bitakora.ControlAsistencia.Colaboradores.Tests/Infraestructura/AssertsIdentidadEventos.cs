// Issue #330 (gemela exacta de AssertsIdentidadEventos de ControlHoras.Tests/Programacion.Tests):
// detecta un evento persistido que quedo fuera del registro explicito de
// IdentidadEventosColaboradores.TiposPersistidos. Sin AddEventTypes, la primera rehidratacion de un
// stream con datos preexistentes cae al fallback por mt_dotnet_type y revienta con
// UnknownEventTypeException. Se invoca sobre el IDocumentStore resuelto del contenedor real de este
// dominio (ComposicionServiciosTests) -- sin Postgres: AllKnownEventTypes() es calculo en memoria.

using AwesomeAssertions;
using Marten;

namespace Bitakora.ControlAsistencia.Colaboradores.Tests.Infraestructura;

public static class AssertsIdentidadEventos
{
    public static void AssertEventosPersistidosRegistrados(
        this IDocumentStore store, IReadOnlyList<Type> tiposEsperados) =>
        store.Options.Events.AllKnownEventTypes()
            .Select(evento => evento.EventType)
            .Should().Contain(tiposEsperados);

    /// <summary>
    /// Congela el alias -- la columna "type" de mt_events, la unica identidad que Marten consulta
    /// antes del fallback -- sobre el store que realmente compuso el contenedor.
    /// AliasEventosColaboradoresTests ya lo congela sobre un StoreOptions standalone, pero ahi no
    /// vive el wiring: un MapEventType o un EventNamingStyle introducidos en ComposicionServicios
    /// cambiarian la identidad de eventos ya persistidos sin poner rojo ningun otro test.
    /// </summary>
    public static void AssertAliasDeEventosPersistidos(
        this IDocumentStore store, IReadOnlyDictionary<Type, string> aliasEsperados) =>
        store.Options.Events.AllKnownEventTypes()
            .Where(evento => aliasEsperados.ContainsKey(evento.EventType))
            .ToDictionary(evento => evento.EventType, evento => evento.Alias)
            .Should().Equal(aliasEsperados);
}
