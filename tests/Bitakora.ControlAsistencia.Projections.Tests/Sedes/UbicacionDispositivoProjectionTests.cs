// Issue #475: materializar la ubicacion vigente de un dispositivo de marcacion. Invocacion DIRECTA
// de los metodos estaticos de UbicacionDispositivoProjection (N2, MEF-ADR-0035,
// skills/projections/modelos-marten.md) -- no el DSL Given/When/Then de CommandHandlerTestBase
// (MEF-ADR-0002, testea command handlers contra el event store): aqui se testean funciones puras
// evento -> vista, sin abrir ningun stream.
//
// Create/Apply toman IEvent<DispositivoInstalado>, no el evento a secas: a diferencia de
// CategoriaDeEtiquetasProjection (issue #357, la otra receta N2 del BC), aqui la sede que
// correlaciona la vigencia NO es un campo del payload -- ambos eventos (DispositivoInstalado y
// DispositivoRetirado) solo llevan DispositivoId; la sede sale del StreamKey de la envolvente del
// evento (contexto del issue, "Notas tecnicas"). La identidad de correlacion N2
// (Identity<DispositivoInstalado>(e => e.DispositivoId)) es un dato distinto de la sede: el
// documento se identifica por DispositivoId, pero su SedeId sale de otra fuente (StreamKey).
//
// Cada assert compara contra un oraculo armado a mano (MEF-ADR-0002, no-tautologia): nunca se
// reusa la logica de Create/Apply/ShouldDelete bajo prueba para construir el valor esperado.
//
// Dato de vistaPrevia en los tests de Apply/ShouldDelete: se construye a mano, simulando el
// documento que Marten ya habria materializado -- nunca se obtiene invocando Create/Apply, para
// no encadenar el oraculo de un test con la logica bajo prueba de otro.
//
// Nota CA-5 (DispositivoRetirado sin documento existente): no hay un test dedicado -- la garantia
// es estructural, no de comportamiento. UbicacionDispositivoProjection no declara ningun
// Create(DispositivoRetirado), asi que Marten no tiene metodo que despachar y nunca crea el
// documento; un test que solo reflexionara sobre esa ausencia pasaria de una contra el stub (nada
// que implementar lo pondria en rojo), violando el principio de la fase roja de este agente --
// mismo criterio documentado en CategoriaDeEtiquetasProjectionTests (issue #357) para su propio
// CA-5.

using AwesomeAssertions;
using Bitakora.ControlAsistencia.Projections.Sedes;
using Bitakora.ControlAsistencia.ReadModels.Sedes;
using Bitakora.ControlAsistencia.Sedes.DomainEvents;
using JasperFx.Events;

namespace Bitakora.ControlAsistencia.Projections.Tests.Sedes;

public class UbicacionDispositivoProjectionTests
{
    // --- CA-1: Create nace el documento con la sede de instalacion, leida del StreamKey ---

    [Fact]
    public void Create_ProyectaLaSedeDeInstalacion_DesdeDispositivoInstalado()
    {
        var evento = new Event<DispositivoInstalado>(new DispositivoInstalado("disp-01"))
        {
            StreamKey = "s:001",
            Version = 1,
            Timestamp = DateTimeOffset.UtcNow,
        };

        var vista = UbicacionDispositivoProjection.Create(evento);

        vista.Should().Be(new UbicacionDispositivo("disp-01", "s:001"));
    }

    // --- CA-1 (correlacion N2, obligatoria para MultiStreamProjection): el mismo DispositivoId,
    // instalado en streams de DOS sedes distintas, correlaciona en el MISMO documento (misma Id) --
    // asi es exactamente como el daemon de Marten encadena eventos de streams distintos sobre un
    // documento N2 correlacionado (precedente CategoriaDeEtiquetasProjectionTests, issue #357) ---

    [Fact]
    public void Create_y_Apply_CorrelacionanPorDispositivoId_DesdeDosStreamsDeSedeDistintos()
    {
        var eventoDeSede001 = new Event<DispositivoInstalado>(new DispositivoInstalado("disp-01"))
        {
            StreamKey = "s:001",
            Version = 1,
            Timestamp = DateTimeOffset.UtcNow,
        };
        var eventoDeSede002 = new Event<DispositivoInstalado>(new DispositivoInstalado("disp-01"))
        {
            StreamKey = "s:002",
            Version = 1,
            Timestamp = DateTimeOffset.UtcNow,
        };

        var vistaTrasSede001 = UbicacionDispositivoProjection.Create(eventoDeSede001);
        var vistaTrasSede002 = UbicacionDispositivoProjection.Apply(eventoDeSede002, vistaTrasSede001);

        vistaTrasSede001.Id.Should().Be("disp-01");
        vistaTrasSede002.Id.Should().Be("disp-01");
        vistaTrasSede002.SedeId.Should().Be("s:002");
    }

    // --- CA-2: DispositivoInstalado posterior, sin retiro previo, reemplaza la sede vigente -- el
    // ultimo aplicado gana ---

    [Fact]
    public void Apply_ReemplazaLaSedeVigente_CuandoDispositivoInstaladoEnOtraSede()
    {
        var vistaPrevia = new UbicacionDispositivo("disp-01", "s:001");
        var evento = new Event<DispositivoInstalado>(new DispositivoInstalado("disp-01"))
        {
            StreamKey = "s:002",
            Version = 2,
            Timestamp = DateTimeOffset.UtcNow,
        };

        var vista = UbicacionDispositivoProjection.Apply(evento, vistaPrevia);

        vista.Should().Be(vistaPrevia with { SedeId = "s:002" });
    }

    // --- CA-3: DispositivoRetirado de la sede VIGENTE elimina el documento, sin fallback a
    // instalaciones anteriores no retiradas ---

    [Fact]
    public void ShouldDelete_EliminaElDocumento_CuandoDispositivoRetiradoDeLaSedeVigente()
    {
        var vistaPrevia = new UbicacionDispositivo("disp-01", "s:001");
        var evento = new Event<DispositivoRetirado>(new DispositivoRetirado("disp-01"))
        {
            StreamKey = "s:001",
            Version = 2,
            Timestamp = DateTimeOffset.UtcNow,
        };

        var debeEliminarse = UbicacionDispositivoProjection.ShouldDelete(evento, vistaPrevia);

        debeEliminarse.Should().BeTrue();
    }

    // --- CA-4: DispositivoRetirado de una sede DISTINTA a la vigente se ignora (limpieza de una
    // instalacion fantasma) -- el documento no se elimina ---

    [Fact]
    public void ShouldDelete_ConservaElDocumento_CuandoDispositivoRetiradoDeUnaSedeDistintaALaVigente()
    {
        var vistaPrevia = new UbicacionDispositivo("disp-01", "s:001");
        var evento = new Event<DispositivoRetirado>(new DispositivoRetirado("disp-01"))
        {
            StreamKey = "s:999", // sede distinta a la vigente ("s:001"): instalacion fantasma
            Version = 3,
            Timestamp = DateTimeOffset.UtcNow,
        };

        var debeEliminarse = UbicacionDispositivoProjection.ShouldDelete(evento, vistaPrevia);

        debeEliminarse.Should().BeFalse();
    }
}
