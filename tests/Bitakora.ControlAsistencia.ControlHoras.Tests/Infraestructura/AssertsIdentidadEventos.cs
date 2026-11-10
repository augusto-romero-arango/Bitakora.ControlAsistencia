using AwesomeAssertions;
using Marten;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.Infraestructura;

/// <summary>
/// Detecta un evento persistido fuera de IdentidadEventos{Dominio}.TiposPersistidos: sin AddEventTypes,
/// la primera rehidratacion de un stream con datos preexistentes cae al fallback por mt_dotnet_type y
/// revienta con UnknownEventTypeException. Sin Postgres: AllKnownEventTypes() es calculo en memoria.
/// </summary>
public static class AssertsIdentidadEventos
{
    public static void AssertEventosPersistidosRegistrados(
        this IDocumentStore store, IReadOnlyList<Type> tiposEsperados) =>
        store.Options.Events.AllKnownEventTypes()
            .Select(evento => evento.EventType)
            .Should().Contain(tiposEsperados);

    /// <summary>
    /// Congela el alias (la columna "type" de mt_events, la unica identidad que Marten consulta antes del
    /// fallback) sobre el store que compuso el contenedor: AliasEventos{Dominio}Tests usa un StoreOptions
    /// standalone, y un MapEventType o un EventNamingStyle en ComposicionServicios cambiaria la identidad
    /// de eventos ya persistidos sin poner rojo ningun otro test.
    /// </summary>
    public static void AssertAliasDeEventosPersistidos(
        this IDocumentStore store, IReadOnlyDictionary<Type, string> aliasEsperados) =>
        store.Options.Events.AllKnownEventTypes()
            .Where(evento => aliasEsperados.ContainsKey(evento.EventType))
            .ToDictionary(evento => evento.EventType, evento => evento.Alias)
            .Should().Equal(aliasEsperados);
}
