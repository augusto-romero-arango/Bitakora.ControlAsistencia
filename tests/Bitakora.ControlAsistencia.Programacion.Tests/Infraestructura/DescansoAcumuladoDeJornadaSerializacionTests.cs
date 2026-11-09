using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;

namespace Bitakora.ControlAsistencia.Programacion.Tests.Infraestructura;

public class DescansoAcumuladoDeJornadaSerializacionTests
{
    private static readonly Guid Id = Guid.Parse("019600a0-0000-7000-8000-000000001437");

    [Fact]
    public void RoundTrip_ReconstruyeJornadaId_CuandoSeMarca()
    {
        var evento = DescansoAcumuladoDeJornadaMarcado.Crear(Id);
        var opciones = ConfiguracionSerializacionProgramacion.CrearOpcionesMarten();

        var json = JsonSerializer.Serialize(evento, opciones);
        var restaurado = JsonSerializer.Deserialize<DescansoAcumuladoDeJornadaMarcado>(json, opciones);

        restaurado.Should().NotBeNull();
        restaurado!.JornadaId.Should().Be(Id);
    }

    [Fact]
    public void RoundTrip_ReconstruyeJornadaId_CuandoSeDesmarca()
    {
        var evento = DescansoAcumuladoDeJornadaDesmarcado.Crear(Id);
        var opciones = ConfiguracionSerializacionProgramacion.CrearOpcionesMarten();

        var json = JsonSerializer.Serialize(evento, opciones);
        var restaurado = JsonSerializer.Deserialize<DescansoAcumuladoDeJornadaDesmarcado>(json, opciones);

        restaurado.Should().NotBeNull();
        restaurado!.JornadaId.Should().Be(Id);
    }

    [Fact]
    public void Deserializar_Falla_CuandoResolverNoTieneRegistroDeDescansoAcumuladoDeJornadaMarcado()
    {
        var opciones = new JsonSerializerOptions { TypeInfoResolver = new DefaultJsonTypeInfoResolver() };
        var json = JsonSerializer.Serialize(DescansoAcumuladoDeJornadaMarcado.Crear(Id), opciones);

        var act = () => JsonSerializer.Deserialize<DescansoAcumuladoDeJornadaMarcado>(json, opciones);

        act.Should().Throw<NotSupportedException>();
    }
}
