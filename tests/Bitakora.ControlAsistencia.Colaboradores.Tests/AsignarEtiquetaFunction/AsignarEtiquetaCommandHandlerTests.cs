using AwesomeAssertions;
using Bitakora.ControlAsistencia.Colaboradores.AsignarEtiquetaFunction;
using Bitakora.ControlAsistencia.Colaboradores.AsignarEtiquetaFunction.CommandHandler;
using Bitakora.ControlAsistencia.Colaboradores.DomainEvents;
using Bitakora.ControlAsistencia.Colaboradores.Entities;
using Cosmos.EventSourcing.Abstractions.Commands;
using Cosmos.EventSourcing.Testing.Utilities;

namespace Bitakora.ControlAsistencia.Colaboradores.Tests.AsignarEtiquetaFunction;

public class AsignarEtiquetaCommandHandlerTests : CommandHandlerAsyncTest<AsignarEtiqueta>
{
    private const string NumeroValido = "79543210";

    // Oraculo independiente: literal, nunca derivado de ComputarStreamId -- si se derivara, un cambio
    // de formato de la clave se auto-validaria.
    private const string StreamIdEsperado = "CC-79543210";

    private const string CodigoVinculacionVigente = "COL-001";
    private const string CodigoVinculacionReingreso = "COL-002";
    private static readonly DateOnly FechaInicioVinculacionVigente = new(2026, 1, 15);
    private static readonly DateOnly FechaEfectivaTerminacion = new(2026, 6, 1);
    private static readonly DateOnly FechaInicioReingreso = new(2026, 7, 1);

    protected override ICommandHandlerAsync<AsignarEtiqueta> Handler =>
        new AsignarEtiquetaCommandHandler(EventStore);

    private static AsignarEtiqueta ComandoValido() => new(
        TipoIdentificacion: "CC",
        NumeroIdentificacion: NumeroValido,
        Categoria: "Área",
        Valor: "Tecnología");

    private static Identificacion IdentificacionValida() =>
        Identificacion.Crear(TipoIdentificacion.CC, NumeroValido);

    private static NombreColaborador NombreValido() =>
        NombreColaborador.Crear("Luis", "Augusto", "Barreto", null);

    private static ColaboradorRegistrado ColaboradorRegistradoValido() =>
        new(IdentificacionValida(), NombreValido());

    private static VinculacionIniciada VinculacionIniciadaVigente() =>
        new(CodigoVinculacionVigente, FechaInicioVinculacionVigente);

    private void DadoUnColaboradorConVinculacionAbierta() =>
        Given(StreamIdEsperado, ColaboradorRegistradoValido(), VinculacionIniciadaVigente());

    private void DadoUnColaboradorConTerminacionRegistrada(DateOnly fechaEfectiva) =>
        Given(StreamIdEsperado,
            ColaboradorRegistradoValido(),
            VinculacionIniciadaVigente(),
            new VinculacionTerminada(fechaEfectiva));

    private void DadoUnColaboradorConEtiquetaAsignada(Etiqueta etiqueta) =>
        Given(StreamIdEsperado,
            ColaboradorRegistradoValido(),
            VinculacionIniciadaVigente(),
            new EtiquetaAsignada(etiqueta));

    [Fact]
    public async Task AsignarEtiqueta_EmiteEtiquetaAsignada_CuandoLaCategoriaEsNueva()
    {
        DadoUnColaboradorConVinculacionAbierta();
        var etiquetaEsperada = Etiqueta.Crear("Área", "Tecnología");

        await WhenAsync(ComandoValido());

        Then(StreamIdEsperado, new EtiquetaAsignada(etiquetaEsperada));
        And<ColaboradorAggregateRoot, int>(StreamIdEsperado, c => c.Etiquetas.Count, 1);
        And<ColaboradorAggregateRoot, Etiqueta>(
            StreamIdEsperado, c => c.Etiquetas["area"], etiquetaEsperada);
    }

