using AwesomeAssertions;
using Bitakora.ControlAsistencia.Colaboradores.AsignarJornadaFunction.CommandHandler;

namespace Bitakora.ControlAsistencia.Colaboradores.Tests.AsignarJornadaFunction;

public class AsignarJornadaCommandHandlerMensajesTests
{
    [Fact]
    public void Mensajes_ResuelvenTextoNoVacio_CuandoPertenecenAAsignarJornadaCommandHandler()
    {
        AsignarJornadaCommandHandler.Mensajes.ColaboradorNoEncontrado.Should().NotBeNullOrWhiteSpace();
        AsignarJornadaCommandHandler.Mensajes.VinculacionTerminada.Should().NotBeNullOrWhiteSpace();
    }
}
