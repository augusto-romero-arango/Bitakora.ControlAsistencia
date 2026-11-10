using AwesomeAssertions;
using Bitakora.ControlAsistencia.Colaboradores.DomainEvents;
using Bitakora.ControlAsistencia.Colaboradores.Entities;
using Bitakora.ControlAsistencia.Colaboradores.Infraestructura;
using Bitakora.ControlAsistencia.Colaboradores.RegistrarColaboradorFunction;
using Bitakora.ControlAsistencia.Colaboradores.RegistrarColaboradorFunction.CommandHandler;
using Cosmos.EventSourcing.Abstractions.Commands;
using Cosmos.EventSourcing.Testing.Utilities;

namespace Bitakora.ControlAsistencia.Colaboradores.Tests.RegistrarColaboradorFunction;

public class RegistrarColaboradorCommandHandlerTests : CommandHandlerAsyncTest<RegistrarColaborador>
{
    private const string NumeroValido = "79543210";
    private const string CodigoColaborador = "COL-001";
    private static readonly DateOnly FechaInicioValida = new(2026, 1, 15);

    private const string CodigoSedeBogota = "BOG";

    // Oraculo independiente: literal, nunca derivado de ComputarStreamId. Derivarlo haria tautologica
    // la clave, y un cambio de formato (separador, orden, casing) pasaria en verde partiendo streams en
    // produccion.
    private const string StreamIdEsperado = "CC-79543210";

    // Distinta de la del comando, para que el And distinga "no cambio nada" de "coincide por casualidad".
    private const string CodigoVinculacionPrevia = "COL-000";
    private static readonly DateOnly FechaInicioVinculacionPrevia = new(2025, 1, 1);

    protected override ICommandHandlerAsync<RegistrarColaborador> Handler =>
        new RegistrarColaboradorCommandHandler(EventStore);

    private static RegistrarColaborador ComandoValido() => new(
        TipoIdentificacion: "CC",
        NumeroIdentificacion: NumeroValido,
        PrimerNombre: "Luis",
        SegundoNombre: "Augusto",
        PrimerApellido: "Barreto",
        SegundoApellido: null,
        CodigoColaborador: CodigoColaborador,
        FechaInicio: FechaInicioValida);

    private static Identificacion IdentificacionValida() =>
        Identificacion.Crear(TipoIdentificacion.CC, NumeroValido);

    private static NombreColaborador NombreValido() =>
        NombreColaborador.Crear("Luis", "Augusto", "Barreto", null);

    private void DadoUnColaboradorYaRegistrado() =>
        Given(StreamIdEsperado,
            new ColaboradorRegistrado(IdentificacionValida(), NombreValido()),
            new VinculacionIniciada(CodigoVinculacionPrevia, FechaInicioVinculacionPrevia));

    // Sin CodigoSede en el comando la sede queda null: los clientes actuales no cambian.
    [Fact]
    public async Task RegistrarColaborador_EmiteColaboradorRegistradoYVinculacionIniciada_CuandoIdentificacionNoExiste()
    {
        Given();
        await WhenAsync(ComandoValido());

        Then(StreamIdEsperado,
            new ColaboradorRegistrado(IdentificacionValida(), NombreValido()),
            new VinculacionIniciada(CodigoColaborador, FechaInicioValida, null));
        And<ColaboradorAggregateRoot, string>(StreamIdEsperado, c => c.Id, StreamIdEsperado);
        And<ColaboradorAggregateRoot, string>(
            StreamIdEsperado, c => c.Identificacion.ToString(), StreamIdEsperado);
        And<ColaboradorAggregateRoot, string>(
            StreamIdEsperado, c => c.Nombre.NombreCompleto, "Luis Augusto Barreto");
        And<ColaboradorAggregateRoot, string>(
            StreamIdEsperado, c => c.CodigoVinculacionVigente, CodigoColaborador);
        And<ColaboradorAggregateRoot, DateOnly>(
            StreamIdEsperado, c => c.FechaInicioVinculacionVigente, FechaInicioValida);
        And<ColaboradorAggregateRoot, string?>(StreamIdEsperado, c => c.CodigoSede, null);
    }

    [Fact]
    public async Task RegistrarColaborador_EmiteVinculacionIniciadaConSede_CuandoCodigoSedeLlegaEnElComando()
    {
        Given();
        await WhenAsync(ComandoValido() with { CodigoSede = CodigoSedeBogota });

        Then(StreamIdEsperado,
            new ColaboradorRegistrado(IdentificacionValida(), NombreValido()),
            new VinculacionIniciada(CodigoColaborador, FechaInicioValida, CodigoSedeBogota));
        And<ColaboradorAggregateRoot, string?>(StreamIdEsperado, c => c.CodigoSede, CodigoSedeBogota);
    }

    // Sin evento de fallo persistido: el 409 no puede dejar el stream a medio escribir.
    [Fact]
    public async Task RegistrarColaborador_LanzaRecursoYaExisteException_CuandoIdentificacionYaExiste()
    {
        DadoUnColaboradorYaRegistrado();

        var act = async () => await WhenAsync(ComandoValido());

        await act.Should().ThrowExactlyAsync<RecursoYaExisteException>()
            .WithMessage($"*{RegistrarColaboradorCommandHandler.Mensajes.ColaboradorYaRegistrado}*");
        Then(StreamIdEsperado);
        And<ColaboradorAggregateRoot, string>(
            StreamIdEsperado, c => c.CodigoVinculacionVigente, CodigoVinculacionPrevia);
    }

    // El Then sin eventos es la asercion clave: sin normalizar, el handler computaria otra clave y
    // nacerian dos personas; el throw solo no distingue "no se registro nada" de "se registro en la
    // clave equivocada".
    [Fact]
    public async Task RegistrarColaborador_LanzaRecursoYaExisteException_CuandoTipoYNumeroLleganSinNormalizarParaUnaIdentificacionYaRegistrada()
    {
        DadoUnColaboradorYaRegistrado();
        var comandoSinNormalizar = ComandoValido() with
        {
            TipoIdentificacion = "cc",
            NumeroIdentificacion = "  79543210  "
        };

        var act = async () => await WhenAsync(comandoSinNormalizar);

        await act.Should().ThrowExactlyAsync<RecursoYaExisteException>()
            .WithMessage($"*{RegistrarColaboradorCommandHandler.Mensajes.ColaboradorYaRegistrado}*");
        Then(StreamIdEsperado);
        And<ColaboradorAggregateRoot, string>(
            StreamIdEsperado, c => c.CodigoVinculacionVigente, CodigoVinculacionPrevia);
    }
}
