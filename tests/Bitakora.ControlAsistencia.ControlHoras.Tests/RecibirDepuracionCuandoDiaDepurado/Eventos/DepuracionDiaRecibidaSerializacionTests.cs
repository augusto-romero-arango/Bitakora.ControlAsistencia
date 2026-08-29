// Issue #425: Test de serializacion roundtrip para DepuracionDiaRecibida.
// Issue #484: cubre ademas el roundtrip de la sede programada/marcada anadida a FranjaDepurada y
// MarcacionDelDia (DomainEvents).
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

    // Issue #484: cada record anidado se prueba en sus dos formas -- con sede y sin ella.
    private static readonly FranjaDepurada Franja = new(
        new TimeOnly(6, 0), new TimeOnly(14, 0), 0,
        new DateTime(2026, 3, 15, 6, 0, 0), new DateTime(2026, 3, 15, 14, 0, 0), false,
        "SEDE-01", "Sede Principal", "CC-100");

    private static readonly FranjaDepurada FranjaSinSedeProgramada = new(
        new TimeOnly(6, 0), new TimeOnly(14, 0), 0,
        new DateTime(2026, 3, 15, 6, 0, 0), new DateTime(2026, 3, 15, 14, 0, 0), false);

    private static readonly MarcacionDelDia Marcacion =
        new(new DateTime(2026, 3, 15, 6, 0, 0), "ENTRADA",
            "SEDE-02", "Sede Norte", "CC-200");

    private static readonly MarcacionDelDia MarcacionSinSedeEstampada =
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

    // CA-4 (#484): el roundtrip conserva la sede PROGRAMADA de la franja y la sede MARCADA de la
    // marcacion -- los 6 campos nuevos que introduce el issue.
    [Fact]
    public void Deserializar_ReconstruyeEvento_ConSedeProgramadaYSedeMarcada()
    {
        var evento = new DepuracionDiaRecibida(
            StreamId, CodigoColaborador, Fecha, Colaborador, "Turno Manana",
            [Franja], [Marcacion], HorasConDatos());
        var opciones = ConfiguracionSerializacionControlHoras.CrearOpcionesMarten();

        var json = JsonSerializer.Serialize(evento, opciones);
        var deserializado = JsonSerializer.Deserialize<DepuracionDiaRecibida>(json, opciones);

        deserializado.Should().NotBeNull();
        var franja = deserializado!.Franjas.Single();
        franja.CodigoSedeProgramada.Should().Be("SEDE-01");
        franja.NombreSedeProgramada.Should().Be("Sede Principal");
        franja.CentroDeCostosProgramado.Should().Be("CC-100");
        var marcacion = deserializado.Marcaciones.Single();
        marcacion.CodigoSede.Should().Be("SEDE-02");
        marcacion.NombreSede.Should().Be("Sede Norte");
        marcacion.CentroDeCostos.Should().Be("CC-200");
    }

    // CA-3 (#425): dia sin jornada valida -- Colaborador/NombreTurno null, Franjas y HorasPorConcepto
    // vacios, Marcaciones cruda como unica evidencia (sin sede estampada).
    [Fact]
    public void Deserializar_ReconstruyeEvento_CuandoNoHayJornadaValida()
    {
        var evento = new DepuracionDiaRecibida(
            StreamId, CodigoColaborador, Fecha, null, null, [], [MarcacionSinSedeEstampada],
            new HorasDiscriminadas(new Dictionary<string, decimal>(), []));
        var opciones = ConfiguracionSerializacionControlHoras.CrearOpcionesMarten();

        var json = JsonSerializer.Serialize(evento, opciones);
        var deserializado = JsonSerializer.Deserialize<DepuracionDiaRecibida>(json, opciones);

        deserializado.Should().NotBeNull();
        deserializado!.Colaborador.Should().BeNull();
        deserializado.NombreTurno.Should().BeNull();
        deserializado.Franjas.Should().BeEmpty();
        deserializado.Marcaciones.Should().Equal(MarcacionSinSedeEstampada);
        deserializado.HorasDiscriminadas.HorasPorConcepto.Should().BeEmpty();
    }

    // CA-3 (#484): franja/marcacion sin sede (null en los 3 campos) viajan null aunque el resto del
    // dia si tenga jornada valida -- el flujo actual de #425 no se altera.
    [Fact]
    public void Deserializar_PreservaSedesNulas_CuandoLaFranjaYLaMarcacionNoTraenSede()
    {
        var evento = new DepuracionDiaRecibida(
            StreamId, CodigoColaborador, Fecha, Colaborador, "Turno Manana",
            [FranjaSinSedeProgramada], [MarcacionSinSedeEstampada], HorasConDatos());
        var opciones = ConfiguracionSerializacionControlHoras.CrearOpcionesMarten();

        var json = JsonSerializer.Serialize(evento, opciones);
        var deserializado = JsonSerializer.Deserialize<DepuracionDiaRecibida>(json, opciones);

        deserializado.Should().NotBeNull();
        var franja = deserializado!.Franjas.Single();
        franja.CodigoSedeProgramada.Should().BeNull();
        franja.NombreSedeProgramada.Should().BeNull();
        franja.CentroDeCostosProgramado.Should().BeNull();
        var marcacion = deserializado.Marcaciones.Single();
        marcacion.CodigoSede.Should().BeNull();
        marcacion.NombreSede.Should().BeNull();
        marcacion.CentroDeCostos.Should().BeNull();
    }
}
