// Sin este guardrail, una clave perdida del .resx haria que GetString(...)! retorne null y los
// tests del handler pasarian en FALSO: su WithMessage($"*{Mensajes.X}*") se vuelve "**" con X null,
// que matchea cualquier excepcion.

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
