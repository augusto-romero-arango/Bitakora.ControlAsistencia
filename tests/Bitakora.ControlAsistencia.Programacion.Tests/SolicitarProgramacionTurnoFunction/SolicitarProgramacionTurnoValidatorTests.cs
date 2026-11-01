using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.Programacion.SolicitarProgramacionTurnoFunction;
using Bitakora.ControlAsistencia.Programacion.SolicitarProgramacionTurnoFunction.CommandHandler;

namespace Bitakora.ControlAsistencia.Programacion.Tests.SolicitarProgramacionTurnoFunction;

public class SolicitarProgramacionTurnoValidatorTests
{
    private readonly SolicitarProgramacionTurnoValidator _validator = new();

    private static ColaboradorSolicitado ColaboradorValido() => new("CC-12345678", "E001", "Juan Perez");

    private static SolicitarProgramacionTurno Valido() => new(
        Guid.NewGuid(), Guid.NewGuid(),
        ColaboradorValido(),
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

    [Fact]
    public void Validar_Falla_CuandoTurnoIdEsGuidVacio()
    {
        var resultado = _validator.Validate(Valido() with { TurnoId = Guid.Empty });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.PropertyName == nameof(SolicitarProgramacionTurno.TurnoId));
    }

    [Fact]
    public void Validar_Falla_CuandoCodigoColaboradorEstaVacio()
    {
        var resultado = _validator.Validate(
            Valido() with { Colaborador = ColaboradorValido() with { CodigoColaborador = "" } });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.PropertyName.Contains(nameof(ColaboradorSolicitado.CodigoColaborador)));
    }

    [Fact]
    public void Validar_Falla_CuandoIdentificacionEstaVacia()
    {
        var resultado = _validator.Validate(
            Valido() with { Colaborador = ColaboradorValido() with { Identificacion = "" } });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.PropertyName.Contains(nameof(ColaboradorSolicitado.Identificacion)));
    }

    [Fact]
    public void Validar_Falla_CuandoNombreCompletoEstaVacio()
    {
        var resultado = _validator.Validate(
            Valido() with { Colaborador = ColaboradorValido() with { NombreCompleto = "   " } });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.PropertyName.Contains(nameof(ColaboradorSolicitado.NombreCompleto)));
    }

    [Fact]
    public void Validar_TieneErrorEnColaborador_CuandoColaboradorEsNull()
    {
        var resultado = _validator.Validate(Valido() with { Colaborador = null! });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.PropertyName == nameof(SolicitarProgramacionTurno.Colaborador));
    }

    [Fact]
    public void Validar_EsValido_CuandoSedeEsNull()
    {
        _validator.Validate(Valido() with { Sede = null }).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validar_EsValido_CuandoSedeTieneIdYNombre()
    {
        _validator.Validate(Valido() with { Sede = new SedeProgramada("SEDE-01", "Sede Principal") })
            .IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validar_TieneErrorEnSedeId_CuandoSedeTieneIdVacio()
    {
        var resultado = _validator.Validate(Valido() with { Sede = new SedeProgramada("", "Sede Principal") });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.PropertyName.Contains(nameof(SedeProgramada.Id)));
    }

    [Fact]
    public void Validar_TieneErrorEnSedeNombre_CuandoSedeTieneNombreEnBlanco()
    {
        var resultado = _validator.Validate(Valido() with { Sede = new SedeProgramada("SEDE-01", "   ") });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.PropertyName.Contains(nameof(SedeProgramada.Nombre)));
    }

    [Fact]
    public void Validar_EsValido_CuandoNoTraeJornadaId()
    {
        _validator.Validate(Valido() with { JornadaId = null }).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validar_EsValido_CuandoTraeJornadaIdConValor()
    {
        _validator.Validate(Valido() with { JornadaId = Guid.NewGuid() }).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validar_Falla_CuandoJornadaIdEsGuidVacio()
    {
        var resultado = _validator.Validate(Valido() with { JornadaId = Guid.Empty });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.PropertyName == nameof(SolicitarProgramacionTurno.JornadaId));
    }
}
