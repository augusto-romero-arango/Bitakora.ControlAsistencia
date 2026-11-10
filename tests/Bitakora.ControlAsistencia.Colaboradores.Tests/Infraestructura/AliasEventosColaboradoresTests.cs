// Congela el alias que Marten deriva del nombre de clase de cada evento persistido: es su identidad
// en mt_events y un rename de clase lo cambiaria en silencio. Sin Postgres: AllKnownEventTypes() es
// calculo en memoria.

using AwesomeAssertions;
using Bitakora.ControlAsistencia.Colaboradores.DomainEvents;
using Marten;

namespace Bitakora.ControlAsistencia.Colaboradores.Tests.Infraestructura;

public class AliasEventosColaboradoresTests
{
    private static StoreOptions CrearOpcionesConEventosDeColaboradoresRegistrados()
    {
        var options = new StoreOptions();
        options.Events.AddEventTypes(IdentidadEventosColaboradores.TiposPersistidos);
        return options;
    }

    private static string? AliasDe<TEvento>(StoreOptions options) =>
        ((IReadOnlyStoreOptions)options).Events.AllKnownEventTypes()
            .SingleOrDefault(evento => evento.EventType == typeof(TEvento))
            ?.Alias;

    [Fact]
    public void ColaboradorRegistrado_TieneAliasColaboradorRegistrado()
    {
        var options = CrearOpcionesConEventosDeColaboradoresRegistrados();

        AliasDe<ColaboradorRegistrado>(options).Should().Be("colaborador_registrado");
    }

    [Fact]
    public void VinculacionIniciada_TieneAliasVinculacionIniciada()
    {
        var options = CrearOpcionesConEventosDeColaboradoresRegistrados();

        AliasDe<VinculacionIniciada>(options).Should().Be("vinculacion_iniciada");
    }

    [Fact]
    public void VinculacionTerminada_TieneAliasVinculacionTerminada()
    {
        var options = CrearOpcionesConEventosDeColaboradoresRegistrados();

        AliasDe<VinculacionTerminada>(options).Should().Be("vinculacion_terminada");
    }

    [Fact]
    public void NombresCorregidos_TieneAliasNombresCorregidos()
    {
        var options = CrearOpcionesConEventosDeColaboradoresRegistrados();

        AliasDe<NombresCorregidos>(options).Should().Be("nombres_corregidos");
    }

    [Fact]
    public void FechaInicioVinculacionCorregida_TieneAliasFechaInicioVinculacionCorregida()
    {
        var options = CrearOpcionesConEventosDeColaboradoresRegistrados();

        AliasDe<FechaInicioVinculacionCorregida>(options).Should().Be("fecha_inicio_vinculacion_corregida");
    }

    [Fact]
    public void TerminacionAnulada_TieneAliasTerminacionAnulada()
    {
        var options = CrearOpcionesConEventosDeColaboradoresRegistrados();

        AliasDe<TerminacionAnulada>(options).Should().Be("terminacion_anulada");
    }

    [Fact]
    public void EtiquetaAsignada_TieneAliasEtiquetaAsignada()
    {
        var options = CrearOpcionesConEventosDeColaboradoresRegistrados();

        AliasDe<EtiquetaAsignada>(options).Should().Be("etiqueta_asignada");
    }

    [Fact]
    public void EtiquetaRetirada_TieneAliasEtiquetaRetirada()
    {
        var options = CrearOpcionesConEventosDeColaboradoresRegistrados();

        AliasDe<EtiquetaRetirada>(options).Should().Be("etiqueta_retirada");
    }

    [Fact]
    public void SedeAsignada_TieneAliasSedeAsignada()
    {
        var options = CrearOpcionesConEventosDeColaboradoresRegistrados();

        AliasDe<SedeAsignada>(options).Should().Be("sede_asignada");
    }

    [Fact]
    public void JornadaAsignada_TieneAliasJornadaAsignada()
    {
        var options = CrearOpcionesConEventosDeColaboradoresRegistrados();

        AliasDe<JornadaAsignada>(options).Should().Be("jornada_asignada");
    }
}
