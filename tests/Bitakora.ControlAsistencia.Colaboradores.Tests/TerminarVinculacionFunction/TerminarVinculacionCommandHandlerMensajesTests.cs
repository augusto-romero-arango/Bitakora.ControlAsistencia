// Sin este guardrail, una clave perdida del .resx haria que GetString(...)! retorne null y los
// tests del handler pasarian en FALSO: su WithMessage($"*{Mensajes.X}*") se vuelve "**" con X null,
// que matchea cualquier excepcion.

using AwesomeAssertions;
using Bitakora.ControlAsistencia.Colaboradores.TerminarVinculacionFunction.CommandHandler;

namespace Bitakora.ControlAsistencia.Colaboradores.Tests.TerminarVinculacionFunction;

public class TerminarVinculacionCommandHandlerMensajesTests
{
    [Fact]
    public void Mensajes_ResuelvenTextoNoVacio_CuandoPertenecenATerminarVinculacionCommandHandler()
    {
        TerminarVinculacionCommandHandler.Mensajes.ColaboradorNoEncontrado.Should().NotBeNullOrWhiteSpace();
        TerminarVinculacionCommandHandler.Mensajes.VinculacionYaTerminada.Should().NotBeNullOrWhiteSpace();
        TerminarVinculacionCommandHandler.Mensajes.FechaAnteriorAInicio.Should().NotBeNullOrWhiteSpace();
        TerminarVinculacionCommandHandler.Mensajes.CodigoNoCorresponde.Should().NotBeNullOrWhiteSpace();
    }
}
