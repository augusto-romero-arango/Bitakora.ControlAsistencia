using AwesomeAssertions;
using Bitakora.ControlAsistencia.Sedes.DomainEvents;
using Bitakora.ControlAsistencia.Sedes.Entities;
using Bitakora.ControlAsistencia.Sedes.ModificarNombreSedeFunction;
using Bitakora.ControlAsistencia.Sedes.ModificarNombreSedeFunction.CommandHandler;
using Cosmos.EventSourcing.Abstractions.Commands;
using Cosmos.EventSourcing.Testing.Utilities;

namespace Bitakora.ControlAsistencia.Sedes.Tests.ModificarNombreSedeFunction;

// El aggregate usa stream ID compuesto, no el GuidAggregateId del harness: Given/Then/And exigen
// los overloads que reciben el streamId explicito.
public class ModificarNombreSedeCommandHandlerTests : CommandHandlerAsyncTest<ModificarNombreSede>
{
    private const string Codigo = "SEDE-001";
    private const string NombreOriginal = "Sede Original";
    private const string NombreNuevo = "Sede Renombrada";
    private const string Ciudad = "Bogota";
    private const string Direccion = "Calle 100 # 10-20";

    // Oraculo independiente de la clave de stream: literal, nunca derivado de ComputarStreamId.
    private const string StreamIdEsperado = "s:SEDE-001";

    protected override ICommandHandlerAsync<ModificarNombreSede> Handler =>
        new ModificarNombreSedeCommandHandler(EventStore);

    private static SedeRegistrada CrearSedeRegistrada() =>
        new(Codigo, NombreOriginal, Ciudad, Direccion);

    // CA-1
    [Fact]
    public async Task ModificarNombreSede_EmiteNombreSedeModificado_CuandoSedeExiste()
    {
        Given(StreamIdEsperado, CrearSedeRegistrada());

        await WhenAsync(new ModificarNombreSede(Codigo, NombreNuevo));

        Then(StreamIdEsperado, new NombreSedeModificado(NombreNuevo));
        And<SedeAggregateRoot, string>(StreamIdEsperado, s => s.Nombre, NombreNuevo);
    }

    // CA-4: sin Given -- el stream no existe. El Then sin eventos esperados es la asercion de que
    // nada se escribio al event store.
    [Fact]
    public async Task ModificarNombreSede_LanzaKeyNotFoundException_CuandoSedeNoExiste()
    {
        var act = async () => await WhenAsync(new ModificarNombreSede(Codigo, NombreNuevo));

        await act.Should().ThrowExactlyAsync<KeyNotFoundException>()
            .WithMessage($"*{ModificarNombreSedeCommandHandler.Mensajes.SedeNoEncontrada}*");
        Then(StreamIdEsperado);
    }
}
