using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.SolicitarProgramacionTurnoEnFechasFunction;
using Bitakora.ControlAsistencia.Programacion.SolicitarProgramacionTurnoEnFechasFunction.CommandHandler;
using Bitakora.ControlAsistencia.Programacion.SolicitarProgramacionTurnoFunction;

namespace Bitakora.ControlAsistencia.Programacion.Tests.SolicitarProgramacionTurnoEnFechasFunction;

public class SolicitarProgramacionTurnoEnFechasValidatorTests
{
    private readonly SolicitarProgramacionTurnoEnFechasValidator _validator = new();

    private static SolicitarProgramacionTurnoEnFechas Valido() => new(
        Guid.NewGuid(), Guid.NewGuid(),
        new ColaboradorSolicitado("CC-12345678", "E001", "Juan Perez"),
        [new DateOnly(2026, 4, 7)]);

    [Fact]
    public void Validar_EsValido_CuandoElComandoEstaCompleto()
    {
        _validator.Validate(Valido()).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validar_Falla_CuandoNoHayFechas()
    {
        var resultado = _validator.Validate(Valido() with { Fechas = [] });

        resultado.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validar_Falla_CuandoElIdEstaVacio()
    {
        var resultado = _validator.Validate(Valido() with { Id = Guid.Empty });

        resultado.IsValid.Should().BeFalse();
    }
}
