// Issue #277 CA-5: congela el alias que Marten deriva del nombre de clase de cada evento
// persistido de Programacion (JasperFx.Events.EventTypeExtensions.GetSmarterEventTypeName /
// GetEventTypeName -> eventType.Name.ToTableAlias(), verificado por decompilacion contra Marten
// 9.12 + JasperFx.Events 2.18.1: ambos estilos de naming resuelven igual para tipos top-level no
// genericos como los cinco eventos persistidos de este BC). Es la garantia central del fix: si
// el alias resuelve, EventDocumentStorage.Resolve nunca cae al fallback por mt_dotnet_type que
// rompio el issue #237. Este test hace visible el dia en que un rename de clase rompa la
// convencion, ANTES de desplegarlo.
//
// No necesita Postgres: EventGraph.AllKnownEventTypes() es calculo puro en memoria sobre un
// StoreOptions standalone (verificado por decompilacion: el constructor de StoreOptions no abre
// conexion). Se alimenta de IdentidadEventosProgramacion.TiposPersistidos -- mismo stub que CA-1,
// asi que en fase roja (lista vacia) este test tambien falla: AllKnownEventTypes() no encuentra
// el tipo y el alias sale null.
//
// No se usan [Theory]/[InlineData] (MEF-ADR de convencion de tests de este agente: solo [Fact]):
// un Fact por evento, en vez de la forma parametrizada que sugiere el issue.

using System.Linq;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Marten;

namespace Bitakora.ControlAsistencia.Programacion.Tests.Infraestructura;

public class AliasEventosProgramacionTests
{
    private static StoreOptions CrearOpcionesConEventosDeProgramacionRegistrados()
    {
        var options = new StoreOptions();
        options.Events.AddEventTypes(IdentidadEventosProgramacion.TiposPersistidos);
        return options;
    }

    private static string? AliasDe<TEvento>(StoreOptions options) =>
        ((IReadOnlyStoreOptions)options).Events.AllKnownEventTypes()
            .SingleOrDefault(evento => evento.EventType == typeof(TEvento))
            ?.Alias;

    [Fact]
    public void TurnoCreado_TieneAliasTurnoCreado()
    {
        var options = CrearOpcionesConEventosDeProgramacionRegistrados();

        AliasDe<TurnoCreado>(options).Should().Be("turno_creado");
    }

    [Fact]
    public void ProgramacionTurnoSolicitada_TieneAliasProgramacionTurnoSolicitada()
    {
        var options = CrearOpcionesConEventosDeProgramacionRegistrados();

        AliasDe<ProgramacionTurnoSolicitada>(options).Should().Be("programacion_turno_solicitada");
    }
}
