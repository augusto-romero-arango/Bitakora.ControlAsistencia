using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.ListarAdvertenciasProgramacionSemanal;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.ListarAdvertenciasProgramacionSemanal;

public class CursorOpacoTests
{
    [Fact]
    public void TryDecodificar_RecuperaElCodigo_CuandoElCursorSeCodificoAntes()
    {
        var cursor = CursorOpaco.Codificar("EMP-001");

        CursorOpaco.TryDecodificar(cursor, out var codigo).Should().BeTrue();
        codigo.Should().Be("EMP-001");
    }

    [Fact]
    public void Codificar_NoExponeElCodigoEnClaro()
    {
        CursorOpaco.Codificar("EMP-001").Should().NotContain("EMP-001");
    }

    [Fact]
    public void TryDecodificar_RetornaFalse_CuandoElCursorNoEsBase64Url()
    {
        CursorOpaco.TryDecodificar("@@no-base64@@", out _).Should().BeFalse();
    }
}
