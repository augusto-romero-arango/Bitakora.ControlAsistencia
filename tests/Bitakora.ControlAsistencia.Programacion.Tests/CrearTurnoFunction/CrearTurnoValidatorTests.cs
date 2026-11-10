using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.CrearTurnoFunction;
using Bitakora.ControlAsistencia.Programacion.CrearTurnoFunction.CommandHandler;
using FluentValidation;

namespace Bitakora.ControlAsistencia.Programacion.Tests.CrearTurnoFunction;

public class CrearTurnoValidatorTests
{
    private const string NombreTurno = "Turno Manana";

    private static CrearTurno.Franja FranjaDiurnaSimple() =>
        new(new TimeOnly(8, 0), new TimeOnly(16, 0), [], []);

    private static CrearTurno ComandoConUnaFranja(Guid turnoId) =>
        new(turnoId, NombreTurno, [FranjaDiurnaSimple()]);

    private readonly IValidator<CrearTurno> _validator = new CrearTurnoValidator();

    [Fact]
    public async Task DebeSerValido_CuandoDatosCompletos()
    {
        var comando = ComandoConUnaFranja(Guid.NewGuid());
        var resultado = await _validator.ValidateAsync(
            comando, TestContext.Current.CancellationToken);
        resultado.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task DebeRechazar_CuandoTurnoIdEsGuidVacio()
    {
        var comando = new CrearTurno(
            Guid.Empty, NombreTurno, [FranjaDiurnaSimple()]);
        var resultado = await _validator.ValidateAsync(
            comando, TestContext.Current.CancellationToken);
        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should()
            .Contain(e => e.PropertyName == nameof(CrearTurno.TurnoId));
    }

    [Fact]
    public async Task DebeRechazar_CuandoNombreEstaVacio()
    {
        var comando = new CrearTurno(
            Guid.NewGuid(), string.Empty, [FranjaDiurnaSimple()]);
        var resultado = await _validator.ValidateAsync(
            comando, TestContext.Current.CancellationToken);
        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should()
            .Contain(e => e.PropertyName == nameof(CrearTurno.Nombre));
    }

    // Un turno nace vacio y se completa por pasos (CA-ADR-0033): Ordinarias vacia (o ausente) es
    // valida cuando no se marca como descanso.
    [Fact]
    public async Task CrearTurno_EsValido_CuandoOrdinariasEsListaVaciaYNoEsDescanso()
    {
        var comando = new CrearTurno(Guid.NewGuid(), NombreTurno, []);
        var resultado = await _validator.ValidateAsync(
            comando, TestContext.Current.CancellationToken);
        resultado.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task CrearTurno_EsValido_CuandoOrdinariasEsNull()
    {
        var comando = new CrearTurno(Guid.NewGuid(), NombreTurno, null);
        var resultado = await _validator.ValidateAsync(
            comando, TestContext.Current.CancellationToken);
        resultado.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task CrearTurno_EsValido_CuandoEsDescansoYOrdinariasVacia()
    {
        var comando = new CrearTurno(Guid.NewGuid(), NombreTurno, [], EsDescanso: true);
        var resultado = await _validator.ValidateAsync(
            comando, TestContext.Current.CancellationToken);
        resultado.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task CrearTurno_EsInvalido_CuandoEsDescansoTraeFranjasOrdinarias()
    {
        var comando = new CrearTurno(
            Guid.NewGuid(), NombreTurno, [FranjaDiurnaSimple()], EsDescanso: true);
        var resultado = await _validator.ValidateAsync(
            comando, TestContext.Current.CancellationToken);
        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should()
            .Contain(e => e.ErrorMessage.Contains(CrearTurnoValidator.Mensajes.EsDescansoConFranjas));
    }
}
