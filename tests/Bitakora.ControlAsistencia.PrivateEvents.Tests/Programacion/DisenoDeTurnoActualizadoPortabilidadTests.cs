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
                [new DetalleSubFranja(new TimeOnly(2, 0), new TimeOnly(2, 30), 1, 1, "(02:00+1-02:30+1)")],
                [new DetalleSubFranja(new TimeOnly(5, 0), new TimeOnly(6, 0), 1, 1, "(05:00+1-06:00+1)")],
                "(22:00-06:00+1)[Descansos:(02:00+1-02:30+1)][Extras:(05:00+1-06:00+1)][sede:Chapinero]",
                new DetalleSede("SEDE-CHAPINERO", "Chapinero", "CC-01"))],
            true);
        var opciones = CrearOpcionesBus();

        var json = JsonSerializer.Serialize(evento, opciones);
        var restaurado = JsonSerializer.Deserialize<DisenoDeTurnoActualizado>(json, opciones);

        restaurado.Should().NotBeNull();
        restaurado.Should().Be(evento);
        restaurado!.Franjas[0].Descripcion.Should().Be(evento.Franjas[0].Descripcion);
    }

    private static DisenoDeTurnoActualizado Evento(int amplitud) => new(
        Guid.Parse("019600a0-0000-7000-8000-000000001109"), 2, "Oficina", false,
        [new DetalleFranjaOrdinaria(new TimeOnly(7, 0), new TimeOnly(16, 0), 0, [], [], "(07:00-16:00)")],
        false, amplitud);

    [Fact]
    public void RoundTrip_ConservaLaAmplitudDeLaVentana_ConSerializadorPorDefectoDelBus()
    {
        var opciones = CrearOpcionesBus();

        var json = JsonSerializer.Serialize(Evento(180), opciones);
        var restaurado = JsonSerializer.Deserialize<DisenoDeTurnoActualizado>(json, opciones);

        restaurado.Should().NotBeNull();
        restaurado!.AmplitudVentanaEntradaEnMinutos.Should().Be(180);
    }

    [Fact]
    public void Deserializar_LeeAmplitudCero_CuandoElJsonNoTraeLaClave()
    {
        const string json = """
            {"turnoId":"019600a0-0000-7000-8000-000000001109","version":2,"nombre":"Oficina","esDescanso":false,
             "franjas":[],"retirado":false}
            """;

        var restaurado = JsonSerializer.Deserialize<DisenoDeTurnoActualizado>(json, CrearOpcionesBus());

        restaurado!.AmplitudVentanaEntradaEnMinutos.Should().Be(0);
    }

    [Fact]
    public void Equals_EsFalso_CuandoSoloDifierePorLaAmplitud()
    {
        Evento(180).Should().NotBe(Evento(0));
    }
}
