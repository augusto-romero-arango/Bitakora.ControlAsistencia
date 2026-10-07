using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.CrearJornadaFunction;
using Bitakora.ControlAsistencia.Programacion.CrearJornadaFunction.CommandHandler;

namespace Bitakora.ControlAsistencia.Programacion.Tests.CrearJornadaFunction;

public class CrearJornadaValidatorTests
{
    private readonly CrearJornadaValidator _validator = new();
    private static CrearJornada Comando() => new(Guid.NewGuid(), new(42, 0), new(8, 0), new(0, 0), 1);

    [Fact]
    public async Task CrearJornada_AceptaDatosCompletos()
    {
        var resultado = await _validator.ValidateAsync(Comando(), TestContext.Current.CancellationToken);
        resultado.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task CrearJornada_RechazaGuidVacio()
    {
        var resultado = await _validator.ValidateAsync(Comando() with { JornadaId = Guid.Empty }, TestContext.Current.CancellationToken);
        resultado.Errors.Should().Contain(e => e.PropertyName == nameof(CrearJornada.JornadaId));
    }

    [Fact]
    public async Task CrearJornada_RechazaCadaObjetoAusente()
    {
        foreach (var (comando, propiedad) in new[]
        {
            (Comando() with { HorasSemanales = null! }, nameof(CrearJornada.HorasSemanales)),
            (Comando() with { TopeDiario = null! }, nameof(CrearJornada.TopeDiario)),
            (Comando() with { MinimoDiario = null! }, nameof(CrearJornada.MinimoDiario))
        })
        {
            var resultado = await _validator.ValidateAsync(comando, TestContext.Current.CancellationToken);
            resultado.Errors.Should().Contain(e => e.PropertyName == propiedad);
        }
    }

    [Fact]
    public async Task CrearJornada_DelegaRangosAlDominio_CuandoValoresSonInvalidos()
    {
        var comando = Comando() with { HorasSemanales = new(-1, 60), DiasDescansoPorSemana = 7 };
        var resultado = await _validator.ValidateAsync(comando, TestContext.Current.CancellationToken);
        resultado.IsValid.Should().BeTrue();
    }
}
