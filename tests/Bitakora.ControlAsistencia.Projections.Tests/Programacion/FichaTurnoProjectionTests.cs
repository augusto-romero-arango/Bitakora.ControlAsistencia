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

    private static Event<TurnoCreado> Evento(TurnoCreado turnoCreado) =>
        new(turnoCreado)
        {
            StreamKey = turnoCreado.TurnoId.ToString(),
            Version = 1,
            Timestamp = DateTimeOffset.UtcNow,
        };

    private static FichaTurno FichaCon(string[] rangos, VentanaDeEntradaFicha? ventana)
    {
        var franjas = rangos
            .Select(r => r.Split('-'))
            .Select(p => (Ini: TimeOnly.Parse(p[0]), Fin: TimeOnly.Parse(p[1])))
            .Select(r => new FranjaFicha(
                r.Ini, r.Fin, 0, [], [], null, null, $"({r.Ini:HH\\:mm}-{r.Fin:HH\\:mm})"))
            .ToList();
        return new FichaTurno(
            Guid.NewGuid().ToString(), "Turno", false,
            string.Join(", ", rangos), franjas,
            string.Join(", ", franjas.Select(f => f.Descripcion)), Completo: true, VentanaDeEntrada: ventana);
    }

    [Fact]
    public void Create_DejaVentanaDeEntradaNula_CuandoTurnoDeDescanso()
    {
        var vista = FichaTurnoProjection.Create(Evento(TurnoCreado.CrearDescanso(Guid.NewGuid(), "Descanso")));

        vista.VentanaDeEntrada.Should().BeNull();
    }

    [Fact]
    public void Create_DejaVentanaDeEntradaNula_CuandoTurnoSinFranjas()
    {
        var vista = FichaTurnoProjection.Create(Evento(TurnoCreado.Crear(Guid.NewGuid(), "Vacio", [])));

        vista.VentanaDeEntrada.Should().BeNull();
    }

    [Fact]
    public void Create_DejaVentanaDeEntradaNula_CuandoTurnoConFranjas()
    {
        var vista = FichaTurnoProjection.Create(Evento(TurnoCreado.Crear(
            Guid.NewGuid(), "Manana", [new DatosFranja(new TimeOnly(7, 0), new TimeOnly(16, 0), [], [], null)])));

        vista.VentanaDeEntrada.Should().BeNull();
    }

    [Fact]
    public void Apply_ProyectaVentanaDeTresHoras_CuandoVentanaDeEntradaModificadaConTresHoras()
    {
        var ficha = FichaCon(["07:00-16:00"], null);
        var evento = VentanaDeEntradaModificada.Crear(Guid.NewGuid(), HorasYMinutos.Crear(3, 0));

        var vista = FichaTurnoProjection.Apply(evento, ficha);

        vista.VentanaDeEntrada.Should().Be(
            new VentanaDeEntradaFicha(new TimeOnly(7, 0), new TimeOnly(10, 0), "3 h"));
    }

    [Fact]
    public void Apply_ProyectaVentanaConMinutos_CuandoVentanaDeEntradaModificadaConDosHorasTreintaMinutos()
    {
        var ficha = FichaCon(["07:00-16:00"], null);
        var evento = VentanaDeEntradaModificada.Crear(Guid.NewGuid(), HorasYMinutos.Crear(2, 30));

        var vista = FichaTurnoProjection.Apply(evento, ficha);

        vista.VentanaDeEntrada.Should().Be(
            new VentanaDeEntradaFicha(new TimeOnly(7, 0), new TimeOnly(9, 30), "2 h 30 min"));
    }

    [Fact]
    public void Apply_DejaVentanaNula_CuandoVentanaDeEntradaModificadaConAmplitudCero()
    {
        var ficha = FichaCon(["07:00-16:00"],
            new VentanaDeEntradaFicha(new TimeOnly(7, 0), new TimeOnly(10, 0), "3 h"));
        var evento = VentanaDeEntradaModificada.Crear(Guid.NewGuid(), HorasYMinutos.Crear(0, 0));

        var vista = FichaTurnoProjection.Apply(evento, ficha);

        vista.VentanaDeEntrada.Should().BeNull();
    }

    [Fact]
    public void Apply_DejaVentanaNulaSinLanzar_CuandoVentanaDeEntradaModificadaSobreFichaSinFranjas()
    {
        var ficha = new FichaTurno(
            Guid.NewGuid().ToString(), "Vacio", false, "Sin franjas", [], "Sin franjas", Completo: false);
        var evento = VentanaDeEntradaModificada.Crear(Guid.NewGuid(), HorasYMinutos.Crear(3, 0));

        var vista = FichaTurnoProjection.Apply(evento, ficha);

        vista.VentanaDeEntrada.Should().BeNull();
    }

    [Fact]
    public void Apply_MueveLaAperturaConservandoLaAmplitud_CuandoFranjaAgregadaAntesDeLaPrimera()
    {
        var ficha = FichaCon(["07:00-16:00"],
            new VentanaDeEntradaFicha(new TimeOnly(7, 0), new TimeOnly(10, 0), "3 h"));
        var evento = FranjaAgregada.Crear(
            Guid.NewGuid(), FranjaOrdinaria.Crear(new TimeOnly(5, 0), new TimeOnly(6, 30)));

        var vista = FichaTurnoProjection.Apply(evento, ficha);

        vista.VentanaDeEntrada.Should().Be(
            new VentanaDeEntradaFicha(new TimeOnly(5, 0), new TimeOnly(8, 0), "3 h"));
    }

    [Fact]
    public void Apply_MueveLaAperturaALaSiguienteFranja_CuandoFranjaQuitadaEraLaPrimera()
    {
        var ficha = FichaCon(["06:00-10:00", "14:00-18:00"],
            new VentanaDeEntradaFicha(new TimeOnly(6, 0), new TimeOnly(8, 0), "2 h"));
        var evento = FranjaQuitada.Crear(
            Guid.NewGuid(), FranjaOrdinaria.Crear(new TimeOnly(6, 0), new TimeOnly(10, 0)));

        var vista = FichaTurnoProjection.Apply(evento, ficha);

        vista.VentanaDeEntrada.Should().Be(
            new VentanaDeEntradaFicha(new TimeOnly(14, 0), new TimeOnly(16, 0), "2 h"));
    }

    [Fact]
    public void Apply_ConservaLaVentana_CuandoDescansoAgregado()
    {
        var ventana = new VentanaDeEntradaFicha(new TimeOnly(7, 0), new TimeOnly(10, 0), "3 h");
        var ficha = FichaCon(["07:00-16:00"], ventana);
        var evento = DescansoAgregado.Crear(Guid.NewGuid(), FranjaOrdinaria.Crear(
            new TimeOnly(7, 0), new TimeOnly(16, 0),
            descansos: [SubFranja.Crear(new TimeOnly(12, 0), new TimeOnly(12, 30))]));

        var vista = FichaTurnoProjection.Apply(evento, ficha);

        vista.VentanaDeEntrada.Should().Be(ventana);
    }

    [Fact]
    public void Apply_ConservaLaVentana_CuandoExtraAgregado()
    {
        var ventana = new VentanaDeEntradaFicha(new TimeOnly(7, 0), new TimeOnly(10, 0), "3 h");
        var ficha = FichaCon(["07:00-16:00"], ventana);
        var evento = ExtraAgregado.Crear(Guid.NewGuid(), FranjaOrdinaria.Crear(
            new TimeOnly(7, 0), new TimeOnly(16, 0),
            extras: [SubFranja.Crear(new TimeOnly(15, 0), new TimeOnly(15, 30))]));

        var vista = FichaTurnoProjection.Apply(evento, ficha);

        vista.VentanaDeEntrada.Should().Be(ventana);
    }

    [Fact]
    public void Apply_ConservaLaVentana_CuandoSedeDeFranjaAsignada()
    {
        var ventana = new VentanaDeEntradaFicha(new TimeOnly(7, 0), new TimeOnly(10, 0), "3 h");
        var ficha = FichaCon(["07:00-16:00"], ventana);
        var evento = SedeDeFranjaAsignada.Crear(Guid.NewGuid(), FranjaOrdinaria.Crear(
            new TimeOnly(7, 0), new TimeOnly(16, 0), sede: new SedeProgramada("sede-01", "Sede Centro")));

        var vista = FichaTurnoProjection.Apply(evento, ficha);

        vista.VentanaDeEntrada.Should().Be(ventana);
    }

    [Fact]
    public void Apply_NoCambiaHorarioResumidoDescripcionNiCompleto_CuandoVentanaDeEntradaModificada()
    {
        var ficha = FichaCon(["07:00-16:00"], null);
        var evento = VentanaDeEntradaModificada.Crear(Guid.NewGuid(), HorasYMinutos.Crear(3, 0));

        var vista = FichaTurnoProjection.Apply(evento, ficha);

        vista.HorarioResumido.Should().Be("07:00-16:00");
        vista.Descripcion.Should().Be("(07:00-16:00)");
        vista.Completo.Should().BeTrue();
    }

    [Fact]
    public void Deserializar_DejaVentanaNula_CuandoElJsonNoTieneLaClaveVentanaDeEntrada()
    {
        const string json = """
            {"id":"t-1","nombre":"Turno","esDescanso":false,"horarioResumido":"Sin franjas",
             "franjas":[],"descripcion":"Sin franjas","completo":false}
            """;

        var ficha = System.Text.Json.JsonSerializer.Deserialize<FichaTurno>(
            json, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));

        ficha!.VentanaDeEntrada.Should().BeNull();
    }
}
