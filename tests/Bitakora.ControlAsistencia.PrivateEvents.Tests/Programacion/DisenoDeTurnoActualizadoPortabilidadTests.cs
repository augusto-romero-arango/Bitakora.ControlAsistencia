using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.PrivateEvents.Programacion;

namespace Bitakora.ControlAsistencia.PrivateEvents.Tests.Programacion;

public class DisenoDeTurnoActualizadoPortabilidadTests
{
    private static JsonSerializerOptions CrearOpcionesBus() => new(JsonSerializerDefaults.Web);

    [Fact]
    public void RoundTrip_PreservaTodosLosCampos_ConSerializadorPorDefectoDelBus()
    {
        var evento = new DisenoDeTurnoActualizado(
            Guid.Parse("019600a0-0000-7000-8000-000000000894"),
            4,
            "Turno Noche",
            false,
            [new DetalleFranjaOrdinaria(
                new TimeOnly(22, 0), new TimeOnly(6, 0), 1,
                [new DetalleSubFranja(new TimeOnly(2, 0), new TimeOnly(2, 30), 1, 1, "(02:00-02:30)")],
                [new DetalleSubFranja(new TimeOnly(5, 0), new TimeOnly(6, 0), 1, 1, "(05:00-06:00)")],
                "(22:00-06:00+1)[Descansos:(02:00-02:30)][Extras:(05:00-06:00)][sede:Chapinero]",
                new DetalleSede("SEDE-CHAPINERO", "Chapinero", "CC-01"))],
            true);
        var opciones = CrearOpcionesBus();

        var json = JsonSerializer.Serialize(evento, opciones);
        var restaurado = JsonSerializer.Deserialize<DisenoDeTurnoActualizado>(json, opciones);

        restaurado.Should().NotBeNull();
        restaurado.Should().Be(evento);
        restaurado!.Franjas[0].Descripcion.Should().Be(evento.Franjas[0].Descripcion);
    }
}
