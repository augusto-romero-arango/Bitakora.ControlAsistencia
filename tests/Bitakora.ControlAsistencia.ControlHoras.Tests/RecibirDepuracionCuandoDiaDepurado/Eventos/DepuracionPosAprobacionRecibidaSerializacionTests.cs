// Issue #491: test de serializacion roundtrip para DepuracionPosAprobacionRecibida (regla 16 /
// seccion 6d del test-writer). Misma forma que DepuracionDiaRecibida -- mismo patron de prueba,
// ver DepuracionDiaRecibidaSerializacionTests.

using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.RecibirDepuracionCuandoDiaDepurado.Eventos;

public class DepuracionPosAprobacionRecibidaSerializacionTests
{
    private const string StreamId = "dc:EMP-001:20260315";
    private const string CodigoColaborador = "EMP-001";
    private static readonly DateOnly Fecha = new(2026, 3, 15);

    private static readonly ResumenColaborador Colaborador =
        new("CC-1234567890", CodigoColaborador, "Luis Augusto Barreto");

    private static readonly FranjaDepurada Franja = new(
        new TimeOnly(6, 0), new TimeOnly(14, 0), 0,
        new DateTime(2026, 3, 15, 6, 0, 0), new DateTime(2026, 3, 15, 14, 0, 0), false,
        "SEDE-01", "Sede Principal", "CC-100");

    private static readonly MarcacionDelDia Marcacion =
        new(new DateTime(2026, 3, 15, 6, 0, 0), "ENTRADA", "SEDE-02", "Sede Norte", "CC-200");

    private static HorasDiscriminadas HorasConDatos() => new(
        new Dictionary<string, decimal> { ["OrdinariaDiurna"] = 7.00m },
        ["06:00-14:00 OrdinariaDiurna"]);

    // Regla 16: usa CrearOpcionesMarten(), las opciones reales que registra el dominio (no un
    // resolver armado inline que solo conozca este tipo).
    [Fact]
    public void Deserializar_ReconstruyeEvento_ConTodosLosCampos()
    {
        var evento = new DepuracionPosAprobacionRecibida(
            StreamId, CodigoColaborador, Fecha, Colaborador, "Turno Manana",
            [Franja], [Marcacion], HorasConDatos());
        var opciones = ConfiguracionSerializacionControlHoras.CrearOpcionesMarten();

        var json = JsonSerializer.Serialize(evento, opciones);
        var deserializado = JsonSerializer.Deserialize<DepuracionPosAprobacionRecibida>(json, opciones);

        deserializado.Should().NotBeNull();
        deserializado!.Id.Should().Be(StreamId);
        deserializado.CodigoColaborador.Should().Be(CodigoColaborador);
        deserializado.Fecha.Should().Be(Fecha);
        deserializado.Colaborador.Should().Be(Colaborador);
        deserializado.NombreTurno.Should().Be("Turno Manana");
        deserializado.Franjas.Should().Equal(Franja);
        deserializado.Marcaciones.Should().Equal(Marcacion);
        deserializado.HorasDiscriminadas.Should().Be(HorasConDatos());
    }

    // CA-1: la evidencia tardia de un dia sin jornada valida -- Colaborador/NombreTurno null,
    // Franjas y HorasPorConcepto vacios, igual que DepuracionDiaRecibida en el mismo escenario.
    [Fact]
    public void Deserializar_ReconstruyeEvento_CuandoNoHayJornadaValida()
    {
        var evento = new DepuracionPosAprobacionRecibida(
            StreamId, CodigoColaborador, Fecha, null, null, [], [Marcacion],
            new HorasDiscriminadas(new Dictionary<string, decimal>(), []));
        var opciones = ConfiguracionSerializacionControlHoras.CrearOpcionesMarten();

        var json = JsonSerializer.Serialize(evento, opciones);
        var deserializado = JsonSerializer.Deserialize<DepuracionPosAprobacionRecibida>(json, opciones);

        deserializado.Should().NotBeNull();
        deserializado!.Colaborador.Should().BeNull();
        deserializado.NombreTurno.Should().BeNull();
        deserializado.Franjas.Should().BeEmpty();
        deserializado.Marcaciones.Should().Equal(Marcacion);
        deserializado.HorasDiscriminadas.HorasPorConcepto.Should().BeEmpty();
    }
}
