// Issue #350: guardrail de resolucion del .resx del handler (MEF-ADR-0009), gemelo del que
// TerminarVinculacionCommandHandlerMensajesTests.cs aplica.
//
// Por que existe: ReingresarColaboradorCommandHandler.Mensajes devuelve
// ResourceManager.GetString(...)! -- el "!" es supresion del compilador, no garantia de runtime. Si
// la CLAVE desaparece del .resx (rename, merge que la pierde) GetString retorna null en silencio y
// los tests del handler pasan en FALSO: sus aserciones son WithMessage($"*{Mensajes.X}*"), que con
// X == null se vuelve "**" y matchea cualquier excepcion.

using AwesomeAssertions;
using Bitakora.ControlAsistencia.Colaboradores.ReingresarColaboradorFunction.CommandHandler;

namespace Bitakora.ControlAsistencia.Colaboradores.Tests.ReingresarColaboradorFunction;

public class ReingresarColaboradorCommandHandlerMensajesTests
{
    [Fact]
    public void Mensajes_ResuelvenTextoNoVacio_CuandoPertenecenAReingresarColaboradorCommandHandler()
    {
        ReingresarColaboradorCommandHandler.Mensajes.ColaboradorNoEncontrado.Should().NotBeNullOrWhiteSpace();
        ReingresarColaboradorCommandHandler.Mensajes.VinculacionAbierta.Should().NotBeNullOrWhiteSpace();
        ReingresarColaboradorCommandHandler.Mensajes.FechaSolapaVinculacionAnterior.Should().NotBeNullOrWhiteSpace();
    }
}
