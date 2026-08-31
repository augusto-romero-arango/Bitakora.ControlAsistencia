// Issue #330: registrar colaboradores bajo control de asistencia -- primer comando, primer
// aggregate y primeros dos eventos persistidos del dominio Colaboradores.

using AwesomeAssertions;
using Bitakora.ControlAsistencia.Colaboradores.DomainEvents;
using Bitakora.ControlAsistencia.Colaboradores.Entities;
using Bitakora.ControlAsistencia.Colaboradores.RegistrarColaboradorFunction;
using Bitakora.ControlAsistencia.Colaboradores.RegistrarColaboradorFunction.CommandHandler;
using Cosmos.EventSourcing.Abstractions.Commands;
using Cosmos.EventSourcing.Testing.Utilities;

namespace Bitakora.ControlAsistencia.Colaboradores.Tests.RegistrarColaboradorFunction;

// El aggregate usa un stream ID compuesto (Identificacion.ToString(), "CC-79543210"), no el
// GuidAggregateId del harness -- overloads explicitos de Given/Then/And (regla 18 del test-writer).
public class RegistrarColaboradorCommandHandlerTests : CommandHandlerAsyncTest<RegistrarColaborador>
{
    private const string NumeroValido = "79543210";
    private const string CodigoColaborador = "COL-001";
    private static readonly DateOnly FechaInicioValida = new(2026, 1, 15);

    // Issue #520: CodigoSede opcional en el body del ingreso.
    private const string CodigoSedeBogota = "BOG";

    // Oraculo independiente de la clave de stream (MEF-ADR-0002 + MEF-ADR-0037: el formato de la
    // identidad de stream es contrato de datos, no estilo). Se declara como literal y NO como
    // ColaboradorAggregateRoot.ComputarStreamId(...): derivarlo del propio codigo bajo prueba haria
    // tautologica la clave con la que Given/Then/And direccionan el stream, y un cambio de formato
    // -- otro separador, otro orden, otro casing -- pasaria en verde partiendo streams en produccion.
    private const string StreamIdEsperado = "CC-79543210";

    // Vinculacion previa del stream ya registrado (CA-2/CA-4): distinta de la del comando, para que
    // el And posterior distinga "no cambio nada" de "coincide por casualidad".
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

    // Precondicion compartida por CA-2 y CA-4: el colaborador ya esta registrado con una vinculacion
    // distinta de la que trae el comando.
    private void DadoUnColaboradorYaRegistrado() =>
        Given(StreamIdEsperado,
            new ColaboradorRegistrado(IdentificacionValida(), NombreValido()),
            new VinculacionIniciada(CodigoVinculacionPrevia, FechaInicioVinculacionPrevia));

    // CA-1: nace el stream con clave "CC-79543210" conteniendo ColaboradorRegistrado +
    // VinculacionIniciada persistidos en un solo commit, en ese orden.
    // CA-2/CA-5: VinculacionIniciada persiste Codigo/FechaInicio tal como llegaron del request, y
    // sin CodigoSede en el comando queda con sede null (compatibilidad: los clientes actuales no
    // cambian).
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

    // CA-1: registrar CON CodigoSede emite VinculacionIniciada con la sede DENTRO del evento (nunca
    // un SedeAsignada adicional en el commit); rehidratado, la sede queda asentada.
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

    // CA-2: identificacion ya registrada -> 409 Conflict (InvalidOperationException, no evento de
    // fallo persistido -- MEF-ADR-0004 capa 2, precedente CrearTurnoCommandHandler). Then sin eventos
    // esperados verifica la segunda mitad del CA -- el stream existente no recibe ningun evento
    // nuevo -- y el And que la vinculacion vigente sigue siendo la previa, no la del comando
    // rechazado (el 409 no puede dejar el stream a medio escribir).
    [Fact]
    public async Task RegistrarColaborador_LanzaInvalidOperationException_CuandoIdentificacionYaExiste()
    {
        DadoUnColaboradorYaRegistrado();

        var act = async () => await WhenAsync(ComandoValido());

        await act.Should().ThrowExactlyAsync<InvalidOperationException>()
            .WithMessage($"*{RegistrarColaboradorCommandHandler.Mensajes.ColaboradorYaRegistrado}*");
        Then(StreamIdEsperado);
        And<ColaboradorAggregateRoot, string>(
            StreamIdEsperado, c => c.CodigoVinculacionVigente, CodigoVinculacionPrevia);
    }

    // CA-4: "cc" (minusculas) + numero con espacios, tras haber registrado "CC" con el mismo numero
    // normalizado -> 409. La normalizacion del NUMERO ya la garantiza Identificacion.Crear (#348,
    // trim+MAYUSCULAS); la normalizacion del CODIGO DE TIPO ("cc" -> "CC") la garantiza
    // TipoIdentificacion.Desde, que normaliza internamente (issue #371 -- supersede el racional de
    // #348, ver TipoIdentificacionTests).
    // El Then sin eventos esperados es la asercion clave: si Desde no normalizara, el handler
    // computaria otra clave, no encontraria el stream y nacerian dos personas -- un throw esperado
    // por si solo no distingue "no se registro nada" de "se registro en la clave equivocada".
    [Fact]
    public async Task RegistrarColaborador_LanzaInvalidOperationException_CuandoTipoYNumeroLleganSinNormalizarParaUnaIdentificacionYaRegistrada()
    {
        DadoUnColaboradorYaRegistrado();
        var comandoSinNormalizar = ComandoValido() with
        {
            TipoIdentificacion = "cc",
            NumeroIdentificacion = "  79543210  "
        };

        var act = async () => await WhenAsync(comandoSinNormalizar);

        await act.Should().ThrowExactlyAsync<InvalidOperationException>()
            .WithMessage($"*{RegistrarColaboradorCommandHandler.Mensajes.ColaboradorYaRegistrado}*");
        Then(StreamIdEsperado);
        And<ColaboradorAggregateRoot, string>(
            StreamIdEsperado, c => c.CodigoVinculacionVigente, CodigoVinculacionPrevia);
    }
}
