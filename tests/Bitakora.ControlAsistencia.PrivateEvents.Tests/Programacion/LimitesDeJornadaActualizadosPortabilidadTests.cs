using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.PrivateEvents.Programacion;

namespace Bitakora.ControlAsistencia.PrivateEvents.Tests.Programacion;

public class LimitesDeJornadaActualizadosPortabilidadTests
{
    private static JsonSerializerOptions CrearOpcionesBus() => new(JsonSerializerDefaults.Web);

    [Fact]
    public void RoundTrip_PreservaTodosLosCampos_ConSerializadorPorDefectoDelBus()
    {
        var evento = new LimitesDeJornadaActualizados(
            Guid.Parse("019600a0-0000-7000-8000-000000000897"), 2, 2640, 480, 30, 1);
        var opciones = CrearOpcionesBus();

        var json = JsonSerializer.Serialize(evento, opciones);
        var restaurado = JsonSerializer.Deserialize<LimitesDeJornadaActualizados>(json, opciones);

        restaurado.Should().NotBeNull();
        restaurado!.JornadaId.Should().Be(evento.JornadaId);
        restaurado.Version.Should().Be(2);
        restaurado.HorasSemanalesEnMinutos.Should().Be(2640);
        restaurado.TopeDiarioEnMinutos.Should().Be(480);
        restaurado.MinimoDiarioEnMinutos.Should().Be(30);
        restaurado.DiasDescansoPorSemana.Should().Be(1);
    }
}
