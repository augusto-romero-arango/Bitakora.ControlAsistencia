using System.Linq;
using AwesomeAssertions;
using Marten;

namespace Bitakora.ControlAsistencia.Projections.Tests.Infraestructura;

/// <summary>
/// Issue #277 CA-4: hermano read-side del helper del mismo nombre en Programacion.Tests /
/// ControlHoras.Tests. Detecta un evento persistido que quedo fuera del registro explicito de
/// IdentidadEventos{Dominio}.TiposPersistidos en el named store del worker de proyecciones. Sin
/// AddEventTypes, la primera lectura de un stream con datos preexistentes cae al fallback por
/// mt_dotnet_type y revienta con UnknownEventTypeException (issue #237 seccion "Consecuencia
/// asumida"). No necesita Postgres: AllKnownEventTypes() es calculo en memoria, igual que el
/// resto de guardas de AssertsProyecciones.
/// </summary>
public static class AssertsIdentidadEventos
{
    public static void AssertEventosPersistidosRegistrados(
        this IDocumentStore store, IReadOnlyList<Type> tiposEsperados) =>
        store.Options.Events.AllKnownEventTypes()
            .Select(evento => evento.EventType)
            .Should().Contain(tiposEsperados);
}
