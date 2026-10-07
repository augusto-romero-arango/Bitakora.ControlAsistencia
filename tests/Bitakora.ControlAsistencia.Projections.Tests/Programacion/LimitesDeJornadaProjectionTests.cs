// Invocacion directa de los metodos estaticos de LimitesDeJornadaProjection (N1, MEF-ADR-0035).
// Oraculo armado a mano (MEF-ADR-0002): la Descripcion esperada replica el texto de
// LimitesJornada.ToString() con las etiquetas del .resx, sin ejecutar ese ToString() en el test.

using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.Projections.Programacion;
using Bitakora.ControlAsistencia.ReadModels.Programacion;
using JasperFx.Events;

namespace Bitakora.ControlAsistencia.Projections.Tests.Programacion;

public class LimitesDeJornadaProjectionTests
{
    private static readonly Guid JornadaId = Guid.Parse("019600b0-0000-7000-8000-000000000042");

    [Fact]
    public void Create_ProyectaLimitesEnMinutosYDescripcion_DesdeJornadaCreada()
    {
        var limites = LimitesJornada.Crear(
            HorasYMinutos.Crear(47, 30), HorasYMinutos.Crear(8, 0), HorasYMinutos.Crear(0, 0), 1);
        var evento = new Event<JornadaCreada>(JornadaCreada.Crear(JornadaId, limites))
        {
            StreamKey = JornadaId.ToString(),
            Version = 1,
            Timestamp = DateTimeOffset.UtcNow,
        };

        var vista = LimitesDeJornadaProjection.Create(evento);

        vista.Should().Be(new LimitesDeJornada(
            JornadaId.ToString(),
            HorasSemanalesEnMinutos: 2850,
            TopeDiarioEnMinutos: 480,
            MinimoDiarioEnMinutos: 0,
            DiasDescansoPorSemana: 1,
            Descripcion: "47 h 30 min semanales, tope diario 8 h, sin mínimo diario, 1 día de descanso por semana"));
    }

    [Fact]
    public void Apply_ReemplazaLimitesYDescripcion_CuandoLimitesJornadaModificados()
    {
        var previa = new LimitesDeJornada(
            JornadaId.ToString(), 2850, 480, 0, 1,
            "47 h 30 min semanales, tope diario 8 h, sin mínimo diario, 1 día de descanso por semana");
        var nuevos = LimitesJornada.Crear(
            HorasYMinutos.Crear(42, 0), HorasYMinutos.Crear(8, 0), HorasYMinutos.Crear(4, 0), 2);

        var vista = LimitesDeJornadaProjection.Apply(
            LimitesJornadaModificados.Crear(JornadaId, nuevos), previa);

        vista.Should().Be(new LimitesDeJornada(
            JornadaId.ToString(), 2520, 480, 240, 2,
            "42 h semanales, tope diario 8 h, mínimo diario 4 h, 2 días de descanso por semana"));
    }
}
