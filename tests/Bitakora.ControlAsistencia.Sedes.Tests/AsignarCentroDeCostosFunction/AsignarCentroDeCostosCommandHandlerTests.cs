using AwesomeAssertions;
using Bitakora.ControlAsistencia.Sedes.AsignarCentroDeCostosFunction;
using Bitakora.ControlAsistencia.Sedes.AsignarCentroDeCostosFunction.CommandHandler;
using Bitakora.ControlAsistencia.Sedes.DomainEvents;
using Bitakora.ControlAsistencia.Sedes.Entities;
using Cosmos.EventSourcing.Abstractions.Commands;
using Cosmos.EventSourcing.Testing.Utilities;

namespace Bitakora.ControlAsistencia.Sedes.Tests.AsignarCentroDeCostosFunction;

// El aggregate usa stream ID compuesto, no el GuidAggregateId del harness: Given/Then/And exigen
// los overloads que reciben el streamId explicito.
public class AsignarCentroDeCostosCommandHandlerTests : CommandHandlerAsyncTest<AsignarCentroDeCostos>
{
    private const string Codigo = "SEDE-001";
    private const string Nombre = "Sede Principal";
    private const string CentroDeCostosNuevo = "CC-100";
    private const string CentroDeCostosPrevio = "CC-050";

    // Espacios y minusculas deliberados: si alguien agregara Trim/ToUpper al asignar, el
    // evento dejaria de llevar el string que envio el cliente.
    private const string CentroDeCostosSinNormalizar = "  cc-100 / bodega  ";

    // Oraculo independiente de la clave de stream: literal, nunca derivado de ComputarStreamId.
    private const string StreamIdEsperado = "s:SEDE-001";

    protected override ICommandHandlerAsync<AsignarCentroDeCostos> Handler =>
        new AsignarCentroDeCostosCommandHandler(EventStore);

    private static SedeRegistrada CrearSedeRegistrada() => new(Codigo, Nombre, null, null);

    // CA-1
    [Fact]
    public async Task AsignarCentroDeCostos_EmiteCentroDeCostosAsignado_CuandoSedeExiste()
    {
        Given(StreamIdEsperado, CrearSedeRegistrada());

        await WhenAsync(new AsignarCentroDeCostos(Codigo, CentroDeCostosNuevo));

        Then(StreamIdEsperado, new CentroDeCostosAsignado(CentroDeCostosNuevo));
        And<SedeAggregateRoot, string?>(StreamIdEsperado, s => s.CentroDeCostos, CentroDeCostosNuevo);
    }

    // CA-2
    [Fact]
    public async Task AsignarCentroDeCostos_EmiteCentroDeCostosAsignado_CuandoLaSedeYaTieneUnCentroVigente()
    {
        Given(StreamIdEsperado, CrearSedeRegistrada(), new CentroDeCostosAsignado(CentroDeCostosPrevio));

        await WhenAsync(new AsignarCentroDeCostos(Codigo, CentroDeCostosNuevo));

        Then(StreamIdEsperado, new CentroDeCostosAsignado(CentroDeCostosNuevo));
        And<SedeAggregateRoot, string?>(StreamIdEsperado, s => s.CentroDeCostos, CentroDeCostosNuevo);
    }

    // CA-5: sede inexistente -> KeyNotFoundException, sin escribir nada al event store.
    [Fact]
    public async Task AsignarCentroDeCostos_LanzaKeyNotFoundException_CuandoSedeNoExiste()
    {
        var act = async () => await WhenAsync(new AsignarCentroDeCostos(Codigo, CentroDeCostosNuevo));

        await act.Should().ThrowExactlyAsync<KeyNotFoundException>()
            .WithMessage($"*{AsignarCentroDeCostosCommandHandler.Mensajes.SedeNoEncontrada}*");
        Then(StreamIdEsperado);
    }

    // CA-1: el CC es opaco -- se estampa byte a byte, sin trim, sin casing, sin validacion contra
    // catalogo alguno.
    [Fact]
    public async Task AsignarCentroDeCostos_EstampaElCentroDeCostosSinNormalizar_CuandoTraeEspaciosYMinusculas()
    {
        Given(StreamIdEsperado, CrearSedeRegistrada());

        await WhenAsync(new AsignarCentroDeCostos(Codigo, CentroDeCostosSinNormalizar));

        Then(StreamIdEsperado, new CentroDeCostosAsignado(CentroDeCostosSinNormalizar));
        And<SedeAggregateRoot, string?>(StreamIdEsperado, s => s.CentroDeCostos, CentroDeCostosSinNormalizar);
    }
}
