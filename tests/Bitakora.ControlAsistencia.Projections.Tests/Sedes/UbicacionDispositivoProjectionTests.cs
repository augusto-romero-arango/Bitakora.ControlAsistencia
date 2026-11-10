// Funciones puras evento -> vista, sin el DSL de CommandHandlerTestBase. Oraculos armados a mano,
// incluida la vistaPrevia (MEF-ADR-0002).
//
// Create/Apply/ShouldDelete toman IEvent<T>: la sede sale del StreamKey de la envolvente y el
// DispositivoId del payload, asi que la firma no puede simplificarse al evento pelado.
//
// Sin test para "retiro sin documento existente": la clase no declara Create(DispositivoRetirado) y
// el dispatcher generado no invoca ShouldDelete con snapshot nulo.

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

    // Correlacion N2 (obligatorio en MultiStreamProjection): asi encadena el daemon eventos de streams
    // distintos sobre un mismo documento.
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
