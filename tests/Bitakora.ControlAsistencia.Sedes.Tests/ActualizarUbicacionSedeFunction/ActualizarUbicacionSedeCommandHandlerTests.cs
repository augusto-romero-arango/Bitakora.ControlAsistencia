using AwesomeAssertions;
using Bitakora.ControlAsistencia.Sedes.ActualizarUbicacionSedeFunction;
using Bitakora.ControlAsistencia.Sedes.ActualizarUbicacionSedeFunction.CommandHandler;
using Bitakora.ControlAsistencia.Sedes.DomainEvents;
using Bitakora.ControlAsistencia.Sedes.Entities;
using Cosmos.EventSourcing.Abstractions.Commands;
using Cosmos.EventSourcing.Testing.Utilities;

namespace Bitakora.ControlAsistencia.Sedes.Tests.ActualizarUbicacionSedeFunction;

// El aggregate usa stream ID compuesto, no el GuidAggregateId del harness: Given/Then/And exigen
// los overloads que reciben el streamId explicito.
public class ActualizarUbicacionSedeCommandHandlerTests : CommandHandlerAsyncTest<ActualizarUbicacionSede>
{
    private const string Codigo = "SEDE-001";
    private const string Nombre = "Sede Principal";
    private const string CiudadOriginal = "Bogota";
    private const string DireccionOriginal = "Calle 100 # 10-20";
    private const string CiudadNueva = "Medellin";
    private const string DireccionNueva = "Carrera 50 # 20-30";

    // Oraculo independiente de la clave de stream: literal, nunca derivado de ComputarStreamId.
    private const string StreamIdEsperado = "s:SEDE-001";

    protected override ICommandHandlerAsync<ActualizarUbicacionSede> Handler =>
        new ActualizarUbicacionSedeCommandHandler(EventStore);

    private static SedeRegistrada CrearSedeRegistrada() =>
        new(Codigo, Nombre, CiudadOriginal, DireccionOriginal);

    // CA-3
    [Fact]
    public async Task ActualizarUbicacionSede_EmiteUbicacionActualizada_CuandoAmbosCamposLlegan()
    {
        Given(StreamIdEsperado, CrearSedeRegistrada());

        await WhenAsync(new ActualizarUbicacionSede(Codigo, CiudadNueva, DireccionNueva));

        Then(StreamIdEsperado, new UbicacionActualizada(CiudadNueva, DireccionNueva));
        And<SedeAggregateRoot, string?>(StreamIdEsperado, s => s.Ciudad, CiudadNueva);
        And<SedeAggregateRoot, string?>(StreamIdEsperado, s => s.Direccion, DireccionNueva);
    }

    // CA-3: los nulos se persisten como reemplazo completo, no como merge parcial que conserva la
    // ubicacion anterior.
    [Fact]
    public async Task ActualizarUbicacionSede_EmiteUbicacionActualizadaConNulos_CuandoAmbosCamposLleganNulos()
    {
        Given(StreamIdEsperado, CrearSedeRegistrada());

        await WhenAsync(new ActualizarUbicacionSede(Codigo, null, null));

        Then(StreamIdEsperado, new UbicacionActualizada(null, null));
        And<SedeAggregateRoot, string?>(StreamIdEsperado, s => s.Ciudad, null);
        And<SedeAggregateRoot, string?>(StreamIdEsperado, s => s.Direccion, null);
    }

    // CA-4: sin Given -- el stream no existe. El Then sin eventos esperados es la asercion de que
    // nada se escribio al event store.
    [Fact]
    public async Task ActualizarUbicacionSede_LanzaKeyNotFoundException_CuandoSedeNoExiste()
    {
        var act = async () =>
            await WhenAsync(new ActualizarUbicacionSede(Codigo, CiudadNueva, DireccionNueva));

        await act.Should().ThrowExactlyAsync<KeyNotFoundException>()
            .WithMessage($"*{ActualizarUbicacionSedeCommandHandler.Mensajes.SedeNoEncontrada}*");
        Then(StreamIdEsperado);
    }
}
