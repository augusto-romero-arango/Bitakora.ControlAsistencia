// Issue #355: guardrail de resolucion del .resx del handler (MEF-ADR-0009), gemelo del que
// AnularTerminacionCommandHandlerMensajesTests.cs aplica.
//
// Por que existe: RetirarEtiquetaCommandHandler.Mensajes devuelve ResourceManager.GetString(...)!
// -- el "!" es supresion del compilador, no garantia de runtime. Si la CLAVE desaparece del .resx
// (rename, merge que la pierde) GetString retorna null en silencio y los tests del handler pasan
// en FALSO: sus aserciones son WithMessage($"*{Mensajes.X}*"), que con X == null se vuelve "**" y
// matchea cualquier excepcion.

using AwesomeAssertions;
using Bitakora.ControlAsistencia.Colaboradores.RetirarEtiquetaFunction.CommandHandler;

namespace Bitakora.ControlAsistencia.Colaboradores.Tests.RetirarEtiquetaFunction;

public class RetirarEtiquetaCommandHandlerMensajesTests
{
    [Fact]
    public void Mensajes_ResuelvenTextoNoVacio_CuandoPertenecenARetirarEtiquetaCommandHandler()
    {
        RetirarEtiquetaCommandHandler.Mensajes.ColaboradorNoEncontrado.Should().NotBeNullOrWhiteSpace();
        RetirarEtiquetaCommandHandler.Mensajes.VinculacionTerminada.Should().NotBeNullOrWhiteSpace();
        RetirarEtiquetaCommandHandler.Mensajes.CategoriaInexistente.Should().NotBeNullOrWhiteSpace();
    }
}
