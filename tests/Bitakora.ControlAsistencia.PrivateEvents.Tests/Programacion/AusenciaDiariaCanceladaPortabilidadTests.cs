using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.PrivateEvents.Colaboradores;
using Bitakora.ControlAsistencia.PrivateEvents.Programacion;

namespace Bitakora.ControlAsistencia.PrivateEvents.Tests.Programacion;

public class AusenciaDiariaCanceladaPortabilidadTests
{
    private static JsonSerializerOptions CrearOpcionesBus() => new(JsonSerializerDefaults.Web);

    [Fact]
    public void RoundTrip_PreservaTodosLosCampos_ConSerializadorPorDefectoDelBus()
    {
        var evento = new AusenciaDiariaCancelada(
            Guid.Parse("019600a0-0000-7000-8000-000000000744"),
            new ResumenColaborador("CC-12345678", "E001", "Ana Maria Gomez"),
            new DateOnly(2026, 10, 5));
        var opciones = CrearOpcionesBus();

        var json = JsonSerializer.Serialize(evento, opciones);
        var restaurado = JsonSerializer.Deserialize<AusenciaDiariaCancelada>(json, opciones);

        restaurado.Should().NotBeNull();
        restaurado!.AusenciaId.Should().Be(evento.AusenciaId);
        restaurado.Colaborador.Should().Be(evento.Colaborador);
        restaurado.Fecha.Should().Be(evento.Fecha);
    }
}
