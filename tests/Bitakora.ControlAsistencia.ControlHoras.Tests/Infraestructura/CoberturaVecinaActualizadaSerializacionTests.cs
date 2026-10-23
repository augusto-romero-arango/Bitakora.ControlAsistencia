using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.Infraestructura;

public class CoberturaVecinaActualizadaSerializacionTests
{
    private const string StreamId = "cd:EMP-001:20260421";
    private static readonly DateTime Limite = new(2026, 4, 21, 5, 0, 0);

    [Fact]
    public void RoundTrip_ReconstruyeEvento_ConVecinoYLimite()
    {
        var opciones = ConfiguracionSerializacionControlHoras.CrearOpcionesMarten();
        var evento = CoberturaVecinaActualizada.Crear(StreamId, DiaVecino.Vispera, Limite);

        var json = JsonSerializer.Serialize(evento, opciones);
        var deserializado = JsonSerializer.Deserialize<CoberturaVecinaActualizada>(json, opciones);

        deserializado.Should().NotBeNull();
        deserializado!.Id.Should().Be(StreamId);
        deserializado.Vecino.Should().Be(DiaVecino.Vispera);
        deserializado.Limite.Should().Be(Limite);
    }

    [Fact]
    public void RoundTrip_ConservaLimiteNull_CuandoElVecinoNoTieneCobertura()
    {
        var opciones = ConfiguracionSerializacionControlHoras.CrearOpcionesMarten();
        var evento = CoberturaVecinaActualizada.Crear(StreamId, DiaVecino.DiaSiguiente, null);

        var json = JsonSerializer.Serialize(evento, opciones);
        var deserializado = JsonSerializer.Deserialize<CoberturaVecinaActualizada>(json, opciones);

        deserializado.Should().NotBeNull();
        deserializado!.Vecino.Should().Be(DiaVecino.DiaSiguiente);
        deserializado.Limite.Should().BeNull();
    }

    [Fact]
    public void Deserializar_Falla_CuandoResolverNoTieneRegistroDeCoberturaVecinaActualizada()
    {
        var opciones = new JsonSerializerOptions { TypeInfoResolver = new DefaultJsonTypeInfoResolver() };
        var json = JsonSerializer.Serialize(
            CoberturaVecinaActualizada.Crear(StreamId, DiaVecino.Vispera, Limite), opciones);

        var act = () => JsonSerializer.Deserialize<CoberturaVecinaActualizada>(json, opciones);

        act.Should().Throw<NotSupportedException>();
    }
}
