// Issue #330 (gemela de AliasEventosControlHorasTests/AliasEventosProgramacionTests): congela el
// alias que Marten deriva del nombre de clase de cada evento persistido de Colaboradores
// (JasperFx.Events.EventTypeExtensions -> eventType.Name.ToTableAlias()). Es la garantia central de
// MEF-ADR-0036: si el alias resuelve, EventDocumentStorage.Resolve nunca cae al fallback por
// mt_dotnet_type. Este test hace visible el dia en que un rename de clase rompa la convencion,
// ANTES de desplegarlo.
//
// No necesita Postgres: EventGraph.AllKnownEventTypes() es calculo puro en memoria sobre un
// StoreOptions standalone.

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

    // Rojo esperado (fase roja, issue #330): IdentidadEventosColaboradores.TiposPersistidos sigue
    // vacio hasta que el implementer registre ColaboradorRegistrado/VinculacionIniciada -- el
    // implementer los agrega ahi (no aqui).
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

    // Issue #349 (gemela de las dos pruebas anteriores): congela el alias de VinculacionTerminada,
    // segundo evento persistido de la vinculacion (terminar). Rojo esperado (fase roja, issue
    // #349): IdentidadEventosColaboradores.TiposPersistidos sigue sin VinculacionTerminada hasta
    // que el implementer lo registre -- el implementer lo agrega ahi (no aqui, MEF-ADR-0002).
    [Fact]
    public void VinculacionTerminada_TieneAliasVinculacionTerminada()
    {
        var options = CrearOpcionesConEventosDeColaboradoresRegistrados();

        AliasDe<VinculacionTerminada>(options).Should().Be("vinculacion_terminada");
    }

    // Issue #351 (gemela de las tres pruebas anteriores): congela el alias de NombresCorregidos,
    // cuarto evento persistido de ColaboradorAggregateRoot (corregir nombres). Rojo esperado (fase
    // roja, issue #351): IdentidadEventosColaboradores.TiposPersistidos sigue sin NombresCorregidos
    // hasta que el implementer lo registre -- el implementer lo agrega ahi (no aqui, MEF-ADR-0002).
    [Fact]
    public void NombresCorregidos_TieneAliasNombresCorregidos()
    {
        var options = CrearOpcionesConEventosDeColaboradoresRegistrados();

        AliasDe<NombresCorregidos>(options).Should().Be("nombres_corregidos");
    }

    // Issue #352 (gemela de las cuatro pruebas anteriores): congela el alias de
    // FechaInicioVinculacionCorregida, quinto evento persistido de ColaboradorAggregateRoot
    // (corregir la fecha de inicio de la ultima vinculacion). Rojo esperado (fase roja, issue
    // #352): IdentidadEventosColaboradores.TiposPersistidos sigue sin
    // FechaInicioVinculacionCorregida hasta que el implementer lo registre -- el implementer lo
    // agrega ahi (no aqui, MEF-ADR-0002).
    [Fact]
    public void FechaInicioVinculacionCorregida_TieneAliasFechaInicioVinculacionCorregida()
    {
        var options = CrearOpcionesConEventosDeColaboradoresRegistrados();

        AliasDe<FechaInicioVinculacionCorregida>(options).Should().Be("fecha_inicio_vinculacion_corregida");
    }

    // Issue #354 (gemela de las cinco pruebas anteriores): congela el alias de TerminacionAnulada,
    // sexto evento persistido de ColaboradorAggregateRoot (anular la terminacion de la ultima
    // vinculacion). El alias es la identidad del evento en mt_events (CA-ADR-0029 decision #6): un
    // rename futuro de la clase lo cambiaria en silencio, y este literal es quien lo delata.
    [Fact]
    public void TerminacionAnulada_TieneAliasTerminacionAnulada()
    {
        var options = CrearOpcionesConEventosDeColaboradoresRegistrados();

        AliasDe<TerminacionAnulada>(options).Should().Be("terminacion_anulada");
    }
}
