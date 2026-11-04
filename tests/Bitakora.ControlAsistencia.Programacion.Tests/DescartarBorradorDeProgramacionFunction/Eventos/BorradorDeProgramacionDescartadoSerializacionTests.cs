using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;

namespace Bitakora.ControlAsistencia.Programacion.Tests.DescartarBorradorDeProgramacionFunction.Eventos;

public class BorradorDeProgramacionDescartadoSerializacionTests
{
    private static readonly DateOnly Fecha1 = new(2026, 10, 28);
    private static readonly DateOnly Fecha2 = new(2026, 10, 29);

    private static readonly ColaboradorProgramado ColaboradorEsperado =
        new("CC-12345678", "E001", "Juan Perez");

    [Fact]
    public void RoundTrip_ReconstruyeEvento_ConDatosCompletos()
    {
        var evento = new BorradorDeProgramacionDescartado(
            ColaboradorEsperado, new List<DateOnly> { Fecha1, Fecha2 }.AsReadOnly());
        var opciones = ConfiguracionSerializacionProgramacion.CrearOpcionesMarten();

        var json = JsonSerializer.Serialize(evento, opciones);
        var restaurado = JsonSerializer.Deserialize<BorradorDeProgramacionDescartado>(json, opciones);

        restaurado.Should().NotBeNull();
        restaurado!.Colaborador.Should().Be(ColaboradorEsperado);
        restaurado.Fechas.Should().Equal(Fecha1, Fecha2);
    }
}
