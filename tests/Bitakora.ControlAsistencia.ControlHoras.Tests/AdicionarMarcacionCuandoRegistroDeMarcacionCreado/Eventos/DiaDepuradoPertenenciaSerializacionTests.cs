using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.PrivateEvents.ControlHoras;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.AdicionarMarcacionCuandoRegistroDeMarcacionCreado.Eventos;

public class DiaDepuradoPertenenciaSerializacionTests
{
    [Fact]
    public void RoundTrip_PreservaLaPertenencia_ConSerializadorPorDefectoDelPublisher()
    {
        var original = new DiaDepurado(
            "EMP-001", new DateOnly(2026, 3, 15), null, "Turno Manana", [],
            [
                new MarcacionDelDia(new DateTime(2026, 3, 15, 4, 30, 0), "ENTRADA", Pertenencia: "Ambigua"),
                new MarcacionDelDia(new DateTime(2026, 3, 15, 6, 0, 0), "ENTRADA", Pertenencia: "DelTurno")
            ],
            new HorasDiscriminadas(new Dictionary<string, decimal>(), []));
        var opciones = new JsonSerializerOptions();

        var json = JsonSerializer.Serialize(original, opciones);
        var restaurado = JsonSerializer.Deserialize<DiaDepurado>(json, opciones);

        restaurado!.Marcaciones.Select(m => m.Pertenencia).Should().Equal("Ambigua", "DelTurno");
    }
}