    [Fact]
    public async Task AsignarEtiqueta_EmiteEtiquetaAsignada_CuandoTipoYNumeroLleganSinNormalizar()
    {
        DadoUnColaboradorConVinculacionAbierta();
        var comandoSinNormalizar = ComandoValido() with
        {
            TipoIdentificacion = "cc",
            NumeroIdentificacion = "  79543210  "
        };

        await WhenAsync(comandoSinNormalizar);

        Then(StreamIdEsperado, new EtiquetaAsignada(Etiqueta.Crear("Área", "Tecnología")));
        And<ColaboradorAggregateRoot, int>(StreamIdEsperado, c => c.Etiquetas.Count, 1);
    }

    [Fact]
    public async Task AsignarEtiqueta_EmiteEtiquetaAsignada_CuandoLaCategoriaExisteConValorDistinto()
    {
        DadoUnColaboradorConEtiquetaAsignada(Etiqueta.Crear("area", "Ventas"));
        var etiquetaSobrescrita = Etiqueta.Crear("Área", "Tecnología");

        await WhenAsync(ComandoValido());

        Then(StreamIdEsperado, new EtiquetaAsignada(etiquetaSobrescrita));
        And<ColaboradorAggregateRoot, int>(StreamIdEsperado, c => c.Etiquetas.Count, 1);
        And<ColaboradorAggregateRoot, Etiqueta>(
            StreamIdEsperado, c => c.Etiquetas["area"], etiquetaSobrescrita);
    }

    [Fact]
    public async Task AsignarEtiqueta_NoEmiteEvento_CuandoLaEtiquetaEsIgualPorValorALaExistente()
    {
        var etiquetaExistente = Etiqueta.Crear("Área", "Tecnología");
        DadoUnColaboradorConEtiquetaAsignada(etiquetaExistente);

        // Misma etiqueta por valor, con otra combinacion de mayusculas/tildes en ambos campos.
        await WhenAsync(ComandoValido() with { Categoria = "area", Valor = "tecnologia" });

        Then(StreamIdEsperado);
        And<ColaboradorAggregateRoot, int>(StreamIdEsperado, c => c.Etiquetas.Count, 1);
        And<ColaboradorAggregateRoot, Etiqueta>(
            StreamIdEsperado, c => c.Etiquetas["area"], etiquetaExistente);
    }

    [Fact]
    public async Task AsignarEtiqueta_LanzaReglaDeNegocioDeclinadaException_CuandoLaUltimaVinculacionTieneTerminacionRegistrada()
    {
        DadoUnColaboradorConTerminacionRegistrada(FechaEfectivaTerminacion);

        var act = async () => await WhenAsync(ComandoValido());

        await act.Should().ThrowExactlyAsync<Bitakora.ControlAsistencia.Colaboradores.Infraestructura.ReglaDeNegocioDeclinadaException>()
            .WithMessage($"*{AsignarEtiquetaCommandHandler.Mensajes.VinculacionTerminada}*");
        Then(StreamIdEsperado);
        And<ColaboradorAggregateRoot, int>(StreamIdEsperado, c => c.Etiquetas.Count, 0);
    }

    // Las etiquetas describen la relacion laboral activa: un preaviso sin vencer bloquea igual.
    [Fact]
    public async Task AsignarEtiqueta_LanzaReglaDeNegocioDeclinadaException_CuandoLaTerminacionEsUnPreavisoConFechaFutura()
    {
        var fechaPreavisoFutura = new DateOnly(2030, 1, 1);
        DadoUnColaboradorConTerminacionRegistrada(fechaPreavisoFutura);

        var act = async () => await WhenAsync(ComandoValido());

        await act.Should().ThrowExactlyAsync<Bitakora.ControlAsistencia.Colaboradores.Infraestructura.ReglaDeNegocioDeclinadaException>()
            .WithMessage($"*{AsignarEtiquetaCommandHandler.Mensajes.VinculacionTerminada}*");
        Then(StreamIdEsperado);
        And<ColaboradorAggregateRoot, int>(StreamIdEsperado, c => c.Etiquetas.Count, 0);
    }

