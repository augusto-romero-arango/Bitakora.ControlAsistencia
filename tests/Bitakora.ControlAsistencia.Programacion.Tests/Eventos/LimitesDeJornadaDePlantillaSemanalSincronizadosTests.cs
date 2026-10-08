using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;

namespace Bitakora.ControlAsistencia.Programacion.Tests.Eventos;

public class LimitesDeJornadaDePlantillaSemanalSincronizadosTests
{
    private static readonly Guid PlantillaId = Guid.Parse("019600a0-0000-7000-8000-000000000884");

    [Fact]
    public void Crear_ExponeDatosRecibidos()
    {
        var limites = LimitesJornada.Crear(HorasYMinutos.Crear(42, 0), HorasYMinutos.Crear(8, 0),
            HorasYMinutos.Crear(4, 0), 1);

        var evento = LimitesDeJornadaDePlantillaSemanalSincronizados.Crear(PlantillaId, limites, 3);

        evento.PlantillaId.Should().Be(PlantillaId);
        evento.Limites.Should().Be(limites);
        evento.VersionJornada.Should().Be(3);
    }
}
