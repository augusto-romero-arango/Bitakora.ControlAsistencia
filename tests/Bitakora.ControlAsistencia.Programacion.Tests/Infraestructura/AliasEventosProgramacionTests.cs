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

    // Issue #603
    [Fact]
    public void DescansoAgregado_TieneAliasDescansoAgregado()
    {
        var options = CrearOpcionesConEventosDeProgramacionRegistrados();

        AliasDe<DescansoAgregado>(options).Should().Be("descanso_agregado");
    }

    [Fact]
    public void ExtraAgregado_TieneAliasExtraAgregado()
    {
        var options = CrearOpcionesConEventosDeProgramacionRegistrados();

        AliasDe<ExtraAgregado>(options).Should().Be("extra_agregado");
    }

    [Fact]
    public void FranjaQuitada_TieneAliasFranjaQuitada()
    {
        var options = CrearOpcionesConEventosDeProgramacionRegistrados();

        AliasDe<FranjaQuitada>(options).Should().Be("franja_quitada");
    }

    [Fact]
    public void DescansoQuitado_TieneAliasDescansoQuitado()
    {
        var options = CrearOpcionesConEventosDeProgramacionRegistrados();

        AliasDe<DescansoQuitado>(options).Should().Be("descanso_quitado");
    }

    [Fact]
    public void ExtraQuitado_TieneAliasExtraQuitado()
    {
        var options = CrearOpcionesConEventosDeProgramacionRegistrados();

        AliasDe<ExtraQuitado>(options).Should().Be("extra_quitado");
    }

    // Issue #606
    [Fact]
    public void SedeDeFranjaAsignada_TieneAliasSedeDeFranjaAsignada()
    {
        var options = CrearOpcionesConEventosDeProgramacionRegistrados();

        AliasDe<SedeDeFranjaAsignada>(options).Should().Be("sede_de_franja_asignada");
    }

    [Fact]
    public void SedeDeFranjaRetirada_TieneAliasSedeDeFranjaRetirada()
    {
        var options = CrearOpcionesConEventosDeProgramacionRegistrados();

        AliasDe<SedeDeFranjaRetirada>(options).Should().Be("sede_de_franja_retirada");
    }

    [Fact]
    public void PlantillaSemanalCreada_TieneAliasPlantillaSemanalCreada()
    {
        var options = CrearOpcionesConEventosDeProgramacionRegistrados();

        AliasDe<PlantillaSemanalCreada>(options).Should().Be("plantilla_semanal_creada");
    }

    // Issue #621
    [Fact]
    public void DiaDePlantillaSemanalAsignado_TieneAliasDiaDePlantillaSemanalAsignado()
    {
        var options = CrearOpcionesConEventosDeProgramacionRegistrados();

        AliasDe<DiaDePlantillaSemanalAsignado>(options).Should().Be("dia_de_plantilla_semanal_asignado");
    }

    // Issue #622
    [Fact]
    public void DiaDePlantillaSemanalQuitado_TieneAliasDiaDePlantillaSemanalQuitado()
    {
        var options = CrearOpcionesConEventosDeProgramacionRegistrados();

        AliasDe<DiaDePlantillaSemanalQuitado>(options).Should().Be("dia_de_plantilla_semanal_quitado");
    }

    [Fact]
    public void PlantillaSemanalRetirada_TieneAliasPlantillaSemanalRetirada()
    {
        var options = CrearOpcionesConEventosDeProgramacionRegistrados();

        AliasDe<PlantillaSemanalRetirada>(options).Should().Be("plantilla_semanal_retirada");
    }
}
