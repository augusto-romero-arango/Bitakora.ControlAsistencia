using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;

namespace Bitakora.ControlAsistencia.Programacion.Tests.Eventos;

public class ProgramacionPublicadaSerializacionTests
{
    private static JsonSerializerOptions CrearOpcionesMarten() =>
        ConfiguracionSerializacionProgramacion.CrearOpcionesMarten();

    [Fact]
    public void RoundTrip_ReconstruyeEvento_ConDatosCompletos()
    {
        var evento = new ProgramacionPublicada(
            new ColaboradorProgramado("CC-12345678", "E001", "Juan Perez"),
            [new DateOnly(2026, 10, 30), new DateOnly(2026, 10, 31)]);
        var opciones = CrearOpcionesMarten();

        var json = JsonSerializer.Serialize(evento, opciones);
        var restaurado = JsonSerializer.Deserialize<ProgramacionPublicada>(json, opciones);

        restaurado.Should().NotBeNull();
        restaurado!.Colaborador.Should().Be(new ColaboradorProgramado("CC-12345678", "E001", "Juan Perez"));
        restaurado.Fechas.Should().Equal(new DateOnly(2026, 10, 30), new DateOnly(2026, 10, 31));
    }
}
