using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.ListarAsistenciasDiarias;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.ListarAsistenciasDiarias;

public class CursorOpacoTests
{
    [Fact]
    public void TryDecodificar_RecuperaLaFecha_CuandoElCursorSeCodificoAntes()
    {
        CursorOpaco.TryDecodificar(CursorOpaco.Codificar(new DateOnly(2026, 10, 7)), out var fecha).Should().BeTrue();
        fecha.Should().Be(new DateOnly(2026, 10, 7));
    }

    [Fact]
    public void Codificar_NoExponeLaFechaEnClaro() =>
        CursorOpaco.Codificar(new DateOnly(2026, 10, 7)).Should().NotContain("2026-10-07");

    [Fact]
    public void TryDecodificar_RetornaFalse_CuandoElCursorNoEsBase64Url() =>
        CursorOpaco.TryDecodificar("@@no-base64@@", out _).Should().BeFalse();

    [Fact]
    public void TryDecodificar_RetornaFalse_CuandoElCursorEsUnaFechaCruda() =>
        CursorOpaco.TryDecodificar("2026-10-07", out _).Should().BeFalse();
}
