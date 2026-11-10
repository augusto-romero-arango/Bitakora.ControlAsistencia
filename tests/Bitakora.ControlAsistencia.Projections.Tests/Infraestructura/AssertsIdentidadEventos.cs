using AwesomeAssertions;
using Marten;

namespace Bitakora.ControlAsistencia.Projections.Tests.Infraestructura;

/// <summary>
/// Detecta un evento persistido fuera de IdentidadEventos{Dominio}.TiposPersistidos en el named store
/// del worker: sin AddEventTypes, leer un stream con datos preexistentes cae al fallback por
/// mt_dotnet_type y revienta con UnknownEventTypeException. No necesita Postgres.
/// </summary>
public static class AssertsIdentidadEventos
{
    public static void AssertEventosPersistidosRegistrados(
        this IDocumentStore store, IReadOnlyList<Type> tiposEsperados) =>
        store.Options.Events.AllKnownEventTypes()
            .Select(evento => evento.EventType)
            .Should().Contain(tiposEsperados);
}
