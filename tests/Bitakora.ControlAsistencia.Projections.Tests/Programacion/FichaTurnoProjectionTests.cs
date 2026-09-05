// Invocacion DIRECTA de los metodos estaticos de FichaTurnoProjection (N1, MEF-ADR-0035) -- no el
// DSL Given/When/Then de CommandHandlerTestBase, que testea command handlers contra el event store:
// aqui se prueba una funcion pura evento -> vista, sin abrir ningun stream.
//
// El oraculo se arma a mano (MEF-ADR-0002, no-tautologia): el texto esperado de Descripcion replica
// el formato de FranjaOrdinaria.ToString()/SubFranja.ToString() verificado por lectura del codigo
// fuente, nunca ejecutando ese ToString() desde el test. Las FranjaFicha/SubFranjaFicha esperadas se
// arman con su constructor posicional, nunca reusando FichaTurnoProjection.MapearFranja/MapearSubFranja
// (el mapeo privado del SUT).
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
    // La identidad del documento es el StreamKey del stream de CatalogoTurnos, nunca recomputada
    // desde el payload.
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
            "(06:00-14:00)[Descansos:(10:00-10:15)][Extras:(13:00-13:30)][sede:Sede Centro]",
            Completo: true));
    }

    // HorarioResumido y Descripcion unen los rangos ordenados por HoraInicio, no en el orden en que
    // el evento los trae (vista para leer el dia -- MEF-ADR-0041): el arrange llega desordenado a
    // proposito.
    [Fact]
    public void Create_UneLosRangosOrdenadosPorHoraInicio_CuandoElTurnoTieneVariasFranjas()
    {
        var turnoId = Guid.Parse("019600b0-0000-7000-8000-000000000003");
        var turnoCreado = TurnoCreado.Crear(
            turnoId,
            "Turno Partido",
            [
                new DatosFranja(new TimeOnly(14, 0), new TimeOnly(18, 0), [], [], null),
                new DatosFranja(new TimeOnly(6, 0), new TimeOnly(10, 0), [], [], null)
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
        vista.Completo.Should().BeTrue();
        vista.Franjas.Should().BeEquivalentTo([
            new FranjaFicha(new TimeOnly(6, 0), new TimeOnly(10, 0), 0, [], [], null, null, "(06:00-10:00)"),
            new FranjaFicha(new TimeOnly(14, 0), new TimeOnly(18, 0), 0, [], [], null, null, "(14:00-18:00)")
        ], opciones => opciones.WithStrictOrdering());
    }

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

        // Un descanso siempre es programable.
        vista.Should().BeEquivalentTo(new FichaTurno(
            turnoId.ToString(),
            "Descanso Dominical",
            true,
            "Descanso",
            [],
            "Descanso",
            Completo: true));
    }

    // Un turno recien nacido (CA-ADR-0033, diseno por pasos) sin franjas y sin marca de descanso NO
    // es un descanso: es un turno incompleto, todavia no programable.
    [Fact]
    public void Create_ProyectaTurnoIncompleto_DesdeTurnoCreadoSinFranjasNiDescanso()
    {
        var turnoId = Guid.Parse("019600b0-0000-7000-8000-000000000005");
        var turnoCreado = TurnoCreado.Crear(turnoId, "Turno Vacio", []);

        var evento = new Event<TurnoCreado>(turnoCreado)
        {
            StreamKey = turnoId.ToString(),
            Version = 1,
            Timestamp = DateTimeOffset.UtcNow,
        };

        var vista = FichaTurnoProjection.Create(evento);

        vista.Should().BeEquivalentTo(new FichaTurno(
            turnoId.ToString(),
            "Turno Vacio",
            false,
            "Sin franjas",
            [],
            "Sin franjas",
            Completo: false));
    }

    // Un turno sin ficha materializada no necesita test propio: sin Create(TurnoRetirado), Marten
    // no llega a invocar ShouldDelete cuando el stream no tiene documento previo.
    [Fact]
    public void ShouldDelete_BorraLaFicha_CuandoTurnoRetirado()
    {
        var turnoRetirado = TurnoRetirado.Crear(Guid.Parse("019600b0-0000-7000-8000-000000000004"));

        var debeBorrarse = FichaTurnoProjection.ShouldDelete(turnoRetirado);

        debeBorrarse.Should().BeTrue();
    }

    [Fact]
    public void Apply_AgregaLaFranjaYCompletaLaFicha_CuandoFranjaAgregadaSobreFichaSinFranjas()
    {
        var turnoId = Guid.NewGuid();
        var fichaPrevia = new FichaTurno(
            turnoId.ToString(), "Turno Manana", false, "Sin franjas", [], "Sin franjas", Completo: false);

        var franja = FranjaOrdinaria.Crear(
            new TimeOnly(6, 0), new TimeOnly(14, 0),
            descansos: [SubFranja.Crear(new TimeOnly(10, 0), new TimeOnly(10, 15))],
            extras: [SubFranja.Crear(new TimeOnly(13, 0), new TimeOnly(13, 30))],
            sede: new SedeProgramada("sede-01", "Sede Centro"));
        var evento = FranjaAgregada.Crear(turnoId, franja);

        var vista = FichaTurnoProjection.Apply(evento, fichaPrevia);

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
            "(06:00-14:00)[Descansos:(10:00-10:15)][Extras:(13:00-13:30)][sede:Sede Centro]",
            Completo: true));
    }

    // Vista para leer el dia, no el orden de diseno (MEF-ADR-0041): una franja agregada con
    // inicio anterior a las que ya estaban queda PRIMERA.
    [Fact]
    public void Apply_OrdenaLasFranjasPorHoraInicio_CuandoSeAgregaUnaFranjaConInicioAnterior()
    {
        var turnoId = Guid.NewGuid();
        var franjaTarde = new FranjaFicha(
            new TimeOnly(14, 0), new TimeOnly(22, 0), 0, [], [], null, null, "(14:00-22:00)");
        var fichaPrevia = new FichaTurno(
            turnoId.ToString(), "Turno Partido", false,
            "14:00-22:00", [franjaTarde], "(14:00-22:00)", Completo: true);

        var franjaManana = FranjaOrdinaria.Crear(new TimeOnly(6, 0), new TimeOnly(14, 0));
        var evento = FranjaAgregada.Crear(turnoId, franjaManana);

        var vista = FichaTurnoProjection.Apply(evento, fichaPrevia);

        vista.HorarioResumido.Should().Be("06:00-14:00, 14:00-22:00");
        vista.Franjas.Should().BeEquivalentTo([
            new FranjaFicha(new TimeOnly(6, 0), new TimeOnly(14, 0), 0, [], [], null, null, "(06:00-14:00)"),
            franjaTarde
        ], opciones => opciones.WithStrictOrdering());
    }

    [Fact]
    public void Apply_QuitaLaFranjaCuyaHoraInicioCoincide_CuandoFranjaQuitada()
    {
        var turnoId = Guid.NewGuid();
        var franjaManana = new FranjaFicha(
            new TimeOnly(6, 0), new TimeOnly(14, 0), 0, [], [], null, null, "(06:00-14:00)");
        var franjaTarde = new FranjaFicha(
            new TimeOnly(14, 0), new TimeOnly(22, 0), 0, [], [], null, null, "(14:00-22:00)");
        var fichaPrevia = new FichaTurno(
            turnoId.ToString(), "Turno Partido", false,
            "06:00-14:00, 14:00-22:00", [franjaManana, franjaTarde],
            "(06:00-14:00), (14:00-22:00)", Completo: true);

        var evento = FranjaQuitada.Crear(turnoId, FranjaOrdinaria.Crear(new TimeOnly(6, 0), new TimeOnly(14, 0)));

        var vista = FichaTurnoProjection.Apply(evento, fichaPrevia);

        vista.Completo.Should().BeTrue();
        vista.HorarioResumido.Should().Be("14:00-22:00");
        vista.Franjas.Should().BeEquivalentTo([franjaTarde]);
    }

    [Fact]
    public void Apply_DejaLaFichaIncompleta_CuandoFranjaQuitadaEraLaUltima()
    {
        var turnoId = Guid.NewGuid();
        var franja = new FranjaFicha(
            new TimeOnly(6, 0), new TimeOnly(14, 0), 0, [], [], null, null, "(06:00-14:00)");
        var fichaPrevia = new FichaTurno(
            turnoId.ToString(), "Turno Manana", false, "06:00-14:00", [franja], "(06:00-14:00)", Completo: true);

        var evento = FranjaQuitada.Crear(turnoId, FranjaOrdinaria.Crear(new TimeOnly(6, 0), new TimeOnly(14, 0)));

        var vista = FichaTurnoProjection.Apply(evento, fichaPrevia);

        vista.Should().BeEquivalentTo(new FichaTurno(
            turnoId.ToString(), "Turno Manana", false, "Sin franjas", [], "Sin franjas", Completo: false));
    }

    // Los eventos de sub-franja y de sede traen la franja CONTENEDORA resultante -- la clave de
    // reemplazo es Franja.ToDetalle().HoraInicio -- y dejan las demas franjas de la ficha intactas.

    [Fact]
    public void Apply_ReemplazaSoloLaFranjaConElDescansoNuevo_CuandoDescansoAgregado()
    {
        var turnoId = Guid.NewGuid();
        var franjaManana = new FranjaFicha(
            new TimeOnly(6, 0), new TimeOnly(14, 0), 0, [], [], null, null, "(06:00-14:00)");
        var franjaTarde = new FranjaFicha(
            new TimeOnly(14, 0), new TimeOnly(22, 0), 0, [], [], null, null, "(14:00-22:00)");
        var fichaPrevia = new FichaTurno(
            turnoId.ToString(), "Turno Partido", false,
            "06:00-14:00, 14:00-22:00", [franjaManana, franjaTarde],
            "(06:00-14:00), (14:00-22:00)", Completo: true);

        var franjaResultante = FranjaOrdinaria.Crear(
            new TimeOnly(6, 0), new TimeOnly(14, 0),
            descansos: [SubFranja.Crear(new TimeOnly(10, 0), new TimeOnly(10, 15))]);
        var evento = DescansoAgregado.Crear(turnoId, franjaResultante);

        var vista = FichaTurnoProjection.Apply(evento, fichaPrevia);

        var franjaMananaEsperada = franjaManana with
        {
            Descansos = [new SubFranjaFicha(new TimeOnly(10, 0), new TimeOnly(10, 15), 0, 0)],
            Descripcion = "(06:00-14:00)[Descansos:(10:00-10:15)]",
        };
        vista.Franjas.Should().BeEquivalentTo([franjaMananaEsperada, franjaTarde]);
        vista.Descripcion.Should().Be("(06:00-14:00)[Descansos:(10:00-10:15)], (14:00-22:00)");
    }

    [Fact]
    public void Apply_ReemplazaSoloLaFranjaConElExtraNuevo_CuandoExtraAgregado()
    {
        var turnoId = Guid.NewGuid();
        var franjaManana = new FranjaFicha(
            new TimeOnly(6, 0), new TimeOnly(14, 0), 0, [], [], null, null, "(06:00-14:00)");
        var franjaTarde = new FranjaFicha(
            new TimeOnly(14, 0), new TimeOnly(22, 0), 0, [], [], null, null, "(14:00-22:00)");
        var fichaPrevia = new FichaTurno(
            turnoId.ToString(), "Turno Partido", false,
            "06:00-14:00, 14:00-22:00", [franjaManana, franjaTarde],
            "(06:00-14:00), (14:00-22:00)", Completo: true);

        var franjaResultante = FranjaOrdinaria.Crear(
            new TimeOnly(6, 0), new TimeOnly(14, 0),
            extras: [SubFranja.Crear(new TimeOnly(13, 0), new TimeOnly(13, 30))]);
        var evento = ExtraAgregado.Crear(turnoId, franjaResultante);

        var vista = FichaTurnoProjection.Apply(evento, fichaPrevia);

        var franjaMananaEsperada = franjaManana with
        {
            Extras = [new SubFranjaFicha(new TimeOnly(13, 0), new TimeOnly(13, 30), 0, 0)],
            Descripcion = "(06:00-14:00)[Extras:(13:00-13:30)]",
        };
        vista.Franjas.Should().BeEquivalentTo([franjaMananaEsperada, franjaTarde]);
        vista.Descripcion.Should().Be("(06:00-14:00)[Extras:(13:00-13:30)], (14:00-22:00)");
    }

    [Fact]
    public void Apply_ReemplazaSoloLaFranjaSinElDescansoQuitado_CuandoDescansoQuitado()
    {
        var turnoId = Guid.NewGuid();
        var franjaMananaConDescanso = new FranjaFicha(
            new TimeOnly(6, 0), new TimeOnly(14, 0), 0,
            [new SubFranjaFicha(new TimeOnly(10, 0), new TimeOnly(10, 15), 0, 0)],
            [], null, null, "(06:00-14:00)[Descansos:(10:00-10:15)]");
        var franjaTarde = new FranjaFicha(
            new TimeOnly(14, 0), new TimeOnly(22, 0), 0, [], [], null, null, "(14:00-22:00)");
        var fichaPrevia = new FichaTurno(
            turnoId.ToString(), "Turno Partido", false,
            "06:00-14:00, 14:00-22:00", [franjaMananaConDescanso, franjaTarde],
            "(06:00-14:00)[Descansos:(10:00-10:15)], (14:00-22:00)", Completo: true);

        // Sin descansos: el evento trae la franja contenedora resultante, ya sin la hija.
        var franjaResultante = FranjaOrdinaria.Crear(new TimeOnly(6, 0), new TimeOnly(14, 0));
        var evento = DescansoQuitado.Crear(turnoId, franjaResultante);

        var vista = FichaTurnoProjection.Apply(evento, fichaPrevia);

        var franjaMananaEsperada = franjaMananaConDescanso with { Descansos = [], Descripcion = "(06:00-14:00)" };
        vista.Franjas.Should().BeEquivalentTo([franjaMananaEsperada, franjaTarde]);
        vista.Descripcion.Should().Be("(06:00-14:00), (14:00-22:00)");
    }

    [Fact]
    public void Apply_ReemplazaSoloLaFranjaSinElExtraQuitado_CuandoExtraQuitado()
    {
        var turnoId = Guid.NewGuid();
        var franjaMananaConExtra = new FranjaFicha(
            new TimeOnly(6, 0), new TimeOnly(14, 0), 0,
            [], [new SubFranjaFicha(new TimeOnly(13, 0), new TimeOnly(13, 30), 0, 0)],
            null, null, "(06:00-14:00)[Extras:(13:00-13:30)]");
        var franjaTarde = new FranjaFicha(
            new TimeOnly(14, 0), new TimeOnly(22, 0), 0, [], [], null, null, "(14:00-22:00)");
        var fichaPrevia = new FichaTurno(
            turnoId.ToString(), "Turno Partido", false,
            "06:00-14:00, 14:00-22:00", [franjaMananaConExtra, franjaTarde],
            "(06:00-14:00)[Extras:(13:00-13:30)], (14:00-22:00)", Completo: true);

        var franjaResultante = FranjaOrdinaria.Crear(new TimeOnly(6, 0), new TimeOnly(14, 0));
        var evento = ExtraQuitado.Crear(turnoId, franjaResultante);

        var vista = FichaTurnoProjection.Apply(evento, fichaPrevia);

        var franjaMananaEsperada = franjaMananaConExtra with { Extras = [], Descripcion = "(06:00-14:00)" };
        vista.Franjas.Should().BeEquivalentTo([franjaMananaEsperada, franjaTarde]);
        vista.Descripcion.Should().Be("(06:00-14:00), (14:00-22:00)");
    }

    [Fact]
    public void Apply_AsignaLaSedeALaFranja_CuandoSedeDeFranjaAsignada()
    {
        var turnoId = Guid.NewGuid();
        var franjaSinSede = new FranjaFicha(
            new TimeOnly(6, 0), new TimeOnly(14, 0), 0, [], [], null, null, "(06:00-14:00)");
        var fichaPrevia = new FichaTurno(
            turnoId.ToString(), "Turno Manana", false, "06:00-14:00", [franjaSinSede], "(06:00-14:00)",
            Completo: true);

        var franjaResultante = FranjaOrdinaria.Crear(
            new TimeOnly(6, 0), new TimeOnly(14, 0), sede: new SedeProgramada("sede-01", "Sede Centro"));
        var evento = SedeDeFranjaAsignada.Crear(turnoId, franjaResultante);

        var vista = FichaTurnoProjection.Apply(evento, fichaPrevia);

        var franjaEsperada = franjaSinSede with
        {
            SedeId = "sede-01",
            NombreSede = "Sede Centro",
            Descripcion = "(06:00-14:00)[sede:Sede Centro]",
        };
        vista.Franjas.Should().BeEquivalentTo([franjaEsperada]);
        vista.Descripcion.Should().Be("(06:00-14:00)[sede:Sede Centro]");
    }

    [Fact]
    public void Apply_RetiraLaSedeDeLaFranja_CuandoSedeDeFranjaRetirada()
    {
        var turnoId = Guid.NewGuid();
        var franjaConSede = new FranjaFicha(
            new TimeOnly(6, 0), new TimeOnly(14, 0), 0, [], [],
            "sede-01", "Sede Centro", "(06:00-14:00)[sede:Sede Centro]");
        var fichaPrevia = new FichaTurno(
            turnoId.ToString(), "Turno Manana", false,
            "06:00-14:00", [franjaConSede], "(06:00-14:00)[sede:Sede Centro]", Completo: true);

        var franjaResultante = FranjaOrdinaria.Crear(new TimeOnly(6, 0), new TimeOnly(14, 0));
        var evento = SedeDeFranjaRetirada.Crear(turnoId, franjaResultante);

        var vista = FichaTurnoProjection.Apply(evento, fichaPrevia);

        var franjaEsperada = franjaConSede with { SedeId = null, NombreSede = null, Descripcion = "(06:00-14:00)" };
        vista.Franjas.Should().BeEquivalentTo([franjaEsperada]);
        vista.Descripcion.Should().Be("(06:00-14:00)");
    }
}
