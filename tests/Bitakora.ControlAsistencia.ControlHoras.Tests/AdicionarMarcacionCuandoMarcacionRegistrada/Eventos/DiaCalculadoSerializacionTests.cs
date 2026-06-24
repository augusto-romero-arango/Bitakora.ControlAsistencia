// Issue #183: Reemplazar el payload de DiaCalculado por HorasDiscriminadas plano.
// CA-6: el payload completo de DiaCalculado round-trip serializa/deserializa con el serializador POR
//       DEFECTO del publisher (sin el resolver custom de Marten) y sin perdida. Esa es la cura del bug
//       (field notes 2026-06-23): el payload ya no contiene tipos de dominio ricos con campos privados
//       que solo serializaban con ConfigurarSerializacion -- resolver que NO se aplica al canal de
//       publicacion a Service Bus. Con solo primitivos, ningun consumidor depende de esa serializacion.
//
// Inversion de la barrera anterior: el test legado afirmaba que la deserializacion FALLABA sin el
// resolver custom (DetalleRetardo tenia ctor privado). Ahora se afirma lo OPUESTO: tiene EXITO con el
// resolver por defecto. Si alguien reintroduce un tipo rico al payload, este test fallaria.

using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Contracts.ControlHoras.Eventos;
using Bitakora.ControlAsistencia.Contracts.ControlHoras.ValueObjects;
using Bitakora.ControlAsistencia.Contracts.Empleados.ValueObjects;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.AdicionarMarcacionCuandoMarcacionRegistrada.Eventos;

public class DiaCalculadoSerializacionTests
{
    private static readonly InformacionEmpleado Empleado =
        new("EMP-001", "CC", "1234567890", "Luis Augusto", "Barreto");

    private static readonly DateOnly Fecha = new(2026, 3, 15);

    // Payload con datos reales: varias claves de concepto y la clave literal "Retardo".
    private static HorasDiscriminadas HorasConDatos() => new(
        new Dictionary<string, int>
        {
            ["DominicalFestivaDiurna"] = 420,
            ["ExtraDiurnaDominicalFestiva"] = 30,
            ["Retardo"] = 15
        },
        []);

    // El serializador POR DEFECTO del publisher: sin TypeInfoResolver custom, como en el canal de
    // publicacion a Service Bus (no se aplica ConfigurarSerializacion de Marten). Mismo objeto de
    // opciones para serializar y deserializar -> round-trip sin perdida si el payload es primitivo.
    private static JsonSerializerOptions OpcionesPorDefectoDelPublisher() => new();

    // CA-6: round-trip preserva todos los campos con el serializador por defecto (sin resolver custom).
    [Fact]
    public void RoundTrip_PreservaTodosLosCampos_ConSerializadorPorDefectoDelPublisher()
    {
        var original = new DiaCalculado(Empleado, Fecha, HorasConDatos());
        var opciones = OpcionesPorDefectoDelPublisher();

        var json = JsonSerializer.Serialize(original, opciones);
        var restaurado = JsonSerializer.Deserialize<DiaCalculado>(json, opciones);

        restaurado.Should().NotBeNull();
        restaurado!.InformacionEmpleado.Should().Be(Empleado);
        restaurado.Fecha.Should().Be(Fecha);
        restaurado.HorasDiscriminadas.MinutosPorConcepto.Should().BeEquivalentTo(
            original.HorasDiscriminadas.MinutosPorConcepto);
        restaurado.HorasDiscriminadas.Trazabilidad.Should().BeEmpty();
    }

    // CA-6: roundtrip con InformacionEmpleado null (caso "marcacion sin turno previo").
    [Fact]
    public void RoundTrip_PreservaCampos_CuandoInformacionEmpleadoEsNula()
    {
        var original = new DiaCalculado(
            null, Fecha, new HorasDiscriminadas(new Dictionary<string, int>(), []));
        var opciones = OpcionesPorDefectoDelPublisher();

        var json = JsonSerializer.Serialize(original, opciones);
        var restaurado = JsonSerializer.Deserialize<DiaCalculado>(json, opciones);

        restaurado.Should().NotBeNull();
        restaurado!.InformacionEmpleado.Should().BeNull();
        restaurado.Fecha.Should().Be(Fecha);
        restaurado.HorasDiscriminadas.MinutosPorConcepto.Should().BeEmpty();
    }

    // CA-6 (barrera anti-regresion invertida): con el resolver POR DEFECTO sin ningun
    // ConfigurarSerializacion registrado, la deserializacion DEBE tener exito -- el payload es 100%
    // primitivo. Si alguien reintroduce un tipo de dominio rico (ctor/campos privados), volveria a
    // necesitar el resolver custom y este test fallaria con NotSupportedException.
    [Fact]
    public void Deserializar_TieneExito_ConResolverPorDefectoSinRegistros()
    {
        var original = new DiaCalculado(Empleado, Fecha, HorasConDatos());
        var json = JsonSerializer.Serialize(original);

        var opcionesSinRegistros = new JsonSerializerOptions
        {
            TypeInfoResolver = new DefaultJsonTypeInfoResolver()
        };

        var act = () => JsonSerializer.Deserialize<DiaCalculado>(json, opcionesSinRegistros);

        act.Should().NotThrow();
        act()!.HorasDiscriminadas.MinutosPorConcepto["DominicalFestivaDiurna"].Should().Be(420);
    }
}