    // Sin este test, un Apply que reemplazara el diccionario en vez de indexarlo pasaria en verde.
    [Fact]
    public async Task AsignarEtiqueta_ConservaLaCategoriaPrevia_CuandoLaCategoriaNuevaEsDistinta()
    {
        var etiquetaPrevia = Etiqueta.Crear("Sede", "Medellín");
        DadoUnColaboradorConEtiquetaAsignada(etiquetaPrevia);
        var etiquetaNueva = Etiqueta.Crear("Área", "Tecnología");

        await WhenAsync(ComandoValido());

        Then(StreamIdEsperado, new EtiquetaAsignada(etiquetaNueva));
        And<ColaboradorAggregateRoot, int>(StreamIdEsperado, c => c.Etiquetas.Count, 2);
        And<ColaboradorAggregateRoot, Etiqueta>(
            StreamIdEsperado, c => c.Etiquetas["sede"], etiquetaPrevia);
        And<ColaboradorAggregateRoot, Etiqueta>(
            StreamIdEsperado, c => c.Etiquetas["area"], etiquetaNueva);
    }

    // La regla de apertura gana sobre la idempotencia: sobre una relacion laboral congelada se rechaza
    // aunque el estado deseado coincida. Orden inverso al de CorregirFechaInicio, que si es valida sobre
    // una vinculacion cerrada.
    [Fact]
    public async Task AsignarEtiqueta_LanzaReglaDeNegocioDeclinadaException_CuandoLaEtiquetaEsIgualPeroLaVinculacionTieneTerminacionRegistrada()
    {
        var etiquetaExistente = Etiqueta.Crear("Área", "Tecnología");
        Given(StreamIdEsperado,
            ColaboradorRegistradoValido(),
            VinculacionIniciadaVigente(),
            new EtiquetaAsignada(etiquetaExistente),
            new VinculacionTerminada(FechaEfectivaTerminacion));

        var act = async () => await WhenAsync(ComandoValido());

        await act.Should().ThrowExactlyAsync<Bitakora.ControlAsistencia.Colaboradores.Infraestructura.ReglaDeNegocioDeclinadaException>()
            .WithMessage($"*{AsignarEtiquetaCommandHandler.Mensajes.VinculacionTerminada}*");
        Then(StreamIdEsperado);
        And<ColaboradorAggregateRoot, Etiqueta>(
            StreamIdEsperado, c => c.Etiquetas["area"], etiquetaExistente);
    }

    [Fact]
    public async Task AsignarEtiqueta_EmiteEtiquetaAsignada_CuandoLaVinculacionEsUnReingresoTrasUnaTerminacionConEtiquetasPrevias()
    {
        Given(StreamIdEsperado,
            ColaboradorRegistradoValido(),
            VinculacionIniciadaVigente(),
            new EtiquetaAsignada(Etiqueta.Crear("Área", "Ventas")),
            new VinculacionTerminada(FechaEfectivaTerminacion),
            new VinculacionIniciada(CodigoVinculacionReingreso, FechaInicioReingreso));
        var etiquetaEsperada = Etiqueta.Crear("Área", "Tecnología");

        await WhenAsync(ComandoValido());

        Then(StreamIdEsperado, new EtiquetaAsignada(etiquetaEsperada));
        And<ColaboradorAggregateRoot, int>(StreamIdEsperado, c => c.Etiquetas.Count, 1);
        And<ColaboradorAggregateRoot, Etiqueta>(
            StreamIdEsperado, c => c.Etiquetas["area"], etiquetaEsperada);
    }

    // Sin And<>: el aggregate no existe en el TestStore.
    [Fact]
    public async Task AsignarEtiqueta_LanzaRecursoNoEncontradoException_CuandoColaboradorNoExiste()
    {
        var act = async () => await WhenAsync(ComandoValido());

        await act.Should().ThrowExactlyAsync<Bitakora.ControlAsistencia.Colaboradores.Infraestructura.RecursoNoEncontradoException>()
            .WithMessage($"*{AsignarEtiquetaCommandHandler.Mensajes.ColaboradorNoEncontrado}*");
        Then(StreamIdEsperado);
    }
}
