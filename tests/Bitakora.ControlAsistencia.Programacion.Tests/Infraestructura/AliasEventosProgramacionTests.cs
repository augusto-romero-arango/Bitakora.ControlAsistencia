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
// conexion), asi que el alias se interroga sin contenedor DI ni base de datos.

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

    // Issue #500
    [Fact]
    public void TurnoRetirado_TieneAliasTurnoRetirado()
    {
        var options = CrearOpcionesConEventosDeProgramacionRegistrados();

        AliasDe<TurnoRetirado>(options).Should().Be("turno_retirado");
    }

    [Fact]
    public void CancelacionProgramacionSolicitada_TieneAliasCancelacionProgramacionSolicitada()
    {
        var options = CrearOpcionesConEventosDeProgramacionRegistrados();

        AliasDe<CancelacionProgramacionSolicitada>(options)
            .Should().Be("cancelacion_programacion_solicitada");
    }

    // Issue #602
    [Fact]
    public void FranjaAgregada_TieneAliasFranjaAgregada()
    {
        var options = CrearOpcionesConEventosDeProgramacionRegistrados();

        AliasDe<FranjaAgregada>(options).Should().Be("franja_agregada");
    }
}
