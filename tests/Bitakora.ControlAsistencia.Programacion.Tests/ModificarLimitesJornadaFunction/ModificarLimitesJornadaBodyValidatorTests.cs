using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.CrearJornadaFunction;
using Bitakora.ControlAsistencia.Programacion.ModificarLimitesJornadaFunction;

namespace Bitakora.ControlAsistencia.Programacion.Tests.ModificarLimitesJornadaFunction;

public class ModificarLimitesJornadaBodyValidatorTests
{
    private static readonly HorasYMinutosSolicitadas Semanales = new(44, 0);
    private static readonly HorasYMinutosSolicitadas Tope = new(10, 0);
    private static readonly HorasYMinutosSolicitadas Minimo = new(4, 0);

    private readonly ModificarLimitesJornadaBodyValidator _validator = new();

    private async Task<FluentValidation.Results.ValidationResult> Validar(ModificarLimitesJornadaBody body) =>
        await _validator.ValidateAsync(body, TestContext.Current.CancellationToken);

    [Fact]
    public async Task Body_EsValido_CuandoTraeLosTresLimites()
    {
        var resultado = await Validar(new ModificarLimitesJornadaBody(Semanales, Tope, Minimo, 1));

        resultado.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Body_EsInvalido_CuandoFaltanLasHorasSemanales()
    {
        var resultado = await Validar(new ModificarLimitesJornadaBody(null!, Tope, Minimo, 1));

        resultado.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Body_EsInvalido_CuandoFaltaElTopeDiario()
    {
        var resultado = await Validar(new ModificarLimitesJornadaBody(Semanales, null!, Minimo, 1));

        resultado.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Body_EsInvalido_CuandoFaltaElMinimoDiario()
    {
        var resultado = await Validar(new ModificarLimitesJornadaBody(Semanales, Tope, null!, 1));

        resultado.IsValid.Should().BeFalse();
    }
}
