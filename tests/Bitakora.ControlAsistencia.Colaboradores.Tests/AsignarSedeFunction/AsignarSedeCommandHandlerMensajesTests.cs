// Por que existe este guardrail: ver TerminarVinculacionCommandHandlerMensajesTests.

using AwesomeAssertions;
using Bitakora.ControlAsistencia.Colaboradores.AsignarSedeFunction.CommandHandler;

namespace Bitakora.ControlAsistencia.Colaboradores.Tests.AsignarSedeFunction;

public class AsignarSedeCommandHandlerMensajesTests
{
    [Fact]
    public void Mensajes_ResuelvenTextoNoVacio_CuandoPertenecenAAsignarSedeCommandHandler()
    {
        AsignarSedeCommandHandler.Mensajes.ColaboradorNoEncontrado.Should().NotBeNullOrWhiteSpace();
        AsignarSedeCommandHandler.Mensajes.VinculacionTerminada.Should().NotBeNullOrWhiteSpace();
    }
}
