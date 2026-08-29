// Invocacion DIRECTA de los metodos estaticos de UbicacionDispositivoProjection -- funciones puras
// evento -> vista, sin abrir ningun stream (no aplica el DSL Given/When/Then de
// CommandHandlerTestBase, que testea command handlers contra el event store).
//
// Create/Apply/ShouldDelete toman IEvent<T>, no el evento a secas: la sede que correlaciona la
// vigencia NO viaja en el payload de ninguno de los dos eventos -- sale del StreamKey de la
// envolvente. La identidad de correlacion N2 (DispositivoId) si sale del payload: son dos datos
// distintos y por eso la firma no puede simplificarse al evento pelado.
//
// Cada oraculo se arma a mano, incluido el de vistaPrevia (el documento que Marten ya habria
// materializado): nunca se construye invocando la logica bajo prueba (MEF-ADR-0002, no-tautologia).
//
// Sin test para "retiro sin documento existente": esa garantia es estructural -- la clase no declara
// ningun Create(DispositivoRetirado), y el dispatcher generado ni siquiera invoca ShouldDelete con
// snapshot nulo. Un test que solo reflexionara sobre esa ausencia seria tautologico.

using AwesomeAssertions;
using Bitakora.ControlAsistencia.Projections.Sedes;
using Bitakora.ControlAsistencia.ReadModels.Sedes;
using Bitakora.ControlAsistencia.Sedes.DomainEvents;
using JasperFx.Events;

namespace Bitakora.ControlAsistencia.Projections.Tests.Sedes;

public class UbicacionDispositivoProjectionTests
{
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

    // Correlacion N2 (test obligatorio para MultiStreamProjection): Create con el primer evento y
    // Apply con el segundo sobre la vista que dejo el primero es exactamente como el daemon encadena
    // eventos de streams distintos sobre un mismo documento correlacionado.

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
