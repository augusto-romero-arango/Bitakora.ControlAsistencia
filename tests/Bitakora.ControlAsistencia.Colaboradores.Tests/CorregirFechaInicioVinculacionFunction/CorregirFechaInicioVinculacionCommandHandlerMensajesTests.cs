// Issue #352: guardrail de resolucion del .resx del handler (MEF-ADR-0009), gemelo del que
// IniciarVinculacionCommandHandlerMensajesTests.cs aplica.
//
// Por que existe: CorregirFechaInicioVinculacionCommandHandler.Mensajes devuelve
// ResourceManager.GetString(...)! -- el "!" es supresion del compilador, no garantia de runtime. Si
// la CLAVE desaparece del .resx (rename, merge que la pierde) GetString retorna null en silencio y
// los tests del handler pasan en FALSO: sus aserciones son WithMessage($"*{Mensajes.X}*"), que con
// X == null se vuelve "**" y matchea cualquier excepcion.

using AwesomeAssertions;
using Bitakora.ControlAsistencia.Colaboradores.CorregirFechaInicioVinculacionFunction.CommandHandler;

namespace Bitakora.ControlAsistencia.Colaboradores.Tests.CorregirFechaInicioVinculacionFunction;

public class CorregirFechaInicioVinculacionCommandHandlerMensajesTests
{
    [Fact]
    public void Mensajes_ResuelvenTextoNoVacio_CuandoPertenecenACorregirFechaInicioVinculacionCommandHandler()
    {
        CorregirFechaInicioVinculacionCommandHandler.Mensajes.ColaboradorNoEncontrado
            .Should().NotBeNullOrWhiteSpace();
        CorregirFechaInicioVinculacionCommandHandler.Mensajes.FechaPosteriorATerminacionPropia
            .Should().NotBeNullOrWhiteSpace();
        CorregirFechaInicioVinculacionCommandHandler.Mensajes.FechaSolapaVinculacionAnterior
            .Should().NotBeNullOrWhiteSpace();
    }
}
