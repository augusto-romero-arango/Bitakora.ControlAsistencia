using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.ProgramarAusenciaFunction;
using Bitakora.ControlAsistencia.Programacion.ProgramarAusenciaFunction.CommandHandler;

namespace Bitakora.ControlAsistencia.Programacion.Tests.ProgramarAusenciaFunction;

public class ProgramarAusenciaBodyValidatorTests
{
    private readonly ProgramarAusenciaBodyValidator _validator = new();

    private static ProgramarAusenciaBody BodyValido() => new(
        Guid.NewGuid(), "CC-12345678", "Ana Maria Gomez",
        new DateOnly(2026, 10, 5), new DateOnly(2026, 10, 7), "Vacaciones");

    private async Task<FluentValidation.Results.ValidationResult> Validar(ProgramarAusenciaBody body) =>
        await _validator.ValidateAsync(body, TestContext.Current.CancellationToken);

    [Fact]
    public async Task Body_EsValido_CuandoTodosLosCamposSonCorrectos()
    {
        var resultado = await Validar(BodyValido());

        resultado.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Body_EsValido_CuandoInicioYFinSonLaMismaFecha()
    {
        var resultado = await Validar(BodyValido() with { FechaFin = new DateOnly(2026, 10, 5) });

        resultado.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Body_EsValido_CuandoElMotivoVieneEnMinusculas()
    {
        var resultado = await Validar(BodyValido() with { Motivo = "incapacidadmedica" });

        resultado.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Body_EsValido_CuandoLasFechasEstanEnElPasadoYElRangoEsLargo()
    {
        var resultado = await Validar(BodyValido() with
        {
            FechaInicio = new DateOnly(2020, 1, 1),
            FechaFin = new DateOnly(2020, 5, 5)
        });

        resultado.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Body_TieneError_CuandoFechaFinEsAnteriorAFechaInicio()
    {
        var resultado = await Validar(BodyValido() with { FechaFin = new DateOnly(2026, 10, 4) });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.PropertyName == nameof(ProgramarAusenciaBody.FechaFin));
    }

    [Fact]
    public async Task Body_TieneError_CuandoElMotivoNoEstaEnLaLista()
    {
        var resultado = await Validar(BodyValido() with { Motivo = "Teletrabajo" });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.PropertyName == nameof(ProgramarAusenciaBody.Motivo));
    }

    [Fact]
    public async Task Body_TieneError_CuandoElMotivoEstaVacio()
    {
        var resultado = await Validar(BodyValido() with { Motivo = "" });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.PropertyName == nameof(ProgramarAusenciaBody.Motivo));
    }

    [Fact]
    public async Task Body_TieneError_CuandoElIdEsGuidVacio()
    {
        var resultado = await Validar(BodyValido() with { Id = Guid.Empty });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.PropertyName == nameof(ProgramarAusenciaBody.Id));
    }

    [Fact]
    public async Task Body_TieneError_CuandoLaIdentificacionEstaVacia()
    {
        var resultado = await Validar(BodyValido() with { Identificacion = "" });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.PropertyName == nameof(ProgramarAusenciaBody.Identificacion));
    }

    [Fact]
    public async Task Body_TieneError_CuandoElNombreCompletoEstaVacio()
    {
        var resultado = await Validar(BodyValido() with { NombreCompleto = "" });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.PropertyName == nameof(ProgramarAusenciaBody.NombreCompleto));
    }

    [Fact]
    public async Task Body_TieneError_CuandoFechaInicioNoViene()
    {
        var resultado = await Validar(BodyValido() with { FechaInicio = default });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.PropertyName == nameof(ProgramarAusenciaBody.FechaInicio));
    }

    [Fact]
    public async Task Body_TieneError_CuandoFechaFinNoViene()
    {
        var resultado = await Validar(BodyValido() with { FechaFin = default });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.PropertyName == nameof(ProgramarAusenciaBody.FechaFin));
    }
}
