using AwesomeAssertions;
using Bitakora.ControlAsistencia.Sedes.DomainEvents;
using Bitakora.ControlAsistencia.Sedes.Entities;
using Bitakora.ControlAsistencia.Sedes.Infraestructura;
using Bitakora.ControlAsistencia.Sedes.RegistrarSedeFunction;
using Bitakora.ControlAsistencia.Sedes.RegistrarSedeFunction.CommandHandler;
using Cosmos.EventSourcing.Abstractions.Commands;
using Cosmos.EventSourcing.Testing.Utilities;

namespace Bitakora.ControlAsistencia.Sedes.Tests.RegistrarSedeFunction;

// El aggregate usa un stream ID compuesto (SedeAggregateRoot.ComputarStreamId, "s:{codigo}"), no
// el GuidAggregateId del harness -- overloads explicitos de Given/Then/And (regla 18 test-writer).
public class RegistrarSedeCommandHandlerTests : CommandHandlerAsyncTest<RegistrarSede>
{
    private const string Codigo = "SEDE-001";
    private const string Nombre = "Sede Principal";
    private const string Ciudad = "Bogota";
    private const string Direccion = "Calle 100 # 10-20";

    // Oraculo independiente de la clave de stream (MEF-ADR-0002 + MEF-ADR-0037): literal, no
    // derivado de SedeAggregateRoot.ComputarStreamId -- derivarlo del propio codigo bajo prueba
    // haria tautologica la clave con la que Given/Then/And direccionan el stream.
    private const string StreamIdEsperado = "s:SEDE-001";

    protected override ICommandHandlerAsync<RegistrarSede> Handler =>
        new RegistrarSedeCommandHandler(EventStore);

    private static RegistrarSede ComandoValido() => new(Codigo, Nombre, Ciudad, Direccion);

    // CA-1: nace el stream con Codigo, Nombre, Ciudad y Direccion tal como llegaron del request.
    [Fact]
    public async Task RegistrarSede_EmiteSedeRegistrada_CuandoCodigoNoExiste()
    {
        Given();
        await WhenAsync(ComandoValido());

        Then(StreamIdEsperado, new SedeRegistrada(Codigo, Nombre, Ciudad, Direccion));
        And<SedeAggregateRoot, string>(StreamIdEsperado, s => s.Id, StreamIdEsperado);
        And<SedeAggregateRoot, string>(StreamIdEsperado, s => s.Nombre, Nombre);
        And<SedeAggregateRoot, string?>(StreamIdEsperado, s => s.Ciudad, Ciudad);
        And<SedeAggregateRoot, string?>(StreamIdEsperado, s => s.Direccion, Direccion);
    }

    // CA-2: Ciudad y Direccion son opcionales -- ausentes en el comando, persisten como null.
    [Fact]
    public async Task RegistrarSede_EmiteSedeRegistradaConCiudadYDireccionNulas_CuandoNoLlegan()
    {
        Given();
        await WhenAsync(new RegistrarSede(Codigo, Nombre, null, null));

        Then(StreamIdEsperado, new SedeRegistrada(Codigo, Nombre, null, null));
        And<SedeAggregateRoot, string?>(StreamIdEsperado, s => s.Ciudad, null);
        And<SedeAggregateRoot, string?>(StreamIdEsperado, s => s.Direccion, null);
    }

    [Fact]
    public async Task RegistrarSede_LanzaRecursoYaExisteException_CuandoCodigoYaExiste()
    {
        Given(StreamIdEsperado, new SedeRegistrada(Codigo, "Sede Original", Ciudad, Direccion));

        var act = async () => await WhenAsync(ComandoValido() with { Nombre = "Otro Nombre" });

        await act.Should().ThrowExactlyAsync<RecursoYaExisteException>()
            .WithMessage($"*{RegistrarSedeCommandHandler.Mensajes.SedeYaRegistrada}*");
        Then(StreamIdEsperado);
        And<SedeAggregateRoot, string>(StreamIdEsperado, s => s.Nombre, "Sede Original");
    }
}
