// Issue #378: guardrail de resolucion del .resx del handler (MEF-ADR-0009), gemelo del que
// TerminarVinculacionCommandHandlerMensajesTests.cs aplica. Reemplaza a
// ReingresarColaboradorCommandHandlerMensajesTests.cs (issue #350).
//
// Por que existe: IniciarVinculacionCommandHandler.Mensajes devuelve
// ResourceManager.GetString(...)! -- el "!" es supresion del compilador, no garantia de runtime. Si
// la CLAVE desaparece del .resx (rename, merge que la pierde) GetString retorna null en silencio y
// los tests del handler pasan en FALSO: sus aserciones son WithMessage($"*{Mensajes.X}*"), que con
// X == null se vuelve "**" y matchea cualquier excepcion.

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
