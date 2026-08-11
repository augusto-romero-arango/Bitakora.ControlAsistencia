// Issue #330: registrar colaboradores bajo control de asistencia -- primer comando, primer
// aggregate y primeros dos eventos persistidos del dominio Colaboradores.

using AwesomeAssertions;
using Bitakora.ControlAsistencia.Colaboradores.DomainEvents;
using Bitakora.ControlAsistencia.Colaboradores.Entities;
using Bitakora.ControlAsistencia.Colaboradores.RegistrarColaborador.CommandHandler;
using Cosmos.EventSourcing.Abstractions.Commands;
using Cosmos.EventSourcing.Testing.Utilities;
using ComandoRegistrarColaborador = Bitakora.ControlAsistencia.Colaboradores.RegistrarColaborador.RegistrarColaborador;

namespace Bitakora.ControlAsistencia.Colaboradores.Tests.RegistrarColaborador;

// El aggregate usa un stream ID compuesto (Identificacion.ToString(), "CC:79543210"), no el
// GuidAggregateId del harness -- overloads explicitos de Given/Then/And (regla 18 del test-writer).
public class RegistrarColaboradorCommandHandlerTests : CommandHandlerAsyncTest<ComandoRegistrarColaborador>
{
    private static readonly TipoIdentificacion TipoCc = TipoIdentificacion.CC;
    private const string NumeroValido = "79543210";
    private const string CodigoColaborador = "COL-001";
    private static readonly DateOnly FechaInicioValida = new(2026, 1, 15);

    protected override ICommandHandlerAsync<ComandoRegistrarColaborador> Handler =>
        new RegistrarColaboradorCommandHandler(EventStore);

    private static ComandoRegistrarColaborador ComandoValido() => new(
        TipoIdentificacion: "CC",
        NumeroIdentificacion: NumeroValido,
        PrimerNombre: "Luis",
        SegundoNombre: "Augusto",
        PrimerApellido: "Barreto",
        SegundoApellido: null,
        CodigoColaborador: CodigoColaborador,
        FechaInicio: FechaInicioValida);

    private static Identificacion IdentificacionValida() => Identificacion.Crear(TipoCc, NumeroValido);

    private static NombreColaborador NombreValido() =>
        NombreColaborador.Crear("Luis", "Augusto", "Barreto", null);

    // CA-1: nace el stream con clave "CC:79543210" conteniendo ColaboradorRegistrado +
    // VinculacionIniciada persistidos en un solo commit, en ese orden.
    // CA-5: VinculacionIniciada persiste Codigo/FechaInicio tal como llegaron del request.
    [Fact]
    public async Task RegistrarColaborador_EmiteColaboradorRegistradoYVinculacionIniciada_CuandoIdentificacionNoExiste()
    {
        var comando = ComandoValido();
        var identificacion = IdentificacionValida();
        var nombre = NombreValido();
        var streamId = ColaboradorAggregateRoot.ComputarStreamId(identificacion);

        Given();
        await WhenAsync(comando);

        Then(streamId,
            new ColaboradorRegistrado(identificacion, nombre),
            new VinculacionIniciada(CodigoColaborador, FechaInicioValida));
        And<ColaboradorAggregateRoot, string>(streamId, c => c.Id, streamId);
        And<ColaboradorAggregateRoot, string>(streamId, c => c.CodigoVinculacionVigente, CodigoColaborador);
        And<ColaboradorAggregateRoot, DateOnly>(
            streamId, c => c.FechaInicioVinculacionVigente, FechaInicioValida);
    }

    // CA-2: identificacion ya registrada -> 409 Conflict (InvalidOperationException, no evento de
    // fallo persistido -- MEF-ADR-0004 capa 2, precedente CrearTurnoCommandHandler). El stream
    // existente no debe recibir ningun evento nuevo (verificado implicitamente: el handler nunca
    // llega a StartStream/AppendEvents si lanza antes).
    [Fact]
    public async Task RegistrarColaborador_LanzaInvalidOperationException_CuandoIdentificacionYaExiste()
    {
        var identificacionPrevia = IdentificacionValida();
        var nombrePrevio = NombreValido();
        var streamId = ColaboradorAggregateRoot.ComputarStreamId(identificacionPrevia);

        Given(streamId,
            new ColaboradorRegistrado(identificacionPrevia, nombrePrevio),
            new VinculacionIniciada("COL-000", new DateOnly(2025, 1, 1)));

        var comando = ComandoValido();
        var act = async () => await WhenAsync(comando);

        await act.Should().ThrowExactlyAsync<InvalidOperationException>()
            .WithMessage($"*{RegistrarColaboradorCommandHandler.Mensajes.ColaboradorYaRegistrado}*");
    }

    // CA-4: "cc" (minusculas) + numero con espacios, tras haber registrado "CC" con el mismo numero
    // normalizado -> 409. La normalizacion del NUMERO ya la garantiza Identificacion.Crear (#348,
    // trim+MAYUSCULAS); la normalizacion del CODIGO DE TIPO ("cc" -> "CC") es responsabilidad del
    // handler en el borde (TipoIdentificacion.Desde es case-sensitive por diseno, #348) --
    // desviacion documentada en el resumen de test-writer respecto del plan del planner.
    [Fact]
    public async Task RegistrarColaborador_LanzaInvalidOperationException_CuandoTipoYNumeroLleganSinNormalizarParaUnaIdentificacionYaRegistrada()
    {
        var identificacionPrevia = IdentificacionValida();
        var nombrePrevio = NombreValido();
        var streamId = ColaboradorAggregateRoot.ComputarStreamId(identificacionPrevia);

        Given(streamId,
            new ColaboradorRegistrado(identificacionPrevia, nombrePrevio),
            new VinculacionIniciada("COL-000", new DateOnly(2025, 1, 1)));

        var comandoSinNormalizar = ComandoValido() with
        {
            TipoIdentificacion = "cc",
            NumeroIdentificacion = "  79543210  "
        };
        var act = async () => await WhenAsync(comandoSinNormalizar);

        await act.Should().ThrowExactlyAsync<InvalidOperationException>()
            .WithMessage($"*{RegistrarColaboradorCommandHandler.Mensajes.ColaboradorYaRegistrado}*");
    }
}
