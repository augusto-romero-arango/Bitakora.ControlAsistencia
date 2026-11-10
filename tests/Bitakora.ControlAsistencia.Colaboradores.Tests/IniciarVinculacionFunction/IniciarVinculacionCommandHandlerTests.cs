using AwesomeAssertions;
using Bitakora.ControlAsistencia.Colaboradores.DomainEvents;
using Bitakora.ControlAsistencia.Colaboradores.Entities;
using Bitakora.ControlAsistencia.Colaboradores.IniciarVinculacionFunction;
using Bitakora.ControlAsistencia.Colaboradores.IniciarVinculacionFunction.CommandHandler;
using Cosmos.EventSourcing.Abstractions.Commands;
using Cosmos.EventSourcing.Testing.Utilities;

namespace Bitakora.ControlAsistencia.Colaboradores.Tests.IniciarVinculacionFunction;

public class IniciarVinculacionCommandHandlerTests : CommandHandlerAsyncTest<IniciarVinculacion>
{
    private const string NumeroValido = "79543210";

    // Oraculo independiente: literal, nunca derivado de ComputarStreamId -- si se derivara, un cambio
    // de formato de la clave se auto-validaria.
    private const string StreamIdEsperado = "CC-79543210";

    private const string CodigoVinculacionOriginal = "COL-001";
    private const string CodigoReingreso = "COL-002";
    private static readonly DateOnly FechaInicioOriginal = new(2026, 1, 15);
    private static readonly DateOnly FechaEfectivaTerminacionOriginal = new(2026, 6, 1);
    private static readonly DateOnly FechaInicioReingresoValida =
        FechaEfectivaTerminacionOriginal.AddDays(1);

    private const string CodigoSedeAnterior = "MED";
    private const string CodigoSedeNueva = "BOG";

    protected override ICommandHandlerAsync<IniciarVinculacion> Handler =>
        new IniciarVinculacionCommandHandler(EventStore);

    private static IniciarVinculacion ComandoValido() => new(
        TipoIdentificacion: "CC",
        NumeroIdentificacion: NumeroValido,
        CodigoColaborador: CodigoReingreso,
        FechaInicio: FechaInicioReingresoValida);

    private static Identificacion IdentificacionValida() =>
        Identificacion.Crear(TipoIdentificacion.CC, NumeroValido);

    private static NombreColaborador NombreValido() =>
        NombreColaborador.Crear("Luis", "Augusto", "Barreto", null);

    private static ColaboradorRegistrado ColaboradorRegistradoValido() =>
        new(IdentificacionValida(), NombreValido());

    private static VinculacionIniciada VinculacionIniciadaOriginal() =>
        new(CodigoVinculacionOriginal, FechaInicioOriginal);

    private void DadoUnColaboradorConVinculacionAbierta() =>
        Given(StreamIdEsperado, ColaboradorRegistradoValido(), VinculacionIniciadaOriginal());

    private void DadoUnColaboradorConVinculacionTerminada(DateOnly fechaEfectiva) =>
        Given(StreamIdEsperado,
            ColaboradorRegistradoValido(),
            VinculacionIniciadaOriginal(),
            new VinculacionTerminada(fechaEfectiva));

    // Sede anterior distinta de la del reingreso, para que el And distinga sede nueva de sede heredada.
    private void DadoUnColaboradorConVinculacionTerminadaYSedeAsignada(
        DateOnly fechaEfectiva, string codigoSedeAnterior) =>
        Given(StreamIdEsperado,
            ColaboradorRegistradoValido(),
            VinculacionIniciadaOriginal(),
            new SedeAsignada(codigoSedeAnterior),
            new VinculacionTerminada(fechaEfectiva));

    [Fact]
    public async Task IniciarVinculacion_EmiteVinculacionIniciada_CuandoFechaInicioEsPosteriorALaTerminacion()
    {
        DadoUnColaboradorConVinculacionTerminada(FechaEfectivaTerminacionOriginal);

        await WhenAsync(ComandoValido());

        Then(StreamIdEsperado, new VinculacionIniciada(CodigoReingreso, FechaInicioReingresoValida));
        And<ColaboradorAggregateRoot, string>(
            StreamIdEsperado, c => c.CodigoVinculacionVigente, CodigoReingreso);
        And<ColaboradorAggregateRoot, DateOnly>(
            StreamIdEsperado, c => c.FechaInicioVinculacionVigente, FechaInicioReingresoValida);
        And<ColaboradorAggregateRoot, DateOnly?>(
            StreamIdEsperado, c => c.FechaTerminacionVinculacionVigente, null);
    }

    // Sin normalizar tipo y numero, el handler computaria otra clave y responderia 404 sobre un
    // colaborador que si existe.
    [Fact]
    public async Task IniciarVinculacion_EmiteVinculacionIniciada_CuandoTipoYNumeroLleganSinNormalizar()
    {
        DadoUnColaboradorConVinculacionTerminada(FechaEfectivaTerminacionOriginal);
        var comandoSinNormalizar = ComandoValido() with
        {
            TipoIdentificacion = "cc",
            NumeroIdentificacion = "  79543210  "
        };

        await WhenAsync(comandoSinNormalizar);

        Then(StreamIdEsperado, new VinculacionIniciada(CodigoReingreso, FechaInicioReingresoValida));
        And<ColaboradorAggregateRoot, string>(
            StreamIdEsperado, c => c.CodigoVinculacionVigente, CodigoReingreso);
    }

