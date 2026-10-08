using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;

namespace Bitakora.ControlAsistencia.Programacion.Tests.Eventos;

public class LimitesDeJornadaDePlantillaSemanalSincronizadosSerializacionTests
{
    private static readonly Guid PlantillaId = Guid.Parse("019600a0-0000-7000-8000-000000000884");
    private static readonly Guid JornadaId = Guid.Parse("019600a0-0000-7000-8000-000000000a01");

    private static JsonSerializerOptions CrearOpcionesMarten() =>
        ConfiguracionSerializacionProgramacion.CrearOpcionesMarten();

    [Fact]
    public void RoundTrip_ReconstruyeEvento_ConDatosCompletos()
    {
        var limites = LimitesJornada.Crear(HorasYMinutos.Crear(42, 30), HorasYMinutos.Crear(9, 0),
            HorasYMinutos.Crear(2, 15), 2);
        var evento = LimitesDeJornadaDePlantillaSemanalSincronizados.Crear(PlantillaId, JornadaId, limites, 7);
        var opciones = CrearOpcionesMarten();

        var json = JsonSerializer.Serialize(evento, opciones);
        var restaurado = JsonSerializer.Deserialize<LimitesDeJornadaDePlantillaSemanalSincronizados>(json, opciones);

        restaurado.Should().NotBeNull();
        restaurado!.PlantillaId.Should().Be(PlantillaId);
        restaurado.JornadaId.Should().Be(JornadaId);
        restaurado.Limites.Should().Be(limites);
        restaurado.Limites.ToString().Should().Be(limites.ToString());
        restaurado.VersionJornada.Should().Be(7);
    }
}
