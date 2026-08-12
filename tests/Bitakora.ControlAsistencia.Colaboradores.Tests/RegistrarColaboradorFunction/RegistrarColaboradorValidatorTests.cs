// Issue #330: validacion de forma del comando RegistrarColaborador en el borde (CA-3).
// Patron de referencia: RegistrarMarcacionValidatorTests (ValidateAsync + verificacion de
// PropertyName en resultado.Errors).

using AwesomeAssertions;
using Bitakora.ControlAsistencia.Colaboradores.RegistrarColaboradorFunction.CommandHandler;
using Bitakora.ControlAsistencia.Colaboradores.RegistrarColaboradorFunction;
using FluentValidation.Results;

namespace Bitakora.ControlAsistencia.Colaboradores.Tests.RegistrarColaboradorFunction;

public class RegistrarColaboradorValidatorTests
{
    private readonly RegistrarColaboradorValidator _validator = new();

    private static RegistrarColaborador ComandoValido() => new(
        TipoIdentificacion: "CC",
        NumeroIdentificacion: "79543210",
        PrimerNombre: "Luis",
        SegundoNombre: "Augusto",
        PrimerApellido: "Barreto",
        SegundoApellido: "Prieto",
        CodigoColaborador: "COL-001",
        FechaInicio: new DateOnly(2026, 1, 15));

    private Task<ValidationResult> Validar(RegistrarColaborador comando) =>
        _validator.ValidateAsync(comando, TestContext.Current.CancellationToken);

    // Camino feliz -- todos los campos correctos
    [Fact]
    public async Task Validar_Aprueba_CuandoTodosLosCamposSonCorrectos()
    {
        var resultado = await Validar(ComandoValido());

        resultado.IsValid.Should().BeTrue();
    }

    // CA-3: TipoIdentificacion vacio produce 400
    [Fact]
    public async Task Validar_RechazaTipoIdentificacion_CuandoEstaVacio()
    {
        var resultado = await Validar(ComandoValido() with { TipoIdentificacion = "" });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e =>
            e.PropertyName == nameof(RegistrarColaborador.TipoIdentificacion));
    }

    // CA-3: TipoIdentificacion fuera de la lista cerrada (PILA: CC/CE/TI/PA/PT) produce 400, no 500
    [Fact]
    public async Task Validar_RechazaTipoIdentificacion_CuandoNoEstaEnLaListaCerrada()
    {
        var resultado = await Validar(ComandoValido() with { TipoIdentificacion = "XX" });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e =>
            e.PropertyName == nameof(RegistrarColaborador.TipoIdentificacion));
    }

    // CA-4: TipoIdentificacion en minusculas ("cc") DEBE seguir siendo valido -- el validator no
    // juzga formato del codigo de tipo; la normalizacion (trim+MAYUSCULAS) vive dentro de
    // TipoIdentificacion.Desde (issue #371). Sin ella, un POST legitimo con "cc" terminaria en 400
    // en vez de 409/202.
    [Fact]
    public async Task Validar_Aprueba_CuandoTipoIdentificacionLlegaEnMinusculas()
    {
        var resultado = await Validar(ComandoValido() with { TipoIdentificacion = "cc" });

        resultado.IsValid.Should().BeTrue();
    }

    // CA-3: NumeroIdentificacion vacio produce 400
    [Fact]
    public async Task Validar_RechazaNumeroIdentificacion_CuandoEstaVacio()
    {
        var resultado = await Validar(ComandoValido() with { NumeroIdentificacion = "" });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e =>
            e.PropertyName == nameof(RegistrarColaborador.NumeroIdentificacion));
    }

    // CA-3: PrimerNombre vacio produce 400
    [Fact]
    public async Task Validar_RechazaPrimerNombre_CuandoEstaVacio()
    {
        var resultado = await Validar(ComandoValido() with { PrimerNombre = "" });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e =>
            e.PropertyName == nameof(RegistrarColaborador.PrimerNombre));
    }

    // CA-3: PrimerApellido vacio produce 400
    [Fact]
    public async Task Validar_RechazaPrimerApellido_CuandoEstaVacio()
    {
        var resultado = await Validar(ComandoValido() with { PrimerApellido = "" });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e =>
            e.PropertyName == nameof(RegistrarColaborador.PrimerApellido));
    }

    // CA-3: CodigoColaborador vacio produce 400
    [Fact]
    public async Task Validar_RechazaCodigoColaborador_CuandoEstaVacio()
    {
        var resultado = await Validar(ComandoValido() with { CodigoColaborador = "" });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e =>
            e.PropertyName == nameof(RegistrarColaborador.CodigoColaborador));
    }

    // CA-3: FechaInicio con el valor default de DateOnly produce 400 -- el tiempo de los hechos
    // viene del cliente, nunca del reloj del servidor (doctrina bitemporal del BC).
    [Fact]
    public async Task Validar_RechazaFechaInicio_CuandoEsDefault()
    {
        var resultado = await Validar(ComandoValido() with { FechaInicio = default });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e =>
            e.PropertyName == nameof(RegistrarColaborador.FechaInicio));
    }

    // SegundoNombre es opcional -- null no debe generar error
    [Fact]
    public async Task Validar_Aprueba_CuandoSegundoNombreEsNull()
    {
        var resultado = await Validar(ComandoValido() with { SegundoNombre = null });

        resultado.IsValid.Should().BeTrue();
    }

    // SegundoApellido es opcional -- null no debe generar error
    [Fact]
    public async Task Validar_Aprueba_CuandoSegundoApellidoEsNull()
    {
        var resultado = await Validar(ComandoValido() with { SegundoApellido = null });

        resultado.IsValid.Should().BeTrue();
    }
}
