using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.ListarMallaSemanalTurnos;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.ListarMallaSemanalTurnos;

public class CursorOpacoTests
{
    [Fact]
    public void TryDecodificar_RecuperaElCodigo_CuandoElCursorSeCodificoAntes()
    {
        CursorOpaco.TryDecodificar(CursorOpaco.Codificar("EMP-001"), out var codigo).Should().BeTrue();
        codigo.Should().Be("EMP-001");
    }

    [Fact]
    public void Codificar_NoExponeElCodigoEnClaro() =>
        CursorOpaco.Codificar("EMP-001").Should().NotContain("EMP-001");

    [Fact]
    public void TryDecodificar_RetornaFalse_CuandoElCursorNoEsBase64Url() =>
        CursorOpaco.TryDecodificar("@@no-base64@@", out _).Should().BeFalse();

    [Fact]
    public void TryDecodificar_RetornaFalse_CuandoElCursorEsVacio() =>
        CursorOpaco.TryDecodificar("", out _).Should().BeFalse();
}
