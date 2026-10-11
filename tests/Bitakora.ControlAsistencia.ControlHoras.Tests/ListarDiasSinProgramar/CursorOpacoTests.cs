using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.ListarDiasSinProgramar;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.ListarDiasSinProgramar;

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
}
