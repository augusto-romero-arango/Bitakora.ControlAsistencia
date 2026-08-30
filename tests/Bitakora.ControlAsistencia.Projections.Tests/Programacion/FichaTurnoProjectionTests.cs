// Invocacion DIRECTA de los metodos estaticos de FichaTurnoProjection (N1, MEF-ADR-0035) -- no el
// DSL Given/When/Then de CommandHandlerTestBase, que testea command handlers contra el event store:
// aqui se prueba una funcion pura evento -> vista, sin abrir ningun stream.
//
// El oraculo se arma a mano (MEF-ADR-0002, no-tautologia): el texto esperado de Descripcion replica
// el formato de FranjaOrdinaria.ToString()/SubFranja.ToString() verificado por lectura del codigo
// fuente, nunca ejecutando ese ToString() desde el test.
//
// BeEquivalentTo, no Be: FichaTurno es un record plano sin igualdad por valor sobre sus colecciones
// (MEF-ADR-0035) -- Be compararia Franjas por referencia y fallaria con valores identicos.

using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.Projections.Programacion;
using Bitakora.ControlAsistencia.ReadModels.Programacion;
using JasperFx.Events;

namespace Bitakora.ControlAsistencia.Projections.Tests.Programacion;

public class FichaTurnoProjectionTests
{
    // CA-1: turno con franjas -- Nombre, EsDescanso=false, HorarioResumido y la franja completa
    // (descansos/extras contenidos, sede prearmada). La identidad del documento es el StreamKey del
    // stream de CatalogoTurnos, nunca recomputada desde el payload.
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

        vista.Should().BeEquivalentTo(new FichaTurno(
            turnoId.ToString(),
            "Turno Manana",
            false,
            "06:00-14:00",
            [franjaEsperada],
            "(06:00-14:00)[Descansos:(10:00-10:15)][Extras:(13:00-13:30)][sede:Sede Centro]"));
    }

    // Caso borde de CA-1 sin test propio hasta la revision: con varias franjas, HorarioResumido y
    // Descripcion unen los rangos en el orden de las franjas del evento.
    [Fact]
    public void Create_UneLosRangosDeTodasLasFranjas_CuandoElTurnoTieneVariasFranjas()
    {
        var turnoId = Guid.Parse("019600b0-0000-7000-8000-000000000003");
        var turnoCreado = TurnoCreado.Crear(
            turnoId,
            "Turno Partido",
            [
                new DatosFranja(new TimeOnly(6, 0), new TimeOnly(10, 0), [], [], null),
                new DatosFranja(new TimeOnly(14, 0), new TimeOnly(18, 0), [], [], null)
            ]);

        var evento = new Event<TurnoCreado>(turnoCreado)
        {
            StreamKey = turnoId.ToString(),
            Version = 1,
            Timestamp = DateTimeOffset.UtcNow,
        };

        var vista = FichaTurnoProjection.Create(evento);

        vista.HorarioResumido.Should().Be("06:00-10:00, 14:00-18:00");
        vista.Descripcion.Should().Be("(06:00-10:00), (14:00-18:00)");
        vista.Franjas.Should().BeEquivalentTo([
            new FranjaFicha(new TimeOnly(6, 0), new TimeOnly(10, 0), 0, [], [], null, null, "(06:00-10:00)"),
            new FranjaFicha(new TimeOnly(14, 0), new TimeOnly(18, 0), 0, [], [], null, null, "(14:00-18:00)")
        ], opciones => opciones.WithStrictOrdering());
    }

    // CA-2: variante descanso (factory CrearDescanso) -- EsDescanso=true y sin franjas.
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

        vista.Should().BeEquivalentTo(new FichaTurno(
            turnoId.ToString(),
            "Descanso Dominical",
            true,
            "Descanso",
            [],
            "Descanso"));
    }

    // Issue #501 CA-1: TurnoRetirado sobre un turno materializado borra su FichaTurno de la vista
    // -- se borra (no se marca): la auditoria vive en el event store, y el nombre queda libre para
    // el patron "modificar = retirar + crear" y la invariante de nombre unico que verifica #497
    // contra esta misma vista. Estilo canonico del Skill (modelos-marten.md): ShouldDelete(TEvento)
    // a secas, sin IEvent<T> ni TView -- el borrado no depende de ningun dato de metadata del
    // evento ni del estado previo de la ficha.
    //
    // "Turno no materializado -> sin efecto" (nota de la capa de tests del issue) no tiene test
    // propio a este nivel: es consecuencia intrinseca del lifecycle de SingleStreamProjection sin
    // Create(TurnoRetirado) -- si Marten no tiene ya un documento para ese stream, no hay Apply ni
    // ShouldDelete que invocar antes. No es logica de FichaTurnoProjection que quepa testear aqui.
    [Fact]
    public void ShouldDelete_BorraLaFicha_CuandoTurnoRetirado()
    {
        var turnoRetirado = TurnoRetirado.Crear(Guid.Parse("019600b0-0000-7000-8000-000000000004"));

        var debeBorrarse = FichaTurnoProjection.ShouldDelete(turnoRetirado);

        debeBorrarse.Should().BeTrue();
    }
}
