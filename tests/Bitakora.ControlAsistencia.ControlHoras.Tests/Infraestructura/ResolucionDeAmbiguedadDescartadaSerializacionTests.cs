using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.Infraestructura;

public class ResolucionDeAmbiguedadDescartadaSerializacionTests
{
    private const string StreamId = "cd:EMP-001:20261021";

    [Fact]
    public void RoundTrip_ConservaElVecino_ConDatosCompletos()
    {
        var opciones = ConfiguracionSerializacionControlHoras.CrearOpcionesMarten();
        var evento = ResolucionDeAmbiguedadDescartada.Crear(StreamId, DiaVecino.DiaSiguiente);

        var json = JsonSerializer.Serialize(evento, opciones);
        var deserializado = JsonSerializer.Deserialize<ResolucionDeAmbiguedadDescartada>(json, opciones);

        deserializado.Should().NotBeNull();
        deserializado!.Id.Should().Be(StreamId);
        deserializado.Vecino.Should().Be(DiaVecino.DiaSiguiente);
    }

    [Fact]
    public void Deserializar_Falla_CuandoResolverNoTieneRegistroDeResolucionDeAmbiguedadDescartada()
    {
        var opciones = new JsonSerializerOptions { TypeInfoResolver = new DefaultJsonTypeInfoResolver() };
        var json = JsonSerializer.Serialize(
            ResolucionDeAmbiguedadDescartada.Crear(StreamId, DiaVecino.Vispera), opciones);

        var act = () => JsonSerializer.Deserialize<ResolucionDeAmbiguedadDescartada>(json, opciones);

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void TiposPersistidos_IncluyeResolucionDeAmbiguedadDescartada()
    {
        IdentidadEventosControlHoras.TiposPersistidos.Should().Contain(typeof(ResolucionDeAmbiguedadDescartada));
    }
}
