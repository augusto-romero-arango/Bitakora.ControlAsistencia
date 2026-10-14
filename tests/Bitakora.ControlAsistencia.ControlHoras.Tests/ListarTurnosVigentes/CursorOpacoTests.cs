using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.ListarTurnosVigentes;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.ListarTurnosVigentes;

public class CursorOpacoTests
{
    [Fact]
    public void TryDecodificar_RecuperaCodigoYFecha_CuandoElCursorSeCodificoAntes()
    {
        var cursor = CursorOpaco.Codificar("EMP-001", new DateOnly(2026, 10, 7));

        CursorOpaco.TryDecodificar(cursor, out var codigo, out var fecha).Should().BeTrue();
        codigo.Should().Be("EMP-001");
        fecha.Should().Be(new DateOnly(2026, 10, 7));
    }

    [Fact]
    public void Codificar_NoExponeElCodigoEnClaro() =>
        CursorOpaco.Codificar("EMP-001", new DateOnly(2026, 10, 7)).Should().NotContain("EMP-001");

    [Fact]
    public void TryDecodificar_RetornaFalse_CuandoElCursorNoEsBase64Url() =>
        CursorOpaco.TryDecodificar("@@no-base64@@", out _, out _).Should().BeFalse();

    [Fact]
    public void TryDecodificar_RetornaFalse_CuandoElCursorEsUnCodigoCrudo() =>
        CursorOpaco.TryDecodificar("EMP-001", out _, out _).Should().BeFalse();
}
