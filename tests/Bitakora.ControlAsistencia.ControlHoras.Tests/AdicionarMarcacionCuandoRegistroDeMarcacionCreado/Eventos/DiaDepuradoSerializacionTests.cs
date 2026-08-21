// Issue #183: Reemplazar el payload de DiaCalculado por HorasDiscriminadas plano.
// Issue #421: DiaCalculado se reclasifica como DiaDepurado (IPrivateEvent); el payload gana
// CodigoColaborador top-level (siempre presente) y Colaborador pasa de InformacionColaborador
// (PublicEvents.Colaboradores) a ResumenColaborador (PrivateEvents.Colaboradores, terna reducida).
// CA-6: el payload completo de DiaDepurado round-trip serializa/deserializa con el serializador POR
//       DEFECTO del publisher (sin el resolver custom de Marten) y sin perdida -- el evento nunca
//       cruza el canal de Marten (no se persiste, ver IdentidadEventosControlHoras).
//
// Inversion de la barrera anterior: el test legado afirmaba que la deserializacion FALLABA sin el
// resolver custom (Retardo tenia ctor privado). Ahora se afirma lo OPUESTO: tiene EXITO con el
// resolver por defecto. Si alguien reintroduce un tipo rico al payload, este test fallaria.

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
        var original = new DiaDepurado("EMP-001", Fecha, Colaborador, HorasConDatos());
        var opciones = OpcionesPorDefectoDelPublisher();

        var json = JsonSerializer.Serialize(original, opciones);
        var restaurado = JsonSerializer.Deserialize<DiaDepurado>(json, opciones);

        restaurado.Should().NotBeNull();
        restaurado!.CodigoColaborador.Should().Be("EMP-001");
        restaurado.Colaborador.Should().Be(Colaborador);
        restaurado.Fecha.Should().Be(Fecha);
        restaurado.HorasDiscriminadas.MinutosPorConcepto.Should().BeEquivalentTo(
            original.HorasDiscriminadas.MinutosPorConcepto);
        restaurado.HorasDiscriminadas.Trazabilidad.Should().BeEmpty();
    }

    // CA-4/CA-6: roundtrip con Colaborador null (caso "marcacion sin turno previo"), pero
    // CodigoColaborador top-level SIEMPRE presente -- el defecto latente que este issue corrige.
    [Fact]
    public void RoundTrip_PreservaCampos_CuandoColaboradorEsNulo()
    {
        var original = new DiaDepurado(
            "EMP-002", Fecha, null, new HorasDiscriminadas(new Dictionary<string, int>(), []));
        var opciones = OpcionesPorDefectoDelPublisher();

        var json = JsonSerializer.Serialize(original, opciones);
        var restaurado = JsonSerializer.Deserialize<DiaDepurado>(json, opciones);

        restaurado.Should().NotBeNull();
        restaurado!.CodigoColaborador.Should().Be("EMP-002");
        restaurado.Colaborador.Should().BeNull();
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
        var original = new DiaDepurado("EMP-001", Fecha, Colaborador, HorasConDatos());
        var json = JsonSerializer.Serialize(original);

        var opcionesSinRegistros = new JsonSerializerOptions
        {
            TypeInfoResolver = new DefaultJsonTypeInfoResolver()
        };

        var act = () => JsonSerializer.Deserialize<DiaDepurado>(json, opcionesSinRegistros);

        act.Should().NotThrow();
        act()!.HorasDiscriminadas.MinutosPorConcepto["DominicalFestivaDiurna"].Should().Be(420);
    }
}
