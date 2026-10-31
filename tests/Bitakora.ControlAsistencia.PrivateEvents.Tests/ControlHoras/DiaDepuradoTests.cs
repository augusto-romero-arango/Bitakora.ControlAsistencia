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

    [Fact]
    public void RoundTrip_PreservaMotivoAusencia_ConSerializadorPorDefectoDelBus()
    {
        var colaborador = new ResumenColaborador("CC-1234567890", "EMP-001", "Luis Augusto Barreto");
        var evento = new DiaDepurado(
            "EMP-001", Fecha, colaborador, null, [], [MarcacionEntrada],
            new HorasDiscriminadas(new Dictionary<string, decimal>(), []), "Vacaciones");
        var opciones = CrearOpcionesDelBus();

        var json = JsonSerializer.Serialize(evento, opciones);
        var restaurado = JsonSerializer.Deserialize<DiaDepurado>(json, opciones);

        restaurado.Should().Be(evento);
    }

    // Evolucion aditiva: un DiaDepurado publicado antes de MotivoAusencia sigue deserializando.
    [Fact]
    public void Deserializar_DejaMotivoAusenciaNulo_CuandoElPayloadNoLoTrae()
    {
        var opciones = CrearOpcionesDelBus();
        var json = JsonSerializer.Serialize(new DiaDepurado(
            "EMP-001", Fecha, null, "Turno Manana", [], [],
            new HorasDiscriminadas(new Dictionary<string, decimal>(), [])), opciones)
            .Replace(",\"motivoAusencia\":null", "");

        var restaurado = JsonSerializer.Deserialize<DiaDepurado>(json, opciones);

        json.Should().NotContain("motivoAusencia");
        restaurado!.MotivoAusencia.Should().BeNull();
        restaurado.NombreTurno.Should().Be("Turno Manana");
    }

    [Fact]
    public void RoundTrip_PreservaVersionControlDiario_ConSerializadorPorDefectoDelBus()
    {
        var evento = new DiaDepurado(
            "EMP-001", Fecha, null, "Turno Manana", [], [],
            new HorasDiscriminadas(new Dictionary<string, decimal>(), []),
            VersionControlDiario: 12);
        var opciones = CrearOpcionesDelBus();

        var json = JsonSerializer.Serialize(evento, opciones);
        var restaurado = JsonSerializer.Deserialize<DiaDepurado>(json, opciones);

        restaurado.Should().Be(evento);
        restaurado!.VersionControlDiario.Should().Be(12L);
    }

    [Fact]
    public void Deserializar_DejaVersionControlDiarioNula_CuandoElPayloadNoLaTrae()
    {
        var opciones = CrearOpcionesDelBus();
        var json = JsonSerializer.Serialize(new DiaDepurado(
            "EMP-001", Fecha, null, "Turno Manana", [], [],
            new HorasDiscriminadas(new Dictionary<string, decimal>(), [])), opciones)
            .Replace(",\"versionControlDiario\":null", "");

        var restaurado = JsonSerializer.Deserialize<DiaDepurado>(json, opciones);

        json.Should().NotContain("versionControlDiario");
        restaurado!.VersionControlDiario.Should().BeNull();
        restaurado.NombreTurno.Should().Be("Turno Manana");
    }
}
