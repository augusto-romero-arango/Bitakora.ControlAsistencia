// Issue #354, CA-2: composicion de la correccion de una fecha de terminacion errada. El comando
// "CorregirFechaTerminacionVinculacion" que el desglose original planeaba desaparecio (decision de
// refinamiento 2026-08-11): corregir es anular + terminar de nuevo, dos intenciones que componen.
//
// Este archivo prueba el segundo tramo de esa composicion -- que TerminarVinculacion (#349) vuelve
// a tener exito con la fecha correcta una vez que la terminacion errada quedo anulada -- SIN
// invocar dos handlers en vivo sobre el mismo TestStore. Verificado contra la fuente del harness
// (TestStore.cs, Cosmos.EventSourcing.Testing.Utilities): GetAggregateRootAsync -- la via que
// CUALQUIER handler de produccion usa para rehidratar, incluido AnularTerminacionCommandHandler y
// TerminarVinculacionCommandHandler -- lee UNICAMENTE _previousEvents, nunca _newEvents. Encadenar
// WhenAsync(AnularTerminacion) seguido de un HandleAsync manual de TerminarVinculacionCommandHandler
// sobre el mismo EventStore no haria visible al segundo handler el TerminacionAnulada que el
// primero acaba de commitear (SaveChanges lo mueve a _newEvents, no a _previousEvents) -- el
// segundo handler veria la vinculacion todavia terminada y el test fallaria con un falso 409 que no
// refleja ningun defecto de AnularTerminacion ni de TerminarVinculacion.
//
// El patron idiomatico y tecnicamente correcto -- ya usado en TerminarVinculacionCommandHandlerTests
// para probar la composicion con el reingreso de #350 (Given incluye la VinculacionIniciada que
// ReingresarColaborador habria commiteado en una request anterior) -- es construir con Given() la
// historia YA persistida, incluyendo aqui el TerminacionAnulada que AnularTerminacion habria
// commiteado, e invocar con WhenAsync solo el comando bajo prueba (TerminarVinculacion).
//
// Este archivo NO modifica TerminarVinculacionCommandHandlerTests.cs (fuera de "Impacto/Modifica"
// del issue #354): es un archivo nuevo, en la carpeta del comando que introduce el escenario de
// composicion, que prueba exactamente la claim de negocio de CA-2 sin tocar un archivo existente.

using Bitakora.ControlAsistencia.Colaboradores.DomainEvents;
using Bitakora.ControlAsistencia.Colaboradores.Entities;
using Bitakora.ControlAsistencia.Colaboradores.TerminarVinculacionFunction;
using Bitakora.ControlAsistencia.Colaboradores.TerminarVinculacionFunction.CommandHandler;
using Cosmos.EventSourcing.Abstractions.Commands;
using Cosmos.EventSourcing.Testing.Utilities;

namespace Bitakora.ControlAsistencia.Colaboradores.Tests.AnularTerminacionFunction;

// El aggregate usa un stream ID compuesto (Identificacion.ToString(), "CC:79543210"), no el
// GuidAggregateId del harness -- overloads explicitos de Given/Then/And (regla 18 del
// test-writer, mismo criterio que el resto de la cadena #349-#352).
public class ComposicionAnularYTerminarVinculacionTests : CommandHandlerAsyncTest<TerminarVinculacion>
{
    private const string NumeroValido = "79543210";
    private const string StreamIdEsperado = "CC:79543210";
    private const string CodigoVinculacionVigente = "COL-001";
    private static readonly DateOnly FechaInicioVinculacionVigente = new(2026, 1, 15);
    private static readonly DateOnly FechaEfectivaErrada = new(2026, 6, 1);
    private static readonly DateOnly FechaEfectivaCorregida = new(2026, 6, 5);

    protected override ICommandHandlerAsync<TerminarVinculacion> Handler =>
        new TerminarVinculacionCommandHandler(EventStore);

    private static Identificacion IdentificacionValida() =>
        Identificacion.Crear(TipoIdentificacion.CC, NumeroValido);

    private static NombreColaborador NombreValido() =>
        NombreColaborador.Crear("Luis", "Augusto", "Barreto", null);

    // CA-2: la historia YA refleja el resultado de un AnularTerminacion previo (una request HTTP
    // anterior, ya persistida) sobre la terminacion errada. TerminarVinculacion con la fecha
    // correcta re-aplica sus propias reglas (#349) sin necesitar ninguna regla nueva -- la
    // composicion completa "corregir la fecha de terminacion" funciona en dos comandos.
    [Fact]
    public async Task TerminarVinculacion_EmiteVinculacionTerminadaConLaFechaCorregida_CuandoLaTerminacionErradaFueAnuladaAntes()
    {
        Given(StreamIdEsperado,
            new ColaboradorRegistrado(IdentificacionValida(), NombreValido()),
            new VinculacionIniciada(CodigoVinculacionVigente, FechaInicioVinculacionVigente),
            new VinculacionTerminada(FechaEfectivaErrada),
            new TerminacionAnulada());

        await WhenAsync(new TerminarVinculacion("CC", NumeroValido, FechaEfectivaCorregida));

        Then(StreamIdEsperado, new VinculacionTerminada(FechaEfectivaCorregida));
        And<ColaboradorAggregateRoot, DateOnly?>(
            StreamIdEsperado, c => c.FechaTerminacionVinculacionVigente, FechaEfectivaCorregida);
        And<ColaboradorAggregateRoot, string>(
            StreamIdEsperado, c => c.CodigoVinculacionVigente, CodigoVinculacionVigente);
    }
}
