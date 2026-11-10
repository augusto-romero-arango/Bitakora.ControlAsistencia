// Por que existe este guardrail: ver TerminarVinculacionCommandHandlerMensajesTests.

using AwesomeAssertions;
using Bitakora.ControlAsistencia.Colaboradores.AsignarEtiquetaFunction.CommandHandler;

namespace Bitakora.ControlAsistencia.Colaboradores.Tests.AsignarEtiquetaFunction;

public class AsignarEtiquetaCommandHandlerMensajesTests
{
    [Fact]
    public void Mensajes_ResuelvenTextoNoVacio_CuandoPertenecenAAsignarEtiquetaCommandHandler()
    {
        AsignarEtiquetaCommandHandler.Mensajes.ColaboradorNoEncontrado.Should().NotBeNullOrWhiteSpace();
        AsignarEtiquetaCommandHandler.Mensajes.VinculacionTerminada.Should().NotBeNullOrWhiteSpace();
    }
}
