// Issue #355: guardrail de resolucion del .resx del handler (MEF-ADR-0009), gemelo del que
// AnularTerminacionCommandHandlerMensajesTests.cs aplica.
//
// Por que existe: AsignarEtiquetaCommandHandler.Mensajes devuelve ResourceManager.GetString(...)!
// -- el "!" es supresion del compilador, no garantia de runtime. Si la CLAVE desaparece del .resx
// (rename, merge que la pierde) GetString retorna null en silencio y los tests del handler pasan
// en FALSO: sus aserciones son WithMessage($"*{Mensajes.X}*"), que con X == null se vuelve "**" y
// matchea cualquier excepcion.

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
