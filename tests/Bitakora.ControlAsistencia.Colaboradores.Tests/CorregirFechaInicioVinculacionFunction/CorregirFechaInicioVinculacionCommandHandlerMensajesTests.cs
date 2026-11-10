// Por que existe este guardrail: ver TerminarVinculacionCommandHandlerMensajesTests.

using AwesomeAssertions;
using Bitakora.ControlAsistencia.Colaboradores.CorregirFechaInicioVinculacionFunction.CommandHandler;

namespace Bitakora.ControlAsistencia.Colaboradores.Tests.CorregirFechaInicioVinculacionFunction;

public class CorregirFechaInicioVinculacionCommandHandlerMensajesTests
{
    [Fact]
    public void Mensajes_ResuelvenTextoNoVacio_CuandoPertenecenACorregirFechaInicioVinculacionCommandHandler()
    {
        CorregirFechaInicioVinculacionCommandHandler.Mensajes.ColaboradorNoEncontrado
            .Should().NotBeNullOrWhiteSpace();
        CorregirFechaInicioVinculacionCommandHandler.Mensajes.FechaPosteriorATerminacionPropia
            .Should().NotBeNullOrWhiteSpace();
        CorregirFechaInicioVinculacionCommandHandler.Mensajes.FechaSolapaVinculacionAnterior
            .Should().NotBeNullOrWhiteSpace();
        CorregirFechaInicioVinculacionCommandHandler.Mensajes.CodigoNoCorresponde
            .Should().NotBeNullOrWhiteSpace();
    }
}
