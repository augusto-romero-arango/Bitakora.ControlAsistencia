// Issue #496: primera proyeccion concreta del dominio Programacion. Invocacion DIRECTA de los
// metodos estaticos de FichaTurnoProjection (N1, MEF-ADR-0035) -- no el DSL Given/When/Then de
// CommandHandlerTestBase (MEF-ADR-0002, testea command handlers contra el event store): aqui se
// testea una funcion pura evento -> vista, sin abrir ningun stream.
//
// Cada assert compara contra un oraculo armado a mano (MEF-ADR-0002, no-tautologia): nunca se
// reusa la logica de Create bajo prueba para construir el valor esperado. El texto de Descripcion/
// FranjaFicha.Descripcion se arma a mano replicando el formato ya VERIFICADO por lectura de
// codigo fuente de FranjaOrdinaria.ToString()/SubFranja.ToString() (Programacion.DomainEvents) --
// "(06:00-14:00)[Descansos:(10:00-10:15)][Extras:(13:00-13:30)][sede:Sede Centro]", con el label
// "sede" en minuscula (FranjaOrdinariaMensajes.resx) -- nunca ejecutando ese ToString() desde el
// test para no acoplar el oraculo a una llamada de produccion.
//
// CA-1 exige Nombre/EsDescanso/HorarioResumido y las franjas completas (descansos/extras
// contenidos, sede prearmada); CA-2 exige EsDescanso=true y Franjas vacia para la variante
// CrearDescanso. Ningun CA fija el algoritmo exacto de HorarioResumido/Descripcion (MEF-ADR-0041:
// "HorarioResumido... espejo del patron de TurnoVigente", "Descripcion -- desambiguacion humana");
// el diseno elegido aqui -- HorarioResumido = rango de horas corto ("06:00-14:00"), Descripcion =
// el detalle completo por franja (mismo texto que FranjaOrdinaria.ToString() produce), "Descanso"
// para ambos campos en la variante sin franjas -- es la decision de este test-writer, documentada
// en el resumen del stage bajo "Desviaciones del plan del planner" (regla 6 del agente).

using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.Projections.Programacion;
using Bitakora.ControlAsistencia.ReadModels.Programacion;
using JasperFx.Events;

namespace Bitakora.ControlAsistencia.Projections.Tests.Programacion;

public class FichaTurnoProjectionTests
{
    // --- CA-1: Create proyecta un turno normal con Nombre/EsDescanso=false/HorarioResumido y las
    // franjas completas (descansos/extras contenidos, sede prearmada por franja) ---

    // Create toma IEvent<TurnoCreado>, no el evento a secas: la identidad del documento es el
    // StreamKey del stream de CatalogoTurnos (turnoId.ToString()), nunca recomputada a mano desde
    // el payload (skills/projections/modelos-marten.md).
    [Fact]
    public void Create_ProyectaTurnoConNombreYFranjaCompleta_DesdeTurnoCreado()
    {
        var turnoId = Guid.Parse("019600b0-0000-7000-8000-000000000001");
        var turnoCreado = TurnoCreado.Crear(
            turnoId,
            "Turno Manana",
            [new DatosFranja(
                new TimeOnly(6, 0), new TimeOnly(14, 0),
                [(new TimeOnly(10, 0), new TimeOnly(10, 15))],
                [(new TimeOnly(13, 0), new TimeOnly(13, 30))],
                new SedeProgramada("sede-01", "Sede Centro"))]);

        var evento = new Event<TurnoCreado>(turnoCreado)
        {
            StreamKey = turnoId.ToString(),
            Version = 1,
            Timestamp = DateTimeOffset.UtcNow,
        };

        var vista = FichaTurnoProjection.Create(evento);

        var franjaEsperada = new FranjaFicha(
            new TimeOnly(6, 0), new TimeOnly(14, 0), 0,
            [new SubFranjaFicha(new TimeOnly(10, 0), new TimeOnly(10, 15), 0, 0)],
            [new SubFranjaFicha(new TimeOnly(13, 0), new TimeOnly(13, 30), 0, 0)],
            "sede-01", "Sede Centro",
            "(06:00-14:00)[Descansos:(10:00-10:15)][Extras:(13:00-13:30)][sede:Sede Centro]");

        vista.Should().Be(new FichaTurno(
            turnoId.ToString(),
            "Turno Manana",
            false,
            "06:00-14:00",
            [franjaEsperada],
            "(06:00-14:00)[Descansos:(10:00-10:15)][Extras:(13:00-13:30)][sede:Sede Centro]"));
    }

    // --- CA-2: Create proyecta la variante descanso (factory CrearDescanso) con EsDescanso=true y
    // sin franjas ---

    [Fact]
    public void Create_ProyectaTurnoDeDescanso_DesdeTurnoCreadoDeDescanso()
    {
        var turnoId = Guid.Parse("019600b0-0000-7000-8000-000000000002");
        var turnoCreado = TurnoCreado.CrearDescanso(turnoId, "Descanso Dominical");

        var evento = new Event<TurnoCreado>(turnoCreado)
        {
            StreamKey = turnoId.ToString(),
            Version = 1,
            Timestamp = DateTimeOffset.UtcNow,
        };

        var vista = FichaTurnoProjection.Create(evento);

        vista.Should().Be(new FichaTurno(
            turnoId.ToString(),
            "Descanso Dominical",
            true,
            "Descanso",
            [],
            "Descanso"));
    }
}
