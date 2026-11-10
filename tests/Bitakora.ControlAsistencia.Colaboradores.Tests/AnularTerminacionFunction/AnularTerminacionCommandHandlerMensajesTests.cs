// Guardrail del .resx (MEF-ADR-0009): el "!" de ResourceManager.GetString(...)! no garantiza nada en
// runtime. Si la clave desaparece, GetString da null y las aserciones WithMessage($"*{Mensajes.X}*")
// de los tests del handler se vuelven "**" y pasan en falso.

using AwesomeAssertions;
using Bitakora.ControlAsistencia.Colaboradores.AnularTerminacionFunction.CommandHandler;

namespace Bitakora.ControlAsistencia.Colaboradores.Tests.AnularTerminacionFunction;

public class AnularTerminacionCommandHandlerMensajesTests
{
    [Fact]
    public void Mensajes_ResuelvenTextoNoVacio_CuandoPertenecenAAnularTerminacionCommandHandler()
    {
        AnularTerminacionCommandHandler.Mensajes.ColaboradorNoEncontrado.Should().NotBeNullOrWhiteSpace();
        AnularTerminacionCommandHandler.Mensajes.VinculacionAbierta.Should().NotBeNullOrWhiteSpace();
        AnularTerminacionCommandHandler.Mensajes.CodigoNoCorresponde.Should().NotBeNullOrWhiteSpace();
    }
}
