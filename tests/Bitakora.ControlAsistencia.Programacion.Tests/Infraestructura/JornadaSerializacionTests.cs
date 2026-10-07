using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;

namespace Bitakora.ControlAsistencia.Programacion.Tests.Infraestructura;

public class JornadaSerializacionTests
{
    private static readonly Guid Id = Guid.Parse("019600a0-0000-7000-8000-000000000854");
    private static JsonSerializerOptions Opciones() => ConfiguracionSerializacionProgramacion.CrearOpcionesMarten();

    [Fact]
    public void RoundTrip_ReconstruyeHorasYMinutosYGuardaMinutosEnteros()
    {
        var original = HorasYMinutos.Crear(8, 12);
        var json = JsonSerializer.Serialize(original, Opciones());
        var restaurado = JsonSerializer.Deserialize<HorasYMinutos>(json, Opciones());
        restaurado.Should().NotBeNull();
        restaurado!.ToString().Should().Be("8 h 12 min");
        restaurado.Should().Be(original);
        using var documento = JsonDocument.Parse(json);
        documento.RootElement.GetProperty("MinutosTotales").GetInt32().Should().Be(492);
    }

    [Fact]
    public void RoundTrip_ReconstruyeLimitesConCuatroValores()
    {
        var original = LimitesJornada.Crear(HorasYMinutos.Crear(42, 0),
            HorasYMinutos.Crear(8, 0), HorasYMinutos.Crear(4, 0), 1);
        var json = JsonSerializer.Serialize(original, Opciones());
        var restaurado = JsonSerializer.Deserialize<LimitesJornada>(json, Opciones());
        restaurado.Should().NotBeNull();
        restaurado!.ToString().Should().Be(
            "42 h semanales, tope diario 8 h, mínimo diario 4 h, 1 día de descanso por semana");
        restaurado.Should().Be(original);
        using var documento = JsonDocument.Parse(json);
        documento.RootElement.GetProperty("DiasDescansoPorSemana").GetInt32().Should().Be(1);
    }

    [Fact]
    public void RoundTrip_ReconstruyeJornadaCreadaConLimites()
    {
        var evento = JornadaCreada.Crear(Id, LimitesJornada.Crear(HorasYMinutos.Crear(42, 0),
            HorasYMinutos.Crear(8, 0), HorasYMinutos.Crear(0, 0), 1));
        var json = JsonSerializer.Serialize(evento, Opciones());
        var restaurado = JsonSerializer.Deserialize<JornadaCreada>(json, Opciones());
        restaurado.Should().NotBeNull();
        restaurado!.JornadaId.Should().Be(Id);
        restaurado.Limites.ToString().Should().Be(
            "42 h semanales, tope diario 8 h, sin mínimo diario, 1 día de descanso por semana");
        restaurado.Limites.Should().Be(evento.Limites);
    }

    [Fact]
    public void RoundTrip_ConservaMinutosYDescansosSinMinimo()
    {
        var original = LimitesJornada.Crear(HorasYMinutos.Crear(30, 37),
            HorasYMinutos.Crear(8, 12), HorasYMinutos.Crear(0, 0), 2);
        var json = JsonSerializer.Serialize(original, Opciones());
        var restaurado = JsonSerializer.Deserialize<LimitesJornada>(json, Opciones());
        restaurado.Should().Be(original);
        restaurado!.ToString().Should().Be(
            "30 h 37 min semanales, tope diario 8 h 12 min, sin mínimo diario, 2 días de descanso por semana");
    }

    [Fact]
    public void Deserializar_Falla_CuandoResolverNoRegistraJornadaCreada()
    {
        var opciones = new JsonSerializerOptions { TypeInfoResolver = new DefaultJsonTypeInfoResolver() };
        var json = JsonSerializer.Serialize(JornadaCreada.Crear(Id, LimitesJornada.Crear(
            HorasYMinutos.Crear(42, 0), HorasYMinutos.Crear(8, 0), HorasYMinutos.Crear(0, 0), 1)), opciones);
        var act = () => JsonSerializer.Deserialize<JornadaCreada>(json, opciones);
        act.Should().Throw<NotSupportedException>();
    }
}
