// Retirar una etiqueta sobre una categoria ausente de la vinculacion vigente es un no-op exitoso
// (MEF-ADR-0004 "Estado ya alcanzado"): sin evento y sin excepcion. VinculacionTerminada es la
// unica razon de rechazo (CA-ADR-0030) -> ReglaDeNegocioDeclinadaException.

using AwesomeAssertions;
using Bitakora.ControlAsistencia.Colaboradores.DomainEvents;
using Bitakora.ControlAsistencia.Colaboradores.Entities;
using Bitakora.ControlAsistencia.Colaboradores.RetirarEtiquetaFunction;
using Bitakora.ControlAsistencia.Colaboradores.RetirarEtiquetaFunction.CommandHandler;
using Cosmos.EventSourcing.Abstractions.Commands;
using Cosmos.EventSourcing.Testing.Utilities;

namespace Bitakora.ControlAsistencia.Colaboradores.Tests.RetirarEtiquetaFunction;

public class RetirarEtiquetaCommandHandlerTests : CommandHandlerAsyncTest<RetirarEtiqueta>
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

    protected override ICommandHandlerAsync<RetirarEtiqueta> Handler =>
        new RetirarEtiquetaCommandHandler(EventStore);

    private static RetirarEtiqueta ComandoValido() => new(
        TipoIdentificacion: "CC",
        NumeroIdentificacion: NumeroValido,
        Categoria: "área");

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

    private void DadoUnColaboradorConEtiquetaAsignada(Etiqueta etiqueta) =>
        Given(StreamIdEsperado,
            ColaboradorRegistradoValido(),
            VinculacionIniciadaVigente(),
            new EtiquetaAsignada(etiqueta));

    private void DadoUnColaboradorConEtiquetaYTerminacionRegistrada(
        Etiqueta etiqueta, DateOnly fechaEfectiva) =>
        Given(StreamIdEsperado,
            ColaboradorRegistradoValido(),
            VinculacionIniciadaVigente(),
            new EtiquetaAsignada(etiqueta),
            new VinculacionTerminada(fechaEfectiva));

    // Retirar por otra forma ("área" sobre lo asignado como "Area") alcanza la misma categoria
    // normalizada.
    [Fact]
    public async Task RetirarEtiqueta_EmiteEtiquetaRetirada_CuandoLaCategoriaExisteConFormaDistinta()
    {
        DadoUnColaboradorConEtiquetaAsignada(Etiqueta.Crear("Area", "Ventas"));

        await WhenAsync(ComandoValido());

        Then(StreamIdEsperado, new EtiquetaRetirada("area"));
        And<ColaboradorAggregateRoot, int>(StreamIdEsperado, c => c.Etiquetas.Count, 0);
    }

    [Fact]
    public async Task RetirarEtiqueta_EmiteEtiquetaRetirada_CuandoTipoYNumeroLleganSinNormalizar()
    {
        DadoUnColaboradorConEtiquetaAsignada(Etiqueta.Crear("Area", "Ventas"));
        var comandoSinNormalizar = ComandoValido() with
        {
            TipoIdentificacion = "cc",
            NumeroIdentificacion = "  79543210  "
        };

        await WhenAsync(comandoSinNormalizar);

        Then(StreamIdEsperado, new EtiquetaRetirada("area"));
        And<ColaboradorAggregateRoot, int>(StreamIdEsperado, c => c.Etiquetas.Count, 0);
    }

    [Fact]
    public async Task RetirarEtiqueta_NoEmiteEvento_CuandoLaCategoriaNoExiste()
    {
        DadoUnColaboradorConVinculacionAbierta();

        await WhenAsync(ComandoValido());

        Then(StreamIdEsperado);
        And<ColaboradorAggregateRoot, int>(StreamIdEsperado, c => c.Etiquetas.Count, 0);
    }

    // Ningun otro test cubre asignar -> retirar -> retirar: los demas no-op parten de una categoria que
    // nunca estuvo en la vinculacion vigente.
    [Fact]
    public async Task RetirarEtiqueta_NoEmiteEvento_CuandoLaCategoriaYaFueRetirada()
    {
        Given(StreamIdEsperado,
            ColaboradorRegistradoValido(),
            VinculacionIniciadaVigente(),
            new EtiquetaAsignada(Etiqueta.Crear("Area", "Ventas")),
            new EtiquetaRetirada("area"));

        await WhenAsync(ComandoValido());

        Then(StreamIdEsperado);
        And<ColaboradorAggregateRoot, int>(StreamIdEsperado, c => c.Etiquetas.Count, 0);
    }

    // "Aera" no es "Area": categorias distintas normalizadas, asi que un error de transcripcion NO
    // aflora -- es no-op exitoso igual, y la etiqueta existente ("Area") queda intacta. Decision
    // deliberada del experto, no un descuido: no la conviertas en rechazo.
    [Fact]
    public async Task RetirarEtiqueta_NoEmiteEvento_CuandoHayUnErrorDeTranscripcionEnLaCategoria()
    {
        DadoUnColaboradorConEtiquetaAsignada(Etiqueta.Crear("Area", "Ventas"));

        await WhenAsync(ComandoValido() with { Categoria = "Aera" });

        Then(StreamIdEsperado);
        And<ColaboradorAggregateRoot, int>(StreamIdEsperado, c => c.Etiquetas.Count, 1);
    }

    [Fact]
    public async Task RetirarEtiqueta_LanzaReglaDeNegocioDeclinadaException_CuandoLaUltimaVinculacionTieneTerminacionRegistrada()
    {
        DadoUnColaboradorConEtiquetaYTerminacionRegistrada(
            Etiqueta.Crear("Area", "Ventas"), FechaEfectivaTerminacion);

        var act = async () => await WhenAsync(ComandoValido());

        await act.Should().ThrowExactlyAsync<Bitakora.ControlAsistencia.Colaboradores.Infraestructura.ReglaDeNegocioDeclinadaException>()
            .WithMessage($"*{RetirarEtiquetaCommandHandler.Mensajes.VinculacionTerminada}*");
        Then(StreamIdEsperado);
        And<ColaboradorAggregateRoot, int>(StreamIdEsperado, c => c.Etiquetas.Count, 1);
    }

    // Las etiquetas describen la relacion laboral activa: un preaviso sin vencer bloquea igual.
    [Fact]
    public async Task RetirarEtiqueta_LanzaReglaDeNegocioDeclinadaException_CuandoLaTerminacionEsUnPreavisoConFechaFutura()
    {
        var fechaPreavisoFutura = new DateOnly(2030, 1, 1);
        DadoUnColaboradorConEtiquetaYTerminacionRegistrada(
            Etiqueta.Crear("Area", "Ventas"), fechaPreavisoFutura);

        var act = async () => await WhenAsync(ComandoValido());

        await act.Should().ThrowExactlyAsync<Bitakora.ControlAsistencia.Colaboradores.Infraestructura.ReglaDeNegocioDeclinadaException>()
            .WithMessage($"*{RetirarEtiquetaCommandHandler.Mensajes.VinculacionTerminada}*");
        Then(StreamIdEsperado);
        And<ColaboradorAggregateRoot, int>(StreamIdEsperado, c => c.Etiquetas.Count, 1);
    }

    // Sin este test (el unico con dos categorias), un Apply que limpiara el diccionario entero pasaria
    // en verde.
    [Fact]
    public async Task RetirarEtiqueta_ConservaLasDemasCategorias_CuandoRetiraUnaDeVarias()
    {
        var etiquetaConservada = Etiqueta.Crear("Sede", "Medellín");
        Given(StreamIdEsperado,
            ColaboradorRegistradoValido(),
            VinculacionIniciadaVigente(),
            new EtiquetaAsignada(Etiqueta.Crear("Area", "Ventas")),
            new EtiquetaAsignada(etiquetaConservada));

        await WhenAsync(ComandoValido());

        Then(StreamIdEsperado, new EtiquetaRetirada("area"));
        And<ColaboradorAggregateRoot, int>(StreamIdEsperado, c => c.Etiquetas.Count, 1);
        And<ColaboradorAggregateRoot, Etiqueta>(
            StreamIdEsperado, c => c.Etiquetas["sede"], etiquetaConservada);
    }

    // El reingreso nace limpio: la vinculacion vigente no hereda la etiqueta de la anterior.
    [Fact]
    public async Task RetirarEtiqueta_NoEmiteEvento_CuandoLaEtiquetaPerteneceALaVinculacionAnteriorTrasUnReingreso()
    {
        Given(StreamIdEsperado,
            ColaboradorRegistradoValido(),
            VinculacionIniciadaVigente(),
            new EtiquetaAsignada(Etiqueta.Crear("Area", "Ventas")),
            new VinculacionTerminada(FechaEfectivaTerminacion),
            new VinculacionIniciada(CodigoVinculacionReingreso, FechaInicioReingreso));

        await WhenAsync(ComandoValido());

        Then(StreamIdEsperado);
        And<ColaboradorAggregateRoot, int>(StreamIdEsperado, c => c.Etiquetas.Count, 0);
    }

    // Sin And<>: el aggregate no existe en el TestStore; el Then vacio demuestra que no se escribio nada.
    [Fact]
    public async Task RetirarEtiqueta_LanzaRecursoNoEncontradoException_CuandoColaboradorNoExiste()
    {
        var act = async () => await WhenAsync(ComandoValido());

        await act.Should().ThrowExactlyAsync<Bitakora.ControlAsistencia.Colaboradores.Infraestructura.RecursoNoEncontradoException>()
            .WithMessage($"*{RetirarEtiquetaCommandHandler.Mensajes.ColaboradorNoEncontrado}*");
        Then(StreamIdEsperado);
    }
}
