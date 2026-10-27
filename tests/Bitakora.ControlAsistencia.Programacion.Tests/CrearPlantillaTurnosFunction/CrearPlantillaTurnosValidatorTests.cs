using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.CrearPlantillaTurnosFunction;
using Bitakora.ControlAsistencia.Programacion.CrearPlantillaTurnosFunction.CommandHandler;
using FluentValidation;

namespace Bitakora.ControlAsistencia.Programacion.Tests.CrearPlantillaTurnosFunction;

public class CrearPlantillaTurnosValidatorTests
{
    private readonly IValidator<CrearPlantillaTurnos> _validator = new CrearPlantillaTurnosValidator();

    [Fact]
    public async Task CrearPlantillaTurnos_EsValido_CuandoDatosSonCompletos()
    {
        var comando = new CrearPlantillaTurnos(Guid.NewGuid(), "Ciclo 8 dias", 8);

        var resultado = await _validator.ValidateAsync(comando, TestContext.Current.CancellationToken);

        resultado.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task CrearPlantillaTurnos_EsInvalido_CuandoPlantillaIdEsGuidVacio()
    {
        var comando = new CrearPlantillaTurnos(Guid.Empty, "Ciclo 8 dias", 8);

        var resultado = await _validator.ValidateAsync(comando, TestContext.Current.CancellationToken);

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.PropertyName == nameof(CrearPlantillaTurnos.PlantillaId));
    }

    [Fact]
    public async Task CrearPlantillaTurnos_EsInvalido_CuandoNombreEstaVacio()
    {
        var comando = new CrearPlantillaTurnos(Guid.NewGuid(), string.Empty, 8);

        var resultado = await _validator.ValidateAsync(comando, TestContext.Current.CancellationToken);

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.PropertyName == nameof(CrearPlantillaTurnos.Nombre));
    }

    [Fact]
    public async Task CrearPlantillaTurnos_EsValido_CuandoDiasDelCicloEstaFueraDeRango()
    {
        var comando = new CrearPlantillaTurnos(Guid.NewGuid(), "Ciclo 8 dias", 0);

        var resultado = await _validator.ValidateAsync(comando, TestContext.Current.CancellationToken);

        resultado.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task CrearPlantillaTurnos_EsInvalido_CuandoIdYNombreEstanVacios()
    {
        var comando = new CrearPlantillaTurnos(Guid.Empty, string.Empty, 8);

        var resultado = await _validator.ValidateAsync(comando, TestContext.Current.CancellationToken);

        resultado.Errors.Should().HaveCountGreaterThanOrEqualTo(2);
    }
}
