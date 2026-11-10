// Por que existe este guardrail: ver TerminarVinculacionCommandHandlerMensajesTests.

using AwesomeAssertions;
using Bitakora.ControlAsistencia.Colaboradores.IniciarVinculacionFunction.CommandHandler;

namespace Bitakora.ControlAsistencia.Colaboradores.Tests.IniciarVinculacionFunction;

public class IniciarVinculacionCommandHandlerMensajesTests
{
    [Fact]
    public void Mensajes_ResuelvenTextoNoVacio_CuandoPertenecenAIniciarVinculacionCommandHandler()
    {
        IniciarVinculacionCommandHandler.Mensajes.ColaboradorNoEncontrado.Should().NotBeNullOrWhiteSpace();
        IniciarVinculacionCommandHandler.Mensajes.VinculacionAbierta.Should().NotBeNullOrWhiteSpace();
        IniciarVinculacionCommandHandler.Mensajes.FechaSolapaVinculacionAnterior.Should().NotBeNullOrWhiteSpace();
    }
}
