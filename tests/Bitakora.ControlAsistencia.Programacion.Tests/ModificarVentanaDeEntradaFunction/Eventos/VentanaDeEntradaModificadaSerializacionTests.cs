using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;

namespace Bitakora.ControlAsistencia.Programacion.Tests.ModificarVentanaDeEntradaFunction.Eventos;

public class VentanaDeEntradaModificadaSerializacionTests
{
    private static readonly Guid Id = Guid.Parse("019600a0-0000-7000-8000-000000001106");

    [Fact]
    public void RoundTrip_ReconstruyeTurnoIdYAmplitud()
    {
        var evento = VentanaDeEntradaModificada.Crear(Id, HorasYMinutos.Crear(3, 15));
        var opciones = ConfiguracionSerializacionProgramacion.CrearOpcionesMarten();

        var json = JsonSerializer.Serialize(evento, opciones);
        var restaurado = JsonSerializer.Deserialize<VentanaDeEntradaModificada>(json, opciones);

        restaurado.Should().NotBeNull();
        restaurado!.TurnoId.Should().Be(Id);
        restaurado.Amplitud.Should().Be(evento.Amplitud);
        restaurado.Amplitud.ToString().Should().Be("3 h 15 min");
    }
}
