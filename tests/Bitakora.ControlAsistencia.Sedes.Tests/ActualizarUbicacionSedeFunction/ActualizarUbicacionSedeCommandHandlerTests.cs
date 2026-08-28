// Issue #457 (MEF-ADR-0043 paso 2): actualizar la ubicacion (Ciudad+Direccion) de una sede -- VO
// atomico, ambos campos opcionales. CA-ADR-0030: sin eventos de fallo -- el handler solo traduce
// "sede inexistente" a KeyNotFoundException (404, CA-4). CA-5: la bandera Activa (issue #459, sin
// implementar todavia) no se interroga -- una sede desactivada sigue siendo editable; sin evento de
// desactivacion en este dominio aun, este CA no tiene un escenario Given propio que construir en
// este issue.

using AwesomeAssertions;
using Bitakora.ControlAsistencia.Sedes.ActualizarUbicacionSedeFunction;
using Bitakora.ControlAsistencia.Sedes.ActualizarUbicacionSedeFunction.CommandHandler;
using Bitakora.ControlAsistencia.Sedes.DomainEvents;
using Bitakora.ControlAsistencia.Sedes.Entities;
using Cosmos.EventSourcing.Abstractions.Commands;
using Cosmos.EventSourcing.Testing.Utilities;

namespace Bitakora.ControlAsistencia.Sedes.Tests.ActualizarUbicacionSedeFunction;

// El aggregate usa un stream ID compuesto (SedeAggregateRoot.ComputarStreamId, "s:{codigo}"), no
// el GuidAggregateId del harness -- overloads explicitos de Given/Then/And (regla 18 test-writer).
public class ActualizarUbicacionSedeCommandHandlerTests : CommandHandlerAsyncTest<ActualizarUbicacionSede>
{
    private const string Codigo = "SEDE-001";
    private const string Nombre = "Sede Principal";
    private const string CiudadOriginal = "Bogota";
    private const string DireccionOriginal = "Calle 100 # 10-20";
    private const string CiudadNueva = "Medellin";
    private const string DireccionNueva = "Carrera 50 # 20-30";

    // Oraculo independiente de la clave de stream (MEF-ADR-0002 + MEF-ADR-0037): literal, no
    // derivado de SedeAggregateRoot.ComputarStreamId.
    private const string StreamIdEsperado = "s:SEDE-001";

    protected override ICommandHandlerAsync<ActualizarUbicacionSede> Handler =>
        new ActualizarUbicacionSedeCommandHandler(EventStore);

    private static SedeRegistrada CrearSedeRegistrada() =>
        new(Codigo, Nombre, CiudadOriginal, DireccionOriginal);

    // CA-3: sede existente + comando con ambos campos -> el stream recibe UbicacionActualizada; el
    // aggregate rehidratado refleja Ciudad y Direccion nuevas.
    [Fact]
    public async Task ActualizarUbicacionSede_EmiteUbicacionActualizada_CuandoAmbosCamposLlegan()
    {
        Given(StreamIdEsperado, CrearSedeRegistrada());

        await WhenAsync(new ActualizarUbicacionSede(Codigo, CiudadNueva, DireccionNueva));

        Then(StreamIdEsperado, new UbicacionActualizada(CiudadNueva, DireccionNueva));
        And<SedeAggregateRoot, string?>(StreamIdEsperado, s => s.Ciudad, CiudadNueva);
        And<SedeAggregateRoot, string?>(StreamIdEsperado, s => s.Direccion, DireccionNueva);
    }

    // CA-3: ambos campos son opcionales -- llegan nulos en el comando y se persisten como null
    // (reemplazo completo del valor atomico, no un merge parcial).
    [Fact]
    public async Task ActualizarUbicacionSede_EmiteUbicacionActualizadaConNulos_CuandoAmbosCamposLlegannNulos()
    {
        Given(StreamIdEsperado, CrearSedeRegistrada());

        await WhenAsync(new ActualizarUbicacionSede(Codigo, null, null));

        Then(StreamIdEsperado, new UbicacionActualizada(null, null));
        And<SedeAggregateRoot, string?>(StreamIdEsperado, s => s.Ciudad, null);
        And<SedeAggregateRoot, string?>(StreamIdEsperado, s => s.Direccion, null);
    }

    // CA-4: sede inexistente -> 404 (KeyNotFoundException), sin escribir nada al event store. Sin
    // Given: el stream no existe. Then sin eventos esperados demuestra "sin escribir nada al event
    // store" (mismo precedente que CorregirNombresCommandHandlerTests CA-4).
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