    [Fact]
    public async Task IniciarVinculacion_LanzaReglaDeNegocioDeclinadaException_CuandoLaVinculacionVigenteEstaAbierta()
    {
        DadoUnColaboradorConVinculacionAbierta();

        var act = async () => await WhenAsync(ComandoValido());

        await act.Should().ThrowExactlyAsync<Bitakora.ControlAsistencia.Colaboradores.Infraestructura.ReglaDeNegocioDeclinadaException>()
            .WithMessage($"*{IniciarVinculacionCommandHandler.Mensajes.VinculacionAbierta}*");
        Then(StreamIdEsperado);
        And<ColaboradorAggregateRoot, string>(
            StreamIdEsperado, c => c.CodigoVinculacionVigente, CodigoVinculacionOriginal);
    }

    [Fact]
    public async Task IniciarVinculacion_LanzaReglaDeNegocioDeclinadaException_CuandoElReingresoPrevioSigueAbierto()
    {
        Given(StreamIdEsperado,
            ColaboradorRegistradoValido(),
            VinculacionIniciadaOriginal(),
            new VinculacionTerminada(FechaEfectivaTerminacionOriginal),
            new VinculacionIniciada(CodigoReingreso, FechaInicioReingresoValida));

        var segundoReingreso = ComandoValido() with { CodigoColaborador = "COL-003" };
        var act = async () => await WhenAsync(segundoReingreso);

        await act.Should().ThrowExactlyAsync<Bitakora.ControlAsistencia.Colaboradores.Infraestructura.ReglaDeNegocioDeclinadaException>()
            .WithMessage($"*{IniciarVinculacionCommandHandler.Mensajes.VinculacionAbierta}*");
        Then(StreamIdEsperado);
        And<ColaboradorAggregateRoot, string>(
            StreamIdEsperado, c => c.CodigoVinculacionVigente, CodigoReingreso);
    }

    // El dia de la fecha efectiva pertenece a la vinculacion que termina: el mismo dia se rechaza.
    [Fact]
    public async Task IniciarVinculacion_LanzaReglaDeNegocioDeclinadaException_CuandoFechaInicioEsIgualALaFechaEfectivaDeTerminacion()
    {
        DadoUnColaboradorConVinculacionTerminada(FechaEfectivaTerminacionOriginal);

        var act = async () => await WhenAsync(
            ComandoValido() with { FechaInicio = FechaEfectivaTerminacionOriginal });

        await act.Should().ThrowExactlyAsync<Bitakora.ControlAsistencia.Colaboradores.Infraestructura.ReglaDeNegocioDeclinadaException>()
            .WithMessage($"*{IniciarVinculacionCommandHandler.Mensajes.FechaSolapaVinculacionAnterior}*");
        Then(StreamIdEsperado);
        And<ColaboradorAggregateRoot, string>(
            StreamIdEsperado, c => c.CodigoVinculacionVigente, CodigoVinculacionOriginal);
    }

    [Fact]
    public async Task IniciarVinculacion_LanzaReglaDeNegocioDeclinadaException_CuandoFechaInicioEsAnteriorALaFechaEfectivaDeTerminacion()
    {
        DadoUnColaboradorConVinculacionTerminada(FechaEfectivaTerminacionOriginal);
        var fechaAnterior = FechaEfectivaTerminacionOriginal.AddDays(-1);

        var act = async () => await WhenAsync(ComandoValido() with { FechaInicio = fechaAnterior });

        await act.Should().ThrowExactlyAsync<Bitakora.ControlAsistencia.Colaboradores.Infraestructura.ReglaDeNegocioDeclinadaException>()
            .WithMessage($"*{IniciarVinculacionCommandHandler.Mensajes.FechaSolapaVinculacionAnterior}*");
        Then(StreamIdEsperado);
        And<ColaboradorAggregateRoot, string>(
            StreamIdEsperado, c => c.CodigoVinculacionVigente, CodigoVinculacionOriginal);
    }

    // La no-solape se compone sin reloj: un preaviso con fecha futura bloquea igual.
    [Fact]
    public async Task IniciarVinculacion_LanzaReglaDeNegocioDeclinadaException_CuandoFechaInicioNoSuperaElPreavisoRegistrado()
    {
        var fechaPreavisoFutura = new DateOnly(2030, 1, 1);
        DadoUnColaboradorConVinculacionTerminada(fechaPreavisoFutura);

        var act = async () => await WhenAsync(ComandoValido() with { FechaInicio = fechaPreavisoFutura });

        await act.Should().ThrowExactlyAsync<Bitakora.ControlAsistencia.Colaboradores.Infraestructura.ReglaDeNegocioDeclinadaException>()
            .WithMessage($"*{IniciarVinculacionCommandHandler.Mensajes.FechaSolapaVinculacionAnterior}*");
        Then(StreamIdEsperado);
        And<ColaboradorAggregateRoot, string>(
            StreamIdEsperado, c => c.CodigoVinculacionVigente, CodigoVinculacionOriginal);
    }

