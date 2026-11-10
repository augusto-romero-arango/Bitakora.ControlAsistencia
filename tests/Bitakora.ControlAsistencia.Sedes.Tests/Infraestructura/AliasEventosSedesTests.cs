// Congela el alias que Marten deriva del nombre de clase de cada evento persistido de Sedes: si el
// alias resuelve, nunca se cae al fallback por mt_dotnet_type (MEF-ADR-0036). Un rename de clase que
// rompa la convencion se ve aqui, antes de desplegarlo. Sin Postgres: AllKnownEventTypes() es
// calculo en memoria.

using AwesomeAssertions;
using Bitakora.ControlAsistencia.Sedes.DomainEvents;
using Marten;

namespace Bitakora.ControlAsistencia.Sedes.Tests.Infraestructura;

public class AliasEventosSedesTests
{
    private static StoreOptions CrearOpcionesConEventosDeSedesRegistrados()
    {
        var options = new StoreOptions();
        options.Events.AddEventTypes(IdentidadEventosSedes.TiposPersistidos);
        return options;
    }

    private static string? AliasDe<TEvento>(StoreOptions options) =>
        ((IReadOnlyStoreOptions)options).Events.AllKnownEventTypes()
            .SingleOrDefault(evento => evento.EventType == typeof(TEvento))
            ?.Alias;

    [Fact]
    public void SedeRegistrada_TieneAliasSedeRegistrada()
    {
        var options = CrearOpcionesConEventosDeSedesRegistrados();

        AliasDe<SedeRegistrada>(options).Should().Be("sede_registrada");
    }

    [Fact]
    public void NombreSedeModificado_TieneAliasNombreSedeModificado()
    {
        var options = CrearOpcionesConEventosDeSedesRegistrados();

        AliasDe<NombreSedeModificado>(options).Should().Be("nombre_sede_modificado");
    }

    [Fact]
    public void UbicacionActualizada_TieneAliasUbicacionActualizada()
    {
        var options = CrearOpcionesConEventosDeSedesRegistrados();

        AliasDe<UbicacionActualizada>(options).Should().Be("ubicacion_actualizada");
    }

    [Fact]
    public void CentroDeCostosAsignado_TieneAliasCentroDeCostosAsignado()
    {
        var options = CrearOpcionesConEventosDeSedesRegistrados();

        AliasDe<CentroDeCostosAsignado>(options).Should().Be("centro_de_costos_asignado");
    }

    [Fact]
    public void CentroDeCostosRetirado_TieneAliasCentroDeCostosRetirado()
    {
        var options = CrearOpcionesConEventosDeSedesRegistrados();

        AliasDe<CentroDeCostosRetirado>(options).Should().Be("centro_de_costos_retirado");
    }

    [Fact]
    public void SedeActivada_TieneAliasSedeActivada()
    {
        var options = CrearOpcionesConEventosDeSedesRegistrados();

        AliasDe<SedeActivada>(options).Should().Be("sede_activada");
    }

    [Fact]
    public void SedeDesactivada_TieneAliasSedeDesactivada()
    {
        var options = CrearOpcionesConEventosDeSedesRegistrados();

        AliasDe<SedeDesactivada>(options).Should().Be("sede_desactivada");
    }

    [Fact]
    public void DispositivoInstalado_TieneAliasDispositivoInstalado()
    {
        var options = CrearOpcionesConEventosDeSedesRegistrados();

        AliasDe<DispositivoInstalado>(options).Should().Be("dispositivo_instalado");
    }

    [Fact]
    public void DispositivoRetirado_TieneAliasDispositivoRetirado()
    {
        var options = CrearOpcionesConEventosDeSedesRegistrados();

        AliasDe<DispositivoRetirado>(options).Should().Be("dispositivo_retirado");
    }
}
