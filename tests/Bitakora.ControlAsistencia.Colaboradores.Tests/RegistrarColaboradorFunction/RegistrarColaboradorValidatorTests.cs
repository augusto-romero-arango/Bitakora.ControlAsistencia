// Issue #330: validacion de forma del comando RegistrarColaborador en el borde (CA-3).
// Patron de referencia: RegistrarMarcacionValidatorTests (ValidateAsync + verificacion de
// PropertyName en resultado.Errors).
//
// Issue #387: CodigoColaborador debe ser URL-safe -- set permitido: unreserved characters de
// RFC 3986 seccion 2.3 (A-Z a-z 0-9 - . _ ~), el mismo set que las Microsoft Azure REST API
// Guidelines fijan para path segments (MEF-ADR-0043 seccion 1). El ":" queda explicitamente fuera
// (reservado como separador de accion). Se RECHAZA (400), nunca se limpia ni normaliza en
// silencio -- a diferencia de Identificacion (#381), el codigo lo asigna la empresa y alterarlo
// cambiaria un dato ajeno.

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

    // CA-1 (#387): codigo con caracteres unreserved no alfanumericos (. _ ~ -) sigue siendo valido --
    // el set permitido no se limita a alfanumericos.
    [Fact]
    public async Task Validar_Aprueba_CuandoCodigoColaboradorTieneCaracteresUnreservedNoAlfanumericos()
    {
        var resultado = await Validar(ComandoValido() with { CodigoColaborador = "a.b_c~2" });

        resultado.IsValid.Should().BeTrue();
    }

    // CA-1 (#387): ejemplo explicito del issue -- guion es unreserved.
    [Fact]
    public async Task Validar_Aprueba_CuandoCodigoColaboradorEsAlfanumericoConGuion()
    {
        var resultado = await Validar(ComandoValido() with { CodigoColaborador = "EMP-001" });

        resultado.IsValid.Should().BeTrue();
    }

    // CA-2 (#387): ":" esta explicitamente fuera del set permitido -- MEF-ADR-0043 seccion 1 lo
    // reserva como separador de accion (vinculaciones/{codigo}:terminar, #379). Un codigo con ":"
    // haria inparseable esa ruta.
    [Fact]
    public async Task Validar_RechazaCodigoColaborador_CuandoContieneDosPuntos()
    {
        var resultado = await Validar(ComandoValido() with { CodigoColaborador = "COL:001" });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e =>
            e.PropertyName == nameof(RegistrarColaborador.CodigoColaborador));
    }

    // CA-3 (#387): espacio no es unreserved -> 400.
    [Fact]
    public async Task Validar_RechazaCodigoColaborador_CuandoContieneEspacio()
    {
        var resultado = await Validar(ComandoValido() with { CodigoColaborador = "COL 001" });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e =>
            e.PropertyName == nameof(RegistrarColaborador.CodigoColaborador));
    }

    // CA-3 (#387): caracter acentuado no es unreserved -> 400.
    [Fact]
    public async Task Validar_RechazaCodigoColaborador_CuandoContieneCaracterAcentuado()
    {
        var resultado = await Validar(ComandoValido() with { CodigoColaborador = "CÓDIGO1" });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e =>
            e.PropertyName == nameof(RegistrarColaborador.CodigoColaborador));
    }

    // CA-3 (#387): "/" no es unreserved -> 400.
    [Fact]
    public async Task Validar_RechazaCodigoColaborador_CuandoContieneBarra()
    {
        var resultado = await Validar(ComandoValido() with { CodigoColaborador = "COL/001" });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e =>
            e.PropertyName == nameof(RegistrarColaborador.CodigoColaborador));
    }

    // CA-3 (#387), caso borde del ancla de fin de linea: un salto de linea final tampoco es
    // unreserved -> 400. En .NET el ancla "$" hace match tambien ANTES de un "\n" final
    // (a diferencia de "\z"), asi que un patron anclado con "$" aceptaria "COL-001\n" en silencio
    // -- un valor que rompe la URL igual que un espacio, y ademas habilita CRLF injection en
    // cualquier consumidor que lo reenvie en un header o lo escriba en un log.
    [Fact]
    public async Task Validar_RechazaCodigoColaborador_CuandoTerminaEnSaltoDeLinea()
    {
        var resultado = await Validar(ComandoValido() with { CodigoColaborador = "COL-001\n" });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e =>
            e.PropertyName == nameof(RegistrarColaborador.CodigoColaborador));
    }
}