    [Fact]
    public async Task IniciarVinculacion_EmiteVinculacionIniciada_CuandoFechaInicioEsPosteriorAlPreavisoRegistrado()
    {
        var fechaPreavisoFutura = new DateOnly(2030, 1, 1);
        var fechaInicioReingreso = fechaPreavisoFutura.AddDays(1);
        DadoUnColaboradorConVinculacionTerminada(fechaPreavisoFutura);

        await WhenAsync(ComandoValido() with { FechaInicio = fechaInicioReingreso });

        Then(StreamIdEsperado, new VinculacionIniciada(CodigoReingreso, fechaInicioReingreso));
        And<ColaboradorAggregateRoot, string>(
            StreamIdEsperado, c => c.CodigoVinculacionVigente, CodigoReingreso);
        And<ColaboradorAggregateRoot, DateOnly?>(
            StreamIdEsperado, c => c.FechaTerminacionVinculacionVigente, null);
    }

    [Fact]
    public async Task IniciarVinculacion_EmiteVinculacionIniciadaConSede_CuandoCodigoSedeLlegaEnElComando()
    {
        DadoUnColaboradorConVinculacionTerminadaYSedeAsignada(
            FechaEfectivaTerminacionOriginal, CodigoSedeAnterior);

        await WhenAsync(ComandoValido() with { CodigoSede = CodigoSedeNueva });

        Then(StreamIdEsperado,
            new VinculacionIniciada(CodigoReingreso, FechaInicioReingresoValida, CodigoSedeNueva));
        And<ColaboradorAggregateRoot, string?>(StreamIdEsperado, c => c.CodigoSede, CodigoSedeNueva);
    }

    [Fact]
    public async Task IniciarVinculacion_EmiteVinculacionIniciadaSinSede_CuandoCodigoSedeNoLlegaAunqueLaAnteriorTeniaSede()
    {
        DadoUnColaboradorConVinculacionTerminadaYSedeAsignada(
            FechaEfectivaTerminacionOriginal, CodigoSedeAnterior);

        await WhenAsync(ComandoValido());

        Then(StreamIdEsperado, new VinculacionIniciada(CodigoReingreso, FechaInicioReingresoValida, null));
        And<ColaboradorAggregateRoot, string?>(StreamIdEsperado, c => c.CodigoSede, null);
    }

    [Fact]
    public async Task IniciarVinculacion_LanzaReglaDeNegocioDeclinadaException_CuandoLaVinculacionVigenteEstaAbiertaYElComandoTraeSede()
    {
        Given(StreamIdEsperado,
            ColaboradorRegistradoValido(),
            VinculacionIniciadaOriginal(),
            new SedeAsignada(CodigoSedeAnterior));

        var act = async () => await WhenAsync(ComandoValido() with { CodigoSede = CodigoSedeNueva });

        await act.Should().ThrowExactlyAsync<Bitakora.ControlAsistencia.Colaboradores.Infraestructura.ReglaDeNegocioDeclinadaException>()
            .WithMessage($"*{IniciarVinculacionCommandHandler.Mensajes.VinculacionAbierta}*");
        Then(StreamIdEsperado);
        And<ColaboradorAggregateRoot, string?>(StreamIdEsperado, c => c.CodigoSede, CodigoSedeAnterior);
    }

    [Fact]
    public async Task IniciarVinculacion_LanzaReglaDeNegocioDeclinadaException_CuandoFechaSolapaYElComandoTraeSede()
    {
        DadoUnColaboradorConVinculacionTerminadaYSedeAsignada(
            FechaEfectivaTerminacionOriginal, CodigoSedeAnterior);

        var act = async () => await WhenAsync(ComandoValido() with
        {
            FechaInicio = FechaEfectivaTerminacionOriginal,
            CodigoSede = CodigoSedeNueva
        });

        await act.Should().ThrowExactlyAsync<Bitakora.ControlAsistencia.Colaboradores.Infraestructura.ReglaDeNegocioDeclinadaException>()
            .WithMessage($"*{IniciarVinculacionCommandHandler.Mensajes.FechaSolapaVinculacionAnterior}*");
        Then(StreamIdEsperado);
        And<ColaboradorAggregateRoot, string?>(StreamIdEsperado, c => c.CodigoSede, CodigoSedeAnterior);
    }

    // Sin And<>: el aggregate no existe en el TestStore.
    [Fact]
    public async Task IniciarVinculacion_LanzaRecursoNoEncontradoException_CuandoColaboradorNoExiste()
    {
        var act = async () => await WhenAsync(ComandoValido());

        await act.Should().ThrowExactlyAsync<Bitakora.ControlAsistencia.Colaboradores.Infraestructura.RecursoNoEncontradoException>()
            .WithMessage($"*{IniciarVinculacionCommandHandler.Mensajes.ColaboradorNoEncontrado}*");
        Then(StreamIdEsperado);
    }
}
