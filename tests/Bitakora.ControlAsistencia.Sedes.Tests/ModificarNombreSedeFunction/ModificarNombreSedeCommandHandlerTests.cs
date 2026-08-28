// Issue #457 (MEF-ADR-0043 paso 2): modificar el nombre de una sede -- reemplazo completo del VO
// atomico Nombre. CA-ADR-0030: sin eventos de fallo -- el handler solo traduce "sede inexistente" a
// KeyNotFoundException (404, CA-4). CA-5: la bandera Activa (issue #459, sin implementar todavia)
// no se interroga -- una sede desactivada sigue siendo editable; sin evento de desactivacion en
// este dominio aun, este CA no tiene un escenario Given propio que construir en este issue.

using AwesomeAssertions;
using Bitakora.ControlAsistencia.Sedes.DomainEvents;
using Bitakora.ControlAsistencia.Sedes.Entities;
using Bitakora.ControlAsistencia.Sedes.ModificarNombreSedeFunction;
using Bitakora.ControlAsistencia.Sedes.ModificarNombreSedeFunction.CommandHandler;
using Cosmos.EventSourcing.Abstractions.Commands;
using Cosmos.EventSourcing.Testing.Utilities;

namespace Bitakora.ControlAsistencia.Sedes.Tests.ModificarNombreSedeFunction;

// El aggregate usa un stream ID compuesto (SedeAggregateRoot.ComputarStreamId, "s:{codigo}"), no
// el GuidAggregateId del harness -- overloads explicitos de Given/Then/And (regla 18 test-writer).
public class ModificarNombreSedeCommandHandlerTests : CommandHandlerAsyncTest<ModificarNombreSede>
{
    private const string Codigo = "SEDE-001";
    private const string NombreOriginal = "Sede Original";
    private const string NombreNuevo = "Sede Renombrada";
    private const string Ciudad = "Bogota";
    private const string Direccion = "Calle 100 # 10-20";

    // Oraculo independiente de la clave de stream (MEF-ADR-0002 + MEF-ADR-0037): literal, no
    // derivado de SedeAggregateRoot.ComputarStreamId.
    private const string StreamIdEsperado = "s:SEDE-001";

    protected override ICommandHandlerAsync<ModificarNombreSede> Handler =>
        new ModificarNombreSedeCommandHandler(EventStore);

    private static SedeRegistrada CrearSedeRegistrada() =>
        new(Codigo, NombreOriginal, Ciudad, Direccion);

    // CA-1: sede existente + comando con nombre nuevo -> el stream recibe NombreSedeModificado; el
    // aggregate rehidratado refleja el nombre nuevo.
    [Fact]
    public async Task ModificarNombreSede_EmiteNombreSedeModificado_CuandoSedeExiste()
    {
        Given(StreamIdEsperado, CrearSedeRegistrada());

        await WhenAsync(new ModificarNombreSede(Codigo, NombreNuevo));

        Then(StreamIdEsperado, new NombreSedeModificado(NombreNuevo));
        And<SedeAggregateRoot, string>(StreamIdEsperado, s => s.Nombre, NombreNuevo);
    }

    // CA-4: sede inexistente -> 404 (KeyNotFoundException), sin escribir nada al event store. Sin
    // Given: el stream no existe. Then sin eventos esperados demuestra "sin escribir nada al event
    // store" (mismo precedente que CorregirNombresCommandHandlerTests CA-4).
    [Fact]
    public async Task ModificarNombreSede_LanzaKeyNotFoundException_CuandoSedeNoExiste()
    {
        var act = async () => await WhenAsync(new ModificarNombreSede(Codigo, NombreNuevo));

        await act.Should().ThrowExactlyAsync<KeyNotFoundException>()
            .WithMessage($"*{ModificarNombreSedeCommandHandler.Mensajes.SedeNoEncontrada}*");
        Then(StreamIdEsperado);
    }
}
