// El evento no lleva VOs con ctor privado, asi que STJ resuelve su unico ctor publico sin ayuda:
// no se registra en ConfigurarSerializacion (igual que su gemela ProgramacionTurnoSolicitada).

using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;

namespace Bitakora.ControlAsistencia.Programacion.Tests.CancelarProgramacionFunction.Eventos;

public class CancelacionProgramacionSolicitadaSerializacionTests
{
    private static readonly Guid Id = Guid.Parse("019600e0-0000-7000-8000-000000000080");
    private static readonly DateOnly Fecha1 = new(2026, 4, 7);
    private static readonly DateOnly Fecha2 = new(2026, 4, 8);

    private static readonly ColaboradorProgramado ColaboradorEsperado =
        new("CC-12345678", "E001", "Juan Perez");

    [Fact]
    public void RoundTrip_ReconstruyeEventoIdentico_ConDatosCompletos()
    {
        var evento = new CancelacionProgramacionSolicitada(
            Id, ColaboradorEsperado, new List<DateOnly> { Fecha1, Fecha2 }.AsReadOnly());
        var opciones = ConfiguracionSerializacionProgramacion.CrearOpcionesMarten();

        var json = JsonSerializer.Serialize(evento, opciones);
        var restaurado = JsonSerializer.Deserialize<CancelacionProgramacionSolicitada>(json, opciones);

        restaurado.Should().NotBeNull();
        restaurado!.Id.Should().Be(Id);
        restaurado.Colaborador.Should().Be(ColaboradorEsperado);
        restaurado.Fechas.Should().Equal(Fecha1, Fecha2);
    }
}
