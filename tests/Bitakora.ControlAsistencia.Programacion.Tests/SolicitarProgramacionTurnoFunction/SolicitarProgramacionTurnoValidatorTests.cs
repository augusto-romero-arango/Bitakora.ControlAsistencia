// HU-10: Solicitar programacion de turno del catalogo - tests del validator

using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.Programacion.SolicitarProgramacionTurnoFunction;
using Bitakora.ControlAsistencia.Programacion.SolicitarProgramacionTurnoFunction.CommandHandler;

namespace Bitakora.ControlAsistencia.Programacion.Tests.SolicitarProgramacionTurnoFunction;

public class SolicitarProgramacionTurnoValidatorTests
{
    private readonly SolicitarProgramacionTurnoValidator _validator = new();

    private static ColaboradorSolicitado DatosColaboradorValidos() =>
        new("CC-12345678", "E001", "Juan Perez");

    private static SolicitarProgramacionTurno ComandoValido() => new(
        Guid.NewGuid(),
        Guid.NewGuid(),
        DatosColaboradorValidos(),
        [new DateOnly(2026, 4, 7)]);

    // Camino feliz - todos los campos correctos
    [Fact]
    public async Task DebeSerValido_CuandoTodosLosCamposSonCorrectos()
    {
        var resultado = await _validator.ValidateAsync(
            ComandoValido(), TestContext.Current.CancellationToken);

        resultado.IsValid.Should().BeTrue();
    }

    // CA-1: Id no puede ser Guid vacio
    [Fact]
    public async Task DebeTenerError_CuandoIdEsGuidVacio()
    {
        var comando = ComandoValido() with { Id = Guid.Empty };

        var resultado = await _validator.ValidateAsync(
            comando, TestContext.Current.CancellationToken);

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e =>
            e.PropertyName == nameof(SolicitarProgramacionTurno.Id));
    }

    // CA-2: TurnoId no puede ser Guid vacio
    [Fact]
    public async Task DebeTenerError_CuandoTurnoIdEsGuidVacio()
    {
        var comando = ComandoValido() with { TurnoId = Guid.Empty };

        var resultado = await _validator.ValidateAsync(
            comando, TestContext.Current.CancellationToken);

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e =>
            e.PropertyName == nameof(SolicitarProgramacionTurno.TurnoId));
    }

    // CA-3: CodigoColaborador no puede estar vacio
    [Fact]
    public async Task DebeTenerError_CuandoCodigoColaboradorEstaVacio()
    {
        var datosInvalidos = DatosColaboradorValidos() with { CodigoColaborador = "" };
        var comando = ComandoValido() with { Colaborador = datosInvalidos };

        var resultado = await _validator.ValidateAsync(
            comando, TestContext.Current.CancellationToken);

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e =>
            e.PropertyName.Contains(nameof(ColaboradorSolicitado.CodigoColaborador)));
    }

    // CA-3: Identificacion no puede estar vacia. Issue #436: llega ya compuesta como
    // "{Tipo}-{Numero}" desde el cliente, asi que el validator ve un solo campo donde antes veia
    // TipoIdentificacion y NumeroIdentificacion por separado.
    [Fact]
    public async Task DebeTenerError_CuandoIdentificacionEstaVacia()
    {
        var datosInvalidos = DatosColaboradorValidos() with { Identificacion = "" };
        var comando = ComandoValido() with { Colaborador = datosInvalidos };

        var resultado = await _validator.ValidateAsync(
            comando, TestContext.Current.CancellationToken);

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e =>
            e.PropertyName.Contains(nameof(ColaboradorSolicitado.Identificacion)));
    }

    // CA-3: NombreCompleto no puede estar vacio. Issue #436: llega ya concatenado, asi que
    // reemplaza a los ejes Nombres y Apellidos del quinteto.
    [Fact]
    public async Task DebeTenerError_CuandoNombreCompletoEstaVacio()
    {
        var datosInvalidos = DatosColaboradorValidos() with { NombreCompleto = "   " };
        var comando = ComandoValido() with { Colaborador = datosInvalidos };

        var resultado = await _validator.ValidateAsync(
            comando, TestContext.Current.CancellationToken);

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e =>
            e.PropertyName.Contains(nameof(ColaboradorSolicitado.NombreCompleto)));
    }

    // HU-225 / CA-1: Colaborador null no debe lanzar excepcion (NullReferenceException por
    // desreferencia encadenada); debe reportar IsValid == false con un error asociado a
    // la propiedad Colaborador.
    [Fact]
    public async Task Validar_TieneErrorEnColaborador_CuandoColaboradorEsNull()
    {
        var comando = ComandoValido() with { Colaborador = null! };

        var resultado = await _validator.ValidateAsync(
            comando, TestContext.Current.CancellationToken);

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e =>
            e.PropertyName == nameof(SolicitarProgramacionTurno.Colaborador));
    }

    // CA-4: Fechas debe tener al menos un elemento
    [Fact]
    public async Task DebeTenerError_CuandoFechasEstaVacia()
    {
        var comando = ComandoValido() with { Fechas = [] };

        var resultado = await _validator.ValidateAsync(
            comando, TestContext.Current.CancellationToken);

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e =>
            e.PropertyName == nameof(SolicitarProgramacionTurno.Fechas));
    }

    // Issue #331 CA-1/CA-2: Sede es opcional -- ausente (null) sigue siendo un comando valido, el
    // comportamiento actual (anterior a este issue) queda intacto.
    [Fact]
    public async Task Validar_EsValido_CuandoSedeEsNull()
    {
        var comando = ComandoValido() with { Sede = null };

        var resultado = await _validator.ValidateAsync(
            comando, TestContext.Current.CancellationToken);

        resultado.IsValid.Should().BeTrue();
    }

    // Issue #331 CA-1: sede presente y con Id/Nombre validos sigue siendo un comando valido.
    [Fact]
    public async Task Validar_EsValido_CuandoSedeTieneIdYNombre()
    {
        var comando = ComandoValido() with { Sede = new SedeProgramada("SEDE-01", "Sede Principal") };

        var resultado = await _validator.ValidateAsync(
            comando, TestContext.Current.CancellationToken);

        resultado.IsValid.Should().BeTrue();
    }

    // Issue #331 CA-3: sede presente pero con Id vacio se rechaza en el validator (400), antes de
    // tocar el aggregate.
    [Fact]
    public async Task Validar_TieneErrorEnSedeId_CuandoSedeTieneIdVacio()
    {
        var comando = ComandoValido() with { Sede = new SedeProgramada("", "Sede Principal") };

        var resultado = await _validator.ValidateAsync(
            comando, TestContext.Current.CancellationToken);

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e =>
            e.PropertyName.Contains(nameof(SedeProgramada.Id)));
    }

    // Issue #331 CA-3: sede presente pero con Nombre en blanco se rechaza en el validator (400).
    [Fact]
    public async Task Validar_TieneErrorEnSedeNombre_CuandoSedeTieneNombreEnBlanco()
    {
        var comando = ComandoValido() with { Sede = new SedeProgramada("SEDE-01", "   ") };

        var resultado = await _validator.ValidateAsync(
            comando, TestContext.Current.CancellationToken);

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e =>
            e.PropertyName.Contains(nameof(SedeProgramada.Nombre)));
    }
}
