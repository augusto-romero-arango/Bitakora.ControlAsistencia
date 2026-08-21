// CA-6/CA-7: el payload completo de DiaDepurado, con datos realistas del dominio (incluyendo Franjas
// y Marcaciones, issue #424), round-trip serializa/deserializa con el serializador POR DEFECTO del
// publisher (sin el resolver custom de Marten) y sin perdida. Complementa el guardrail minimo de
// portabilidad de PrivateEvents.Tests/ControlHoras/DiaDepuradoTests, que compila contra la isla sola.
//
// La asercion es deliberadamente la OPUESTA a la del test legado, que afirmaba que la
// deserializacion FALLABA sin resolver custom (Retardo tenia ctor privado): si alguien reintroduce
// un tipo rico al payload, este test se pone rojo.

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
        new DateTime(2026, 3, 15, 7, 0, 0), new DateTime(2026, 3, 15, 15, 0, 0), false);

    private static readonly MarcacionDelDia Marcacion =
        new(new DateTime(2026, 3, 15, 7, 0, 0), "ENTRADA");

    // Payload con datos reales: varias claves de concepto y la clave literal "Retardo", ya en horas
    // liquidables (issue #424).
    private static HorasDiscriminadas HorasConDatos() => new(
        new Dictionary<string, decimal>
        {
            ["DominicalFestivaDiurna"] = 7.00m,
            ["ExtraDiurnaDominicalFestiva"] = 0.50m,
            ["Retardo"] = 0.25m
        },
        []);

    // El serializador POR DEFECTO del publisher: sin TypeInfoResolver custom, como en el canal de
    // publicacion a Service Bus (no se aplica ConfigurarSerializacion de Marten). Mismo objeto de
    // opciones para serializar y deserializar -> round-trip sin perdida si el payload es primitivo.
    private static JsonSerializerOptions OpcionesPorDefectoDelPublisher() => new();

    // CA-6/CA-7: round-trip preserva todos los campos con el serializador por defecto (sin resolver
    // custom), incluidas Franjas y Marcaciones.
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

    // CA-4/CA-5/CA-6: roundtrip con Colaborador y NombreTurno null (dia sin jornada valida, caso
    // "marcacion sin turno previo"), Franjas vacia pero CodigoColaborador top-level SIEMPRE presente
    // -- el defecto latente que #421 corrigio -- y la marcacion cruda preservada.
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

    // CA-6 (barrera anti-regresion invertida): con el resolver POR DEFECTO sin ningun
    // ConfigurarSerializacion registrado, la deserializacion DEBE tener exito -- el payload es 100%
    // primitivo. Si alguien reintroduce un tipo de dominio rico (ctor/campos privados), volveria a
    // necesitar el resolver custom y este test fallaria con NotSupportedException.
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
}
