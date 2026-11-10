// El payload viaja por Service Bus con el serializador por defecto, sin el resolver custom de Marten.

using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.PrivateEvents.ControlHoras;
using Bitakora.ControlAsistencia.PrivateEvents.Colaboradores;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.AdicionarMarcacionCuandoRegistroDeMarcacionCreado.Eventos;

public class DiaDepuradoSerializacionTests
{
    private static readonly ResumenColaborador Colaborador =
        new("CC-1234567890", "EMP-001", "Luis Augusto Barreto");

    private static readonly DateOnly Fecha = new(2026, 3, 15);

    private static readonly FranjaDepurada Franja = new(
        new TimeOnly(6, 0), new TimeOnly(14, 0), 0,
        new DateTime(2026, 3, 15, 7, 0, 0), new DateTime(2026, 3, 15, 15, 0, 0), false,
        "001", "Sede Principal", "CC-100");

    private static readonly MarcacionDelDia Marcacion =
        new(new DateTime(2026, 3, 15, 7, 0, 0), "ENTRADA", "001", "Sede Principal", "CC-100");

    // Valores ya en horas liquidables, con la clave literal "Retardo".
    private static HorasDiscriminadas HorasConDatos() => new(
        new Dictionary<string, decimal>
        {
            ["DominicalFestivaDiurna"] = 7.00m,
            ["ExtraDiurnaDominicalFestiva"] = 0.50m,
            ["Retardo"] = 0.25m
        },
        []);

    private static JsonSerializerOptions OpcionesPorDefectoDelPublisher() => new();

    [Fact]
    public void RoundTrip_PreservaTodosLosCampos_ConSerializadorPorDefectoDelPublisher()
    {
        var original = new DiaDepurado(
            "EMP-001", Fecha, Colaborador, "Turno Manana", [Franja], [Marcacion], HorasConDatos());
        var opciones = OpcionesPorDefectoDelPublisher();

        var json = JsonSerializer.Serialize(original, opciones);
        var restaurado = JsonSerializer.Deserialize<DiaDepurado>(json, opciones);

        restaurado.Should().NotBeNull();
        restaurado!.CodigoColaborador.Should().Be("EMP-001");
        restaurado.Colaborador.Should().Be(Colaborador);
        restaurado.Fecha.Should().Be(Fecha);
        restaurado.NombreTurno.Should().Be("Turno Manana");
        restaurado.Franjas.Should().Equal(Franja);
        restaurado.Marcaciones.Should().Equal(Marcacion);
        restaurado.HorasDiscriminadas.HorasPorConcepto.Should().BeEquivalentTo(
            original.HorasDiscriminadas.HorasPorConcepto);
        restaurado.HorasDiscriminadas.Trazabilidad.Should().BeEmpty();
    }

    [Fact]
    public void RoundTrip_PreservaCampos_CuandoNoHayJornadaValida()
    {
        var original = new DiaDepurado(
            "EMP-002", Fecha, null, null, [], [Marcacion],
            new HorasDiscriminadas(new Dictionary<string, decimal>(), []));
        var opciones = OpcionesPorDefectoDelPublisher();

        var json = JsonSerializer.Serialize(original, opciones);
        var restaurado = JsonSerializer.Deserialize<DiaDepurado>(json, opciones);

        restaurado.Should().NotBeNull();
        restaurado!.CodigoColaborador.Should().Be("EMP-002");
        restaurado.Colaborador.Should().BeNull();
        restaurado.NombreTurno.Should().BeNull();
        restaurado.Fecha.Should().Be(Fecha);
        restaurado.Franjas.Should().BeEmpty();
        restaurado.Marcaciones.Should().Equal(Marcacion);
        restaurado.HorasDiscriminadas.HorasPorConcepto.Should().BeEmpty();
    }

    // Si alguien reintroduce un tipo de dominio rico (ctor o campos privados), volveria a necesitar el
    // resolver custom y este test fallaria.
    [Fact]
    public void Deserializar_TieneExito_ConResolverPorDefectoSinRegistros()
    {
        var original = new DiaDepurado(
            "EMP-001", Fecha, Colaborador, "Turno Manana", [Franja], [Marcacion], HorasConDatos());
        var json = JsonSerializer.Serialize(original);

        var opcionesSinRegistros = new JsonSerializerOptions
        {
            TypeInfoResolver = new DefaultJsonTypeInfoResolver()
        };

        var act = () => JsonSerializer.Deserialize<DiaDepurado>(json, opcionesSinRegistros);

        act.Should().NotThrow();
        act()!.HorasDiscriminadas.HorasPorConcepto["DominicalFestivaDiurna"].Should().Be(7.00m);
    }

    [Fact]
    public void RoundTrip_PreservaLaPertenencia_ConSerializadorPorDefectoDelPublisher()
    {
        var original = new DiaDepurado(
            "EMP-001", Fecha, null, "Turno Manana", [],
            [
                new MarcacionDelDia(new DateTime(2026, 3, 15, 4, 30, 0), "ENTRADA", Pertenencia: "Ambigua"),
                new MarcacionDelDia(new DateTime(2026, 3, 15, 6, 0, 0), "ENTRADA", Pertenencia: "DelTurno")
            ],
            new HorasDiscriminadas(new Dictionary<string, decimal>(), []));
        var opciones = OpcionesPorDefectoDelPublisher();

        var json = JsonSerializer.Serialize(original, opciones);
        var restaurado = JsonSerializer.Deserialize<DiaDepurado>(json, opciones);

        restaurado!.Marcaciones.Select(m => m.Pertenencia).Should().Equal("Ambigua", "DelTurno");
    }
}
