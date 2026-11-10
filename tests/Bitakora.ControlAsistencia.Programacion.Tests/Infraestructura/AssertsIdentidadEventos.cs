using AwesomeAssertions;
using Marten;

namespace Bitakora.ControlAsistencia.Programacion.Tests.Infraestructura;

/// <summary>
/// Detecta un evento persistido que quedo fuera del registro explicito de
/// IdentidadEventos{Dominio}.TiposPersistidos: sin AddEventTypes, la primera rehidratacion de un
/// stream con datos preexistentes cae al fallback por mt_dotnet_type y revienta con
/// UnknownEventTypeException. Se invoca sobre el IDocumentStore del contenedor real del dominio,
/// sin Postgres: AllKnownEventTypes() es calculo en memoria (Marten 7+ no abre conexion en el
/// bootstrap del IHost).
/// </summary>
public static class AssertsIdentidadEventos
{
    public static void AssertEventosPersistidosRegistrados(
        this IDocumentStore store, IReadOnlyList<Type> tiposEsperados) =>
        store.Options.Events.AllKnownEventTypes()
            .Select(evento => evento.EventType)
            .Should().Contain(tiposEsperados);

    /// <summary>
    /// Congela el alias -- la columna "type" de mt_events, la unica
    /// identidad que Marten consulta antes del fallback -- sobre el store que realmente compuso el
    /// contenedor. AliasEventos{Dominio}Tests ya lo congela sobre un StoreOptions standalone, pero
    /// ahi no vive el wiring: un MapEventType o un EventNamingStyle introducidos en
    /// ComposicionServicios cambiarian la identidad de eventos ya persistidos sin poner rojo ningun
    /// otro test.
    /// </summary>
    public static void AssertAliasDeEventosPersistidos(
        this IDocumentStore store, IReadOnlyDictionary<Type, string> aliasEsperados) =>
        store.Options.Events.AllKnownEventTypes()
            .Where(evento => aliasEsperados.ContainsKey(evento.EventType))
            .ToDictionary(evento => evento.EventType, evento => evento.Alias)
            .Should().Equal(aliasEsperados);
}
