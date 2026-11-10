using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.AprobarDiaFunction.Eventos;

public class DiaAprobadoSerializacionTests
{
    private const string StreamId = "dc:EMP-001:20260824";
    private const string CodigoColaborador = "EMP-001";
    private static readonly DateOnly Fecha = new(2026, 8, 24);

    // Opciones reales de Marten, no un resolver inline: sin el registro en ConfigurarResolver este test
    // lo detecta.
    private static JsonSerializerOptions CrearOpcionesMarten() =>
        ConfiguracionSerializacionControlHoras.CrearOpcionesMarten();

    [Fact]
    public void RoundTrip_ReconstruyeEvento_CuandoTraeSedesDecididas()
    {
        var evento = DiaAprobado.Crear(StreamId, CodigoColaborador, Fecha,
            [new SedeDecidida(new TimeOnly(6, 0), "SEDE-02", "Sede Norte", "CC-200")]);
        var opciones = CrearOpcionesMarten();

        var json = JsonSerializer.Serialize(evento, opciones);
        var deserializado = JsonSerializer.Deserialize<DiaAprobado>(json, opciones);

        deserializado.Should().NotBeNull();
        deserializado!.Id.Should().Be(StreamId);
        deserializado.CodigoColaborador.Should().Be(CodigoColaborador);
        deserializado.Fecha.Should().Be(Fecha);
        deserializado.SedesDecididas.Should().BeEquivalentTo(
            [new SedeDecidida(new TimeOnly(6, 0), "SEDE-02", "Sede Norte", "CC-200")]);
    }

    [Fact]
    public void RoundTrip_ReconstruyeEvento_CuandoSedesDecididasEstaVacia()
    {
        var evento = DiaAprobado.Crear(StreamId, CodigoColaborador, Fecha, []);
        var opciones = CrearOpcionesMarten();

        var json = JsonSerializer.Serialize(evento, opciones);
        var deserializado = JsonSerializer.Deserialize<DiaAprobado>(json, opciones);

        deserializado.Should().NotBeNull();
        deserializado!.Id.Should().Be(StreamId);
        deserializado.SedesDecididas.Should().BeEmpty();
    }

    // Sin el registro en ConfigurarResolver el ctor privado es inalcanzable para STJ.
    [Fact]
    public void Deserializar_LanzaNotSupportedException_CuandoElResolverNoRegistraElTipo()
    {
        var evento = DiaAprobado.Crear(StreamId, CodigoColaborador, Fecha, []);
        var opcionesSinRegistro = new JsonSerializerOptions
        {
            TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
            PropertyNamingPolicy = null
        };
        var json = JsonSerializer.Serialize(evento, opcionesSinRegistro);

        var act = () => JsonSerializer.Deserialize<DiaAprobado>(json, opcionesSinRegistro);

        act.Should().Throw<NotSupportedException>();
    }
}
