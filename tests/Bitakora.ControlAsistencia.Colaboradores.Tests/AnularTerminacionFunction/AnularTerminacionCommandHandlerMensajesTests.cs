// Issue #354: guardrail de resolucion del .resx del handler (MEF-ADR-0009), gemelo del que
// TerminarVinculacionCommandHandlerMensajesTests.cs aplica.
//
// Por que existe: AnularTerminacionCommandHandler.Mensajes devuelve ResourceManager.GetString(...)!
// -- el "!" es supresion del compilador, no garantia de runtime. Si la CLAVE desaparece del .resx
// (rename, merge que la pierde) GetString retorna null en silencio y los tests del handler pasan
// en FALSO: sus aserciones son WithMessage($"*{Mensajes.X}*"), que con X == null se vuelve "**" y
// matchea cualquier excepcion.

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
