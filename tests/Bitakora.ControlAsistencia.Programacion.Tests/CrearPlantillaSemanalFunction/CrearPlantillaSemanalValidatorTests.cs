using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.CrearPlantillaSemanalFunction;
using Bitakora.ControlAsistencia.Programacion.CrearPlantillaSemanalFunction.CommandHandler;
using FluentValidation;

namespace Bitakora.ControlAsistencia.Programacion.Tests.CrearPlantillaSemanalFunction;

public class CrearPlantillaSemanalValidatorTests
{
    private readonly IValidator<CrearPlantillaSemanal> _validator = new CrearPlantillaSemanalValidator();

    [Fact]
    public async Task CrearPlantillaSemanal_EsValido_CuandoDatosSonCompletos()
    {
        var comando = new CrearPlantillaSemanal(Guid.NewGuid(), "Semana Cocina", 2);

        var resultado = await _validator.ValidateAsync(comando, TestContext.Current.CancellationToken);

        resultado.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task CrearPlantillaSemanal_EsInvalido_CuandoPlantillaIdEsGuidVacio()
    {
        var comando = new CrearPlantillaSemanal(Guid.Empty, "Semana Cocina", 2);

        var resultado = await _validator.ValidateAsync(comando, TestContext.Current.CancellationToken);

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.PropertyName == nameof(CrearPlantillaSemanal.PlantillaId));
    }

    [Fact]
    public async Task CrearPlantillaSemanal_EsInvalido_CuandoNombreEstaVacio()
    {
        var comando = new CrearPlantillaSemanal(Guid.NewGuid(), string.Empty, 2);

        var resultado = await _validator.ValidateAsync(comando, TestContext.Current.CancellationToken);

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.PropertyName == nameof(CrearPlantillaSemanal.Nombre));
    }

    // El rango de Semanas es invariante de dominio y vive en PlantillaSemanalCreada.Crear: el
    // validator solo cubre forma.
    [Fact]
    public async Task CrearPlantillaSemanal_EsValido_CuandoSemanasEstaFueraDeRango()
    {
        var comando = new CrearPlantillaSemanal(Guid.NewGuid(), "Semana Cocina", 0);

        var resultado = await _validator.ValidateAsync(comando, TestContext.Current.CancellationToken);

        resultado.IsValid.Should().BeTrue();
    }
}
