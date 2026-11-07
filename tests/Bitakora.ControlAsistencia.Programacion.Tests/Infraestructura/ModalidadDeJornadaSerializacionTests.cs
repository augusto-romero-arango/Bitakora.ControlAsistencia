using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;

namespace Bitakora.ControlAsistencia.Programacion.Tests.Infraestructura;

public class ModalidadDeJornadaSerializacionTests
{
    private static readonly Guid Id = Guid.Parse("019600a0-0000-7000-8000-000000001436");

    [Fact]
    public void RoundTrip_ReconstruyeJornadaIdYModalidad_CuandoSeAsigna()
    {
        var evento = ModalidadDeJornadaAsignada.Crear(Id, ModalidadJornada.SinSolucionDeContinuidad);
        var opciones = ConfiguracionSerializacionProgramacion.CrearOpcionesMarten();

        var json = JsonSerializer.Serialize(evento, opciones);
        var restaurado = JsonSerializer.Deserialize<ModalidadDeJornadaAsignada>(json, opciones);

        restaurado.Should().NotBeNull();
        restaurado!.JornadaId.Should().Be(Id);
        restaurado.Modalidad.Should().Be(evento.Modalidad);
        restaurado.Modalidad.ToString().Should().Be("Sin solución de continuidad");
    }

    [Fact]
    public void RoundTrip_ReconstruyeJornadaId_CuandoSeQuita()
    {
        var evento = ModalidadDeJornadaQuitada.Crear(Id);
        var opciones = ConfiguracionSerializacionProgramacion.CrearOpcionesMarten();

        var json = JsonSerializer.Serialize(evento, opciones);
        var restaurado = JsonSerializer.Deserialize<ModalidadDeJornadaQuitada>(json, opciones);

        restaurado.Should().NotBeNull();
        restaurado!.JornadaId.Should().Be(Id);
    }

    [Fact]
    public void Deserializar_Falla_CuandoResolverNoTieneRegistroDeModalidadDeJornadaAsignada()
    {
        var opciones = new JsonSerializerOptions { TypeInfoResolver = new DefaultJsonTypeInfoResolver() };
        var json = JsonSerializer.Serialize(
            ModalidadDeJornadaAsignada.Crear(Id, ModalidadJornada.Ordinaria), opciones);

        var act = () => JsonSerializer.Deserialize<ModalidadDeJornadaAsignada>(json, opciones);

        act.Should().Throw<NotSupportedException>();
    }
}
