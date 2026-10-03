using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.Infraestructura;

public class CancelacionAusenciaDiariaRegistradaSerializacionTests
{
    private static readonly Guid AusenciaId = Guid.Parse("019600b0-0000-7000-8000-000000000020");
    private static readonly DateOnly Fecha = new(2026, 3, 15);
    private const string StreamId = "cd:EMP-001:20260315";

    private static CancelacionAusenciaDiariaRegistrada CrearEvento() =>
        CancelacionAusenciaDiariaRegistrada.Crear(StreamId, AusenciaId, Fecha);

    [Fact]
    public void RoundTrip_ReconstruyeEvento_ConTodosLosCampos()
    {
        var opciones = ConfiguracionSerializacionControlHoras.CrearOpcionesMarten();

        var json = JsonSerializer.Serialize(CrearEvento(), opciones);
        var deserializado = JsonSerializer.Deserialize<CancelacionAusenciaDiariaRegistrada>(json, opciones);

        deserializado.Should().NotBeNull();
        deserializado!.Id.Should().Be(StreamId);
        deserializado.AusenciaId.Should().Be(AusenciaId);
        deserializado.Fecha.Should().Be(Fecha);
    }

    [Fact]
    public void Deserializar_Falla_CuandoResolverNoTieneRegistroDeCancelacionAusenciaDiariaRegistrada()
    {
        var opciones = new JsonSerializerOptions { TypeInfoResolver = new DefaultJsonTypeInfoResolver() };
        var json = JsonSerializer.Serialize(CrearEvento(), opciones);

        var act = () => JsonSerializer.Deserialize<CancelacionAusenciaDiariaRegistrada>(json, opciones);

        act.Should().Throw<NotSupportedException>();
    }
}
