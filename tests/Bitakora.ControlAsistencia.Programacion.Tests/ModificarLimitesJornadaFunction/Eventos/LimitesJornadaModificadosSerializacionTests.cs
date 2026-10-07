using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;

namespace Bitakora.ControlAsistencia.Programacion.Tests.ModificarLimitesJornadaFunction.Eventos;

public class LimitesJornadaModificadosSerializacionTests
{
    private static readonly Guid Id = Guid.Parse("019600a0-0000-7000-8000-000000000856");

    [Fact]
    public void RoundTrip_ReconstruyeJornadaIdYLosCuatroLimites()
    {
        var evento = LimitesJornadaModificados.Crear(Id, LimitesJornada.Crear(
            HorasYMinutos.Crear(44, 15), HorasYMinutos.Crear(8, 30), HorasYMinutos.Crear(4, 0), 1));
        var opciones = ConfiguracionSerializacionProgramacion.CrearOpcionesMarten();

        var json = JsonSerializer.Serialize(evento, opciones);
        var restaurado = JsonSerializer.Deserialize<LimitesJornadaModificados>(json, opciones);

        restaurado.Should().NotBeNull();
        restaurado!.JornadaId.Should().Be(Id);
        restaurado.Limites.Should().Be(evento.Limites);
        restaurado.Limites.ToString().Should().Be(
            "44 h 15 min semanales, tope diario 8 h 30 min, mínimo diario 4 h, 1 día de descanso por semana");
    }
}
