// Issue #465: guardrail de resolucion del .resx del handler (MEF-ADR-0009), gemelo del que
// AsignarEtiquetaCommandHandlerMensajesTests.cs aplica.
//
// Por que existe: AsignarSedeCommandHandler.Mensajes devuelve ResourceManager.GetString(...)! -- el
// "!" es supresion del compilador, no garantia de runtime. Si la CLAVE desaparece del .resx (rename,
// merge que la pierde) GetString retorna null en silencio y los tests del handler pasan en FALSO:
// sus aserciones son WithMessage($"*{Mensajes.X}*"), que con X == null se vuelve "**" y matchea
// cualquier excepcion.

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
