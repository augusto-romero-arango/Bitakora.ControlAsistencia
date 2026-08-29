using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.PrivateEvents.Colaboradores;
using Bitakora.ControlAsistencia.PrivateEvents.ControlHoras;

namespace Bitakora.ControlAsistencia.PrivateEvents.Tests.ControlHoras;

/// <summary>
/// Verifica que DiaDepurado (IPrivateEvent) sobrevive el cruce fisico del ASB interno del BC
/// (MEF-ADR-0024 decision #3). Nunca se persiste (IdentidadEventosControlHoras lo excluye
/// explicitamente), asi que no hay round-trip de Marten que cubrir (seccion 6d del test-writer):
/// solo el canal del bus (seccion 6e), donde la expectativa es que el payload SOBREVIVA sin
/// resolver custom -- si no sobreviviera, el tipo no seria portable (MEF-ADR-0023).
///
/// Este test compila referenciando UNICAMENTE PrivateEvents (CA-ADR-0029 seccion 3: si necesitara
/// mas, el tipo no seria portable).
///
/// Issue #424: el payload se enriquece con NombreTurno, Franjas y Marcaciones; HorasDiscriminadas
/// habla horas liquidables (decimal), no minutos.
///
/// Issue #464 (CA-5): Franja/Marcacion llevan ademas los seis campos planos de sede (programada y
/// marcada) -- portables por ser primitivos, mismo criterio del resto del payload.
/// </summary>
public class DiaDepuradoTests
{
    private static JsonSerializerOptions CrearOpcionesDelBus() =>
        new(JsonSerializerDefaults.Web);

    private static readonly DateOnly Fecha = new(2026, 3, 15);

    private static HorasDiscriminadas HorasConDatos() => new(
        new Dictionary<string, decimal>
        {
            ["DominicalFestivaDiurna"] = 7.00m,
            ["ExtraDiurnaDominicalFestiva"] = 0.50m,
            ["Retardo"] = 0.25m
        },
        ["06:00-14:00: Dominical festiva diurna"]);

    private static FranjaDepurada FranjaConDatos() => new(
        new TimeOnly(6, 0), new TimeOnly(14, 0), 0,
        new DateTime(2026, 3, 15, 7, 0, 0), new DateTime(2026, 3, 15, 15, 0, 0), false,
        "001", "Sede Principal", "CC-100");

    private static readonly MarcacionDelDia MarcacionEntrada =
        new(new DateTime(2026, 3, 15, 7, 0, 0), "ENTRADA", "001", "Sede Principal", "CC-100");
    private static readonly MarcacionDelDia MarcacionSalida =
        new(new DateTime(2026, 3, 15, 15, 0, 0), "SALIDA", "002", "Sede Bodega", null);

    // CA-3/CA-4/CA-7: round-trip con el serializador POR DEFECTO del bus preserva todo el payload
    // enriquecido, incluidas Franjas y Marcaciones.
    [Fact]
    public void RoundTrip_PreservaTodosLosCampos_ConSerializadorPorDefectoDelBus()
    {
        var colaborador = new ResumenColaborador("CC-1234567890", "EMP-001", "Luis Augusto Barreto");
        var evento = new DiaDepurado(
            "EMP-001", Fecha, colaborador, "Turno Manana",
            [FranjaConDatos()], [MarcacionEntrada, MarcacionSalida], HorasConDatos());
        var opciones = CrearOpcionesDelBus();

        var json = JsonSerializer.Serialize(evento, opciones);
        var restaurado = JsonSerializer.Deserialize<DiaDepurado>(json, opciones);

        restaurado.Should().NotBeNull();
        restaurado.Should().Be(evento);
    }

    // CA-4/CA-5/CA-6: sin turno previo, Colaborador y NombreTurno viajan null, Franjas queda vacia,
    // pero las marcaciones crudas siguen viajando y CodigoColaborador top-level sigue presente.
    [Fact]
    public void RoundTrip_PreservaCodigoColaboradorTopLevelYMarcacionesCrudas_CuandoNoHayJornadaValida()
    {
        var evento = new DiaDepurado(
            "EMP-002", Fecha, null, null, [], [MarcacionEntrada],
            new HorasDiscriminadas(new Dictionary<string, decimal>(), []));
        var opciones = CrearOpcionesDelBus();

        var json = JsonSerializer.Serialize(evento, opciones);
        var restaurado = JsonSerializer.Deserialize<DiaDepurado>(json, opciones);

        restaurado.Should().NotBeNull();
        restaurado!.CodigoColaborador.Should().Be("EMP-002");
        restaurado.Colaborador.Should().BeNull();
        restaurado.NombreTurno.Should().BeNull();
        restaurado.Franjas.Should().BeEmpty();
        restaurado.Marcaciones.Should().Equal(MarcacionEntrada);
    }
}
