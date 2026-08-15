// Issue #351: guardrail de resolucion del .resx del handler (MEF-ADR-0009), gemelo de
// TerminarVinculacionCommandHandlerMensajesTests/IniciarVinculacionCommandHandlerMensajesTests.
//
// Por que existe: CorregirNombresCommandHandler.Mensajes devuelve ResourceManager.GetString(...)!
// -- el "!" es supresion del compilador, no garantia de runtime. Si la CLAVE desaparece del .resx
// (rename, merge que la pierde) GetString retorna null en silencio y el test del handler que
// verifica el 404 pasa en FALSO: su asercion es WithMessage($"*{Mensajes.X}*"), que con X == null
// se vuelve "**" y matchea cualquier excepcion.

using AwesomeAssertions;
using Bitakora.ControlAsistencia.Colaboradores.CorregirNombresFunction.CommandHandler;

namespace Bitakora.ControlAsistencia.Colaboradores.Tests.CorregirNombresFunction;

public class CorregirNombresCommandHandlerMensajesTests
{
    [Fact]
    public void Mensajes_ResuelvenTextoNoVacio_CuandoPertenecenACorregirNombresCommandHandler()
    {
        CorregirNombresCommandHandler.Mensajes.ColaboradorNoEncontrado.Should().NotBeNullOrWhiteSpace();
    }
}
