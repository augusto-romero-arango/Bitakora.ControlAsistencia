// Congela el alias que Marten deriva del nombre de clase de cada evento persistido
// (eventType.Name.ToTableAlias(), verificado por decompilacion contra Marten 9.12 + JasperFx.Events
// 2.18.1): si el alias resuelve, EventDocumentStorage.Resolve nunca cae al fallback por
// mt_dotnet_type (issue #237). Un rename de clase que rompa la convencion se ve aqui antes de desplegar.
//
// Sin Postgres: EventGraph.AllKnownEventTypes() es calculo en memoria sobre un StoreOptions standalone
// (verificado por decompilacion: el constructor de StoreOptions no abre conexion).

using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;
using Marten;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.Infraestructura;

public class AliasEventosControlHorasTests
{
    private static StoreOptions CrearOpcionesConEventosDeControlHorasRegistrados()
    {
        var options = new StoreOptions();
        options.Events.AddEventTypes(IdentidadEventosControlHoras.TiposPersistidos);
        return options;
    }

    private static string? AliasDe<TEvento>(StoreOptions options) =>
        ((IReadOnlyStoreOptions)options).Events.AllKnownEventTypes()
            .SingleOrDefault(evento => evento.EventType == typeof(TEvento))
            ?.Alias;

    [Fact]
    public void MarcacionRegistrada_TieneAliasMarcacionRegistrada()
    {
        var options = CrearOpcionesConEventosDeControlHorasRegistrados();

        AliasDe<MarcacionRegistrada>(options).Should().Be("marcacion_registrada");
    }

    [Fact]
    public void MarcacionAdicionada_TieneAliasMarcacionAdicionada()
    {
        var options = CrearOpcionesConEventosDeControlHorasRegistrados();

        AliasDe<MarcacionAdicionada>(options).Should().Be("marcacion_adicionada");
    }

    [Fact]
    public void TurnoDiarioAsignado_TieneAliasTurnoDiarioAsignado()
    {
        var options = CrearOpcionesConEventosDeControlHorasRegistrados();

        AliasDe<TurnoDiarioAsignado>(options).Should().Be("turno_diario_asignado");
    }

    [Fact]
    public void DepuracionDiaRecibida_TieneAliasDepuracionDiaRecibida()
    {
        var options = CrearOpcionesConEventosDeControlHorasRegistrados();

        AliasDe<DepuracionDiaRecibida>(options).Should().Be("depuracion_dia_recibida");
    }

    [Fact]
    public void SedeDeMarcacionIdentificada_TieneAliasSedeDeMarcacionIdentificada()
    {
        var options = CrearOpcionesConEventosDeControlHorasRegistrados();

        AliasDe<SedeDeMarcacionIdentificada>(options).Should().Be("sede_de_marcacion_identificada");
    }

    [Fact]
    public void DiaAprobado_TieneAliasDiaAprobado()
    {
        var options = CrearOpcionesConEventosDeControlHorasRegistrados();

        AliasDe<DiaAprobado>(options).Should().Be("dia_aprobado");
    }

    [Fact]
    public void DepuracionPosAprobacionRecibida_TieneAliasDepuracionPosAprobacionRecibida()
    {
        var options = CrearOpcionesConEventosDeControlHorasRegistrados();

        AliasDe<DepuracionPosAprobacionRecibida>(options).Should().Be("depuracion_pos_aprobacion_recibida");
    }

    // Congela el alias de TurnoDiarioCancelado: cambiarlo rompe la lectura de los streams ya escritos
    // (MEF-ADR-0036).
    [Fact]
    public void TurnoDiarioCancelado_TieneAliasTurnoDiarioCancelado()
    {
        var options = CrearOpcionesConEventosDeControlHorasRegistrados();

        AliasDe<TurnoDiarioCancelado>(options).Should().Be("turno_diario_cancelado");
    }

    [Fact]
    public void AusenciaDiariaAsignada_TieneAliasAusenciaDiariaAsignada()
    {
        var options = CrearOpcionesConEventosDeControlHorasRegistrados();

        AliasDe<AusenciaDiariaAsignada>(options).Should().Be("ausencia_diaria_asignada");
    }

    [Fact]
    public void CancelacionAusenciaDiariaRegistrada_TieneAliasCancelacionAusenciaDiariaRegistrada()
    {
        var options = CrearOpcionesConEventosDeControlHorasRegistrados();

        AliasDe<CancelacionAusenciaDiariaRegistrada>(options).Should().Be("cancelacion_ausencia_diaria_registrada");
    }
}
