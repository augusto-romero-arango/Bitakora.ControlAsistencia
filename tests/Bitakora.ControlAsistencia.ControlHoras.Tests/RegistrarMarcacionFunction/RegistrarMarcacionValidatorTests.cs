// Issue #279: Validar el comando RegistrarMarcacion en el borde
// RegistrarMarcacion es el unico comando HTTP del repo sin validator y su endpoint es
// publico y anonimo (sin APIM, sin restriccion de red). Este archivo cubre las reglas de
// forma que deben rechazar un request antes de que llegue al command handler.
//
// Patron de referencia: SolicitarProgramacionTurnoValidatorTests (mismo estilo de
// ValidateAsync + verificacion de PropertyName en resultado.Errors).
//
// El contrato que consume el endpoint es doble: IsValid decide el 400 (ver RequestValidator)
// y Errors define el ValidationProblemDetails que viaja al cliente. Por eso cada rechazo
// afirma ambos: el resultado global invalido y la propiedad culpable.

using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.RegistrarMarcacionFunction;
using Bitakora.ControlAsistencia.ControlHoras.RegistrarMarcacionFunction.CommandHandler;
using FluentValidation.Results;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.RegistrarMarcacionFunction;

public class RegistrarMarcacionValidatorTests
{
    private readonly RegistrarMarcacionValidator _validator = new();

    private static RegistrarMarcacion ComandoValido() =>
        new("EMP-001", new DateTime(2026, 3, 15, 8, 9, 59), "ENTRADA", "DEV-001");

    private Task<ValidationResult> Validar(RegistrarMarcacion comando) =>
        _validator.ValidateAsync(comando, TestContext.Current.CancellationToken);

    // Camino feliz - todos los campos correctos
    [Fact]
    public async Task Validar_Aprueba_CuandoTodosLosCamposSonCorrectos()
    {
        var resultado = await Validar(ComandoValido());

        resultado.IsValid.Should().BeTrue();
    }

    // CA-2: EmpleadoId nulo produce 400
    [Fact]
    public async Task Validar_RechazaEmpleadoId_CuandoEsNull()
    {
        var resultado = await Validar(ComandoValido() with { EmpleadoId = null! });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e =>
            e.PropertyName == nameof(RegistrarMarcacion.EmpleadoId));
    }

    // CA-2: EmpleadoId vacio produce 400
    [Fact]
    public async Task Validar_RechazaEmpleadoId_CuandoEstaVacio()
    {
        var resultado = await Validar(ComandoValido() with { EmpleadoId = "" });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e =>
            e.PropertyName == nameof(RegistrarMarcacion.EmpleadoId));
    }

    // CA-2: EmpleadoId con solo espacios en blanco produce 400
    [Fact]
    public async Task Validar_RechazaEmpleadoId_CuandoSonSoloEspacios()
    {
        var resultado = await Validar(ComandoValido() with { EmpleadoId = "   " });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e =>
            e.PropertyName == nameof(RegistrarMarcacion.EmpleadoId));
    }

    // CA-3: EmpleadoId que contiene ':' produce 400 - cierra la colision de stream ID descrita
    // en el Contexto del issue (ComputarStreamId usa ':' como separador entre EmpleadoId y
    // Timestamp; un EmpleadoId con ':' puede fabricar el mismo stream ID que otra combinacion
    // legitima). El valor no esta vacio, asi que el unico error posible proviene de la regla del
    // separador y no de NotEmpty.
    [Fact]
    public async Task Validar_RechazaEmpleadoId_CuandoContieneDosPuntos()
    {
        var resultado = await Validar(ComandoValido() with { EmpleadoId = "EMP:001" });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e =>
            e.PropertyName == nameof(RegistrarMarcacion.EmpleadoId));
    }

    // CA-4: Timestamp con el valor default de DateTime produce 400
    [Fact]
    public async Task Validar_RechazaTimestamp_CuandoEsDefault()
    {
        var resultado = await Validar(ComandoValido() with { Timestamp = default });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e =>
            e.PropertyName == nameof(RegistrarMarcacion.Timestamp));
    }

    // Regresion de #105 CA-3: TipoMarcacion sigue siendo opcional - null no debe generar error
    [Fact]
    public async Task Validar_Aprueba_CuandoTipoMarcacionEsNull()
    {
        var resultado = await Validar(ComandoValido() with { TipoMarcacion = null });

        resultado.IsValid.Should().BeTrue();
    }

    // Regresion de #105 CA-3: DispositivoId sigue siendo opcional - null no debe generar error
    [Fact]
    public async Task Validar_Aprueba_CuandoDispositivoIdEsNull()
    {
        var resultado = await Validar(ComandoValido() with { DispositivoId = null });

        resultado.IsValid.Should().BeTrue();
    }
}
