using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.Infraestructura;

public class AmbiguedadDeMarcacionesResueltaSerializacionTests
{
    private const string StreamId = "cd:EMP-001:20261021";

    private static readonly MarcacionDecidida[] Decisiones =
    [
        new(new DateTime(2026, 10, 21, 4, 0, 0), new DateOnly(2026, 10, 20)),
        new(new DateTime(2026, 10, 21, 4, 50, 0), new DateOnly(2026, 10, 21))
    ];

    [Fact]
    public void RoundTrip_ConservaLasDecisiones_ConVecinoYMarcaciones()
    {
        var opciones = ConfiguracionSerializacionControlHoras.CrearOpcionesMarten();
        var evento = AmbiguedadDeMarcacionesResuelta.Crear(StreamId, DiaVecino.Vispera, Decisiones);

        var json = JsonSerializer.Serialize(evento, opciones);
        var deserializado = JsonSerializer.Deserialize<AmbiguedadDeMarcacionesResuelta>(json, opciones);

        deserializado.Should().NotBeNull();
        deserializado!.Id.Should().Be(StreamId);
        deserializado.Vecino.Should().Be(DiaVecino.Vispera);
        deserializado.Marcaciones.Should().Equal(Decisiones);
    }

    [Fact]
    public void Deserializar_Falla_CuandoResolverNoTieneRegistroDeAmbiguedadDeMarcacionesResuelta()
    {
        var opciones = new JsonSerializerOptions { TypeInfoResolver = new DefaultJsonTypeInfoResolver() };
        var json = JsonSerializer.Serialize(
            AmbiguedadDeMarcacionesResuelta.Crear(StreamId, DiaVecino.DiaSiguiente, Decisiones), opciones);

        var act = () => JsonSerializer.Deserialize<AmbiguedadDeMarcacionesResuelta>(json, opciones);

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void TiposPersistidos_IncluyeAmbiguedadDeMarcacionesResuelta()
    {
        IdentidadEventosControlHoras.TiposPersistidos.Should().Contain(typeof(AmbiguedadDeMarcacionesResuelta));
    }
}
