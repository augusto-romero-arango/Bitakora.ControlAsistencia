using AwesomeAssertions;
using Bitakora.ControlAsistencia.Colaboradores.CorregirNombresFunction;
using Bitakora.ControlAsistencia.Colaboradores.CorregirNombresFunction.CommandHandler;
using Bitakora.ControlAsistencia.Colaboradores.DomainEvents;
using Bitakora.ControlAsistencia.Colaboradores.Entities;
using Cosmos.EventSourcing.Abstractions.Commands;
using Cosmos.EventSourcing.Testing.Utilities;

namespace Bitakora.ControlAsistencia.Colaboradores.Tests.CorregirNombresFunction;

public class CorregirNombresCommandHandlerTests : CommandHandlerAsyncTest<CorregirNombres>
{
    private const string NumeroValido = "79543210";

    // Oraculo independiente: literal, nunca derivado de ComputarStreamId -- si se derivara, un cambio
    // de formato de la clave se auto-validaria.
    private const string StreamIdEsperado = "CC-79543210";

    private const string CodigoVinculacionOriginal = "COL-001";
    private static readonly DateOnly FechaInicioOriginal = new(2026, 1, 15);
    private static readonly DateOnly FechaEfectivaTerminacion = new(2026, 6, 1);

    protected override ICommandHandlerAsync<CorregirNombres> Handler =>
        new CorregirNombresCommandHandler(EventStore);

    private static Identificacion IdentificacionValida() =>
        Identificacion.Crear(TipoIdentificacion.CC, NumeroValido);

    private static NombreColaborador NombreOriginal() =>
        NombreColaborador.Crear("Luis", "Augusto", "Barreto", null);

    private static ColaboradorRegistrado ColaboradorRegistradoValido() =>
        new(IdentificacionValida(), NombreOriginal());

    private static VinculacionIniciada VinculacionIniciadaOriginal() =>
        new(CodigoVinculacionOriginal, FechaInicioOriginal);

    private static CorregirNombres ComandoConNombreDistinto() => new(
        TipoIdentificacion: "CC",
        NumeroIdentificacion: NumeroValido,
        PrimerNombre: "Luis",
        SegundoNombre: "Alberto", // distinto del original ("Augusto")
        PrimerApellido: "Barreto",
        SegundoApellido: null);

    private static CorregirNombres ComandoConElMismoNombre() => new(
        TipoIdentificacion: "CC",
        NumeroIdentificacion: NumeroValido,
        PrimerNombre: "Luis",
        SegundoNombre: "Augusto",
        PrimerApellido: "Barreto",
        SegundoApellido: null);

    private void DadoUnColaboradorConVinculacionAbierta() =>
        Given(StreamIdEsperado, ColaboradorRegistradoValido(), VinculacionIniciadaOriginal());

    private void DadoUnColaboradorConVinculacionTerminada() =>
        Given(StreamIdEsperado,
            ColaboradorRegistradoValido(),
            VinculacionIniciadaOriginal(),
            new VinculacionTerminada(FechaEfectivaTerminacion));

    [Fact]
    public async Task CorregirNombres_EmiteNombresCorregidos_CuandoElNombreEsDistintoPorValor()
    {
        DadoUnColaboradorConVinculacionAbierta();
        var nombreEsperado = NombreColaborador.Crear("Luis", "Alberto", "Barreto", null);

        await WhenAsync(ComandoConNombreDistinto());

        Then(StreamIdEsperado, new NombresCorregidos(nombreEsperado));
        And<ColaboradorAggregateRoot, string>(
            StreamIdEsperado, c => c.Nombre.NombreCompleto, "Luis Alberto Barreto");
    }

    // Sin normalizar tipo y numero, el handler computaria otra clave y responderia 404 sobre un
    // colaborador que si existe.
    [Fact]
    public async Task CorregirNombres_EmiteNombresCorregidos_CuandoTipoYNumeroLleganSinNormalizar()
    {
        DadoUnColaboradorConVinculacionAbierta();
        var comandoSinNormalizar = ComandoConNombreDistinto() with
        {
            TipoIdentificacion = "cc",
            NumeroIdentificacion = "  79543210  "
        };

        await WhenAsync(comandoSinNormalizar);

        Then(StreamIdEsperado, new NombresCorregidos(NombreColaborador.Crear("Luis", "Alberto", "Barreto", null)));
        And<ColaboradorAggregateRoot, string>(
            StreamIdEsperado, c => c.Nombre.NombreCompleto, "Luis Alberto Barreto");
    }

    // Los nombres son de la persona, no de la vinculacion: solo se exige que el colaborador exista.
    [Fact]
    public async Task CorregirNombres_EmiteNombresCorregidos_CuandoLaVinculacionEstaTerminada()
    {
        DadoUnColaboradorConVinculacionTerminada();
        var nombreEsperado = NombreColaborador.Crear("Luis", "Alberto", "Barreto", null);

        await WhenAsync(ComandoConNombreDistinto());

        Then(StreamIdEsperado, new NombresCorregidos(nombreEsperado));
        And<ColaboradorAggregateRoot, string>(
            StreamIdEsperado, c => c.Nombre.NombreCompleto, "Luis Alberto Barreto");
        And<ColaboradorAggregateRoot, DateOnly?>(
            StreamIdEsperado, c => c.FechaTerminacionVinculacionVigente, FechaEfectivaTerminacion);
    }

    [Fact]
    public async Task CorregirNombres_NoEmiteEvento_CuandoElNombreEsIgualPorValorAlActual()
    {
        DadoUnColaboradorConVinculacionAbierta();

        await WhenAsync(ComandoConElMismoNombre());

        Then(StreamIdEsperado);
        And<ColaboradorAggregateRoot, string>(
            StreamIdEsperado, c => c.Nombre.NombreCompleto, "Luis Augusto Barreto");
    }

    // La comparacion ocurre despues de normalizar: el whitespace se vuelve ausente, igual que el original.
    [Fact]
    public async Task CorregirNombres_NoEmiteEvento_CuandoElSegundoApellidoLlegaComoWhitespaceYElOriginalEsAusente()
    {
        DadoUnColaboradorConVinculacionAbierta();

        await WhenAsync(ComandoConElMismoNombre() with { SegundoApellido = "   " });

        Then(StreamIdEsperado);
        And<ColaboradorAggregateRoot, string>(
            StreamIdEsperado, c => c.Nombre.NombreCompleto, "Luis Augusto Barreto");
    }

    [Fact]
    public async Task CorregirNombres_LanzaRecursoNoEncontradoException_CuandoColaboradorNoExiste()
    {
        var act = async () => await WhenAsync(ComandoConNombreDistinto());

        await act.Should().ThrowExactlyAsync<Bitakora.ControlAsistencia.Colaboradores.Infraestructura.RecursoNoEncontradoException>()
            .WithMessage($"*{CorregirNombresCommandHandler.Mensajes.ColaboradorNoEncontrado}*");
        Then(StreamIdEsperado);
    }
}
