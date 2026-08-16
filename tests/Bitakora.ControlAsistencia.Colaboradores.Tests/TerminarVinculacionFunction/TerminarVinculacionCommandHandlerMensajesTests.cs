// Issue #349: guardrail de resolucion del .resx del handler (MEF-ADR-0009), gemelo del que
// ValueObjects/MensajesResxTests.cs ya aplica a los .resx de los VOs.
//
// Por que existe: TerminarVinculacionCommandHandler.Mensajes devuelve
// ResourceManager.GetString(...)! -- el "!" es supresion del compilador, no garantia de runtime. Si
// la CLAVE desaparece del .resx (rename, merge que la pierde) GetString retorna null en silencio y
// los tests del handler pasan en FALSO: sus aserciones son WithMessage($"*{Mensajes.X}*"), que con
// X == null se vuelve "**" y matchea cualquier excepcion.
//
// El modo de falla es exactamente el que se verifico por mutacion durante la revision de #348, pero
// aqui es mas amplio: las tres claves de este handler son la unica evidencia de que un 404 o un 409
// llevan mensaje de dominio, y ninguna otra prueba del issue las verificaria vacias.

using AwesomeAssertions;
using Bitakora.ControlAsistencia.Colaboradores.TerminarVinculacionFunction.CommandHandler;

namespace Bitakora.ControlAsistencia.Colaboradores.Tests.TerminarVinculacionFunction;

public class TerminarVinculacionCommandHandlerMensajesTests
{
    [Fact]
    public void Mensajes_ResuelvenTextoNoVacio_CuandoPertenecenATerminarVinculacionCommandHandler()
    {
        TerminarVinculacionCommandHandler.Mensajes.ColaboradorNoEncontrado.Should().NotBeNullOrWhiteSpace();
        TerminarVinculacionCommandHandler.Mensajes.VinculacionYaTerminada.Should().NotBeNullOrWhiteSpace();
        TerminarVinculacionCommandHandler.Mensajes.FechaAnteriorAInicio.Should().NotBeNullOrWhiteSpace();
        TerminarVinculacionCommandHandler.Mensajes.CodigoNoCorresponde.Should().NotBeNullOrWhiteSpace();
    }
}
