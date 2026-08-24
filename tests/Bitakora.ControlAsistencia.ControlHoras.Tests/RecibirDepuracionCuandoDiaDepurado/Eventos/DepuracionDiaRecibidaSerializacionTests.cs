// Issue #425: Test de serializacion roundtrip para DepuracionDiaRecibida.
// Requerido por regla 16: todo evento persistido en Marten debe sobrevivir Serialize -> Deserialize.

using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.RecibirDepuracionCuandoDiaDepurado.Eventos;

/// <summary>
/// Verifica que DepuracionDiaRecibida sobrevive un roundtrip de serializacion STJ con las opciones
/// reales de Marten (constructor privado + propiedades private set + tipos ricos anidados propios
/// de esta isla). Ver MEF-ADR-0012 y patron canonico en MarcacionAdicionadaSerializacionTests.
/// </summary>
public class DepuracionDiaRecibidaSerializacionTests
{
    private const string StreamId = "dc:EMP-001:20260315";
    private const string CodigoColaborador = "EMP-001";
    private static readonly DateOnly Fecha = new(2026, 3, 15);

    private static readonly ResumenColaborador Colaborador =
        new("CC-1234567890", CodigoColaborador, "Luis Augusto Barreto");

    private static readonly FranjaDepurada Franja = new(
        new TimeOnly(6, 0), new TimeOnly(14, 0), 0,
        new DateTime(2026, 3, 15, 6, 0, 0), new DateTime(2026, 3, 15, 14, 0, 0), false);

    private static readonly MarcacionDelDia Marcacion =
        new(new DateTime(2026, 3, 15, 6, 0, 0), "ENTRADA");

    private static HorasDiscriminadas HorasConDatos() => new(
        new Dictionary<string, decimal>
        {
            ["OrdinariaDiurna"] = 7.00m,
            ["Retardo"] = 0.25m
        },
        ["06:00-14:00 OrdinariaDiurna"]);

    // Regla 16: usa CrearOpcionesMarten() que registra ConfigurarSerializacion de todos los tipos.
    // DepuracionDiaRecibida debe registrarse en ConfiguracionSerializacionControlHoras.ConfigurarResolver.
    [Fact]
    public void Deserializar_ReconstruyeEvento_ConTodosLosCampos()
    {
        var evento = new DepuracionDiaRecibida(
            StreamId, CodigoColaborador, Fecha, Colaborador, "Turno Manana",
            [Franja], [Marcacion], HorasConDatos());
        var opciones = ConfiguracionSerializacionControlHoras.CrearOpcionesMarten();

        var json = JsonSerializer.Serialize(evento, opciones);
        var deserializado = JsonSerializer.Deserialize<DepuracionDiaRecibida>(json, opciones);

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

    // CA-3: dia sin jornada valida -- Colaborador/NombreTurno null, Franjas y HorasPorConcepto
    // vacios, Marcaciones cruda como unica evidencia.
    [Fact]
    public void Deserializar_ReconstruyeEvento_CuandoNoHayJornadaValida()
    {
        var evento = new DepuracionDiaRecibida(
            StreamId, CodigoColaborador, Fecha, null, null, [], [Marcacion],
            new HorasDiscriminadas(new Dictionary<string, decimal>(), []));
        var opciones = ConfiguracionSerializacionControlHoras.CrearOpcionesMarten();

        var json = JsonSerializer.Serialize(evento, opciones);
        var deserializado = JsonSerializer.Deserialize<DepuracionDiaRecibida>(json, opciones);

        deserializado.Should().NotBeNull();
        deserializado!.Colaborador.Should().BeNull();
        deserializado.NombreTurno.Should().BeNull();
        deserializado.Franjas.Should().BeEmpty();
        deserializado.Marcaciones.Should().Equal(Marcacion);
        deserializado.HorasDiscriminadas.HorasPorConcepto.Should().BeEmpty();
    }
}
