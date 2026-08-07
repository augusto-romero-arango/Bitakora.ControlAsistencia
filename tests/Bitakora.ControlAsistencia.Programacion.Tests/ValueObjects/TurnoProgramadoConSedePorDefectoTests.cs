// Issue #341: cascada de sede al copiar el turno del catalogo en la solicitud de programacion.
// Tests unitarios PUROS (sin harness) sobre TurnoProgramado.ConSedePorDefecto -- funcion pura
// sobre records, tal como el precedente TurnoDiario.Segmentar (#327). Cubre la tabla de verdad
// decidida en la sesion del planner (CA-1/CA-2/CA-3):
//   franja con sede + solicitud con sede -> gana la franja
//   franja con sede + solicitud sin sede -> franja
//   franja sin sede + solicitud con sede -> solicitud
//   ambas sin sede -> null ("sin sede asignada", estado valido)

using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;

namespace Bitakora.ControlAsistencia.Programacion.Tests.ValueObjects;

public class TurnoProgramadoConSedePorDefectoTests
{
    private static readonly SedeProgramada SedeCatalogo = new("SEDE-SUBA", "Suba");
    private static readonly SedeProgramada SedeSolicitud = new("SEDE-CHAPINERO", "Chapinero");

    private static FranjaProgramada CrearFranja(SedeProgramada? sede, string descripcion = "(06:00-14:00)") =>
        new(new TimeOnly(6, 0), new TimeOnly(14, 0), 0, [], [], descripcion, sede);

    private static TurnoProgramado CrearTurno(params FranjaProgramada[] franjas) =>
        new("Turno Manana", franjas.ToList().AsReadOnly(), "Turno Manana (06:00-14:00)");

    // Tabla de verdad, fila 1: franja con sede + solicitud con sede -> gana la franja.
    [Fact]
    public void ConSedePorDefecto_ConservaLaSedeDeLaFranja_CuandoLaFranjaYaTraeSedeDelCatalogo()
    {
        var turno = CrearTurno(CrearFranja(SedeCatalogo));

        var resultado = turno.ConSedePorDefecto(SedeSolicitud);

        resultado.FranjasOrdinarias[0].Sede.Should().Be(SedeCatalogo);
    }

    // Tabla de verdad, fila 2: franja con sede + solicitud sin sede -> franja (identidad).
    [Fact]
    public void ConSedePorDefecto_ConservaLaSedeDeLaFranja_CuandoLaSedePorDefectoEsNull()
    {
        var turno = CrearTurno(CrearFranja(SedeCatalogo));

        var resultado = turno.ConSedePorDefecto(null);

        resultado.Should().Be(turno);
        resultado.FranjasOrdinarias[0].Sede.Should().Be(SedeCatalogo);
    }

    // Tabla de verdad, fila 3: franja sin sede + solicitud con sede -> solicitud.
    [Fact]
    public void ConSedePorDefecto_AplicaLaSedePorDefecto_CuandoLaFranjaNoTraeSede()
    {
        var turno = CrearTurno(CrearFranja(null));

        var resultado = turno.ConSedePorDefecto(SedeSolicitud);

        resultado.FranjasOrdinarias[0].Sede.Should().Be(SedeSolicitud);
    }

    // Tabla de verdad, fila 4: ambas sin sede -> null (sin sede asignada, estado valido).
    [Fact]
    public void ConSedePorDefecto_DejaLaFranjaSinSede_CuandoNingunoTraeSede()
    {
        var turno = CrearTurno(CrearFranja(null));

        var resultado = turno.ConSedePorDefecto(null);

        resultado.FranjasOrdinarias[0].Sede.Should().BeNull();
    }

    // CA-1: cada franja del turno resuelve su cascada de forma INDEPENDIENTE -- una implementacion
    // que aplicara la sede por defecto a todo el turno de una sola vez (en lugar de franja por
    // franja) pasaria las cuatro filas de la tabla de verdad de arriba (turnos de una sola franja)
    // pero fallaria aqui.
    [Fact]
    public void ConSedePorDefecto_ResuelveCadaFranjaDeFormaIndependiente_CuandoElTurnoTieneFranjasMixtas()
    {
        var franjaConSede = new FranjaProgramada(
            new TimeOnly(6, 0), new TimeOnly(10, 0), 0, [], [], "(06:00-10:00)[sede:Suba]", SedeCatalogo);
        var franjaSinSede = new FranjaProgramada(
            new TimeOnly(14, 0), new TimeOnly(18, 0), 0, [], [], "(14:00-18:00)");
        var turno = CrearTurno(franjaConSede, franjaSinSede);

        var resultado = turno.ConSedePorDefecto(SedeSolicitud);

        resultado.FranjasOrdinarias[0].Sede.Should().Be(SedeCatalogo);
        resultado.FranjasOrdinarias[1].Sede.Should().Be(SedeSolicitud);
    }

    // La cascada NUNCA reconstruye el ToString() ya congelado en el catalogo (Descripcion es dato
    // derivado que se calculo ANTES de conocer la sede de la solicitud): solo el campo Sede cambia
    // via `with`. Ademas de Descripcion, cubre que HoraInicio/HoraFin/Descansos/Extras/Nombre no se
    // pierden -- si la implementacion reconstruyera la franja/turno a mano en lugar de usar `with`,
    // este test lo delata.
    [Fact]
    public void ConSedePorDefecto_PreservaLosDemasCamposDeLaFranjaYDelTurno_CuandoAplicaLaSedePorDefecto()
    {
        var descanso = new SubFranjaProgramada(new TimeOnly(10, 0), new TimeOnly(10, 15), 0, 0, "(10:00-10:15)");
        var franja = new FranjaProgramada(
            new TimeOnly(6, 0), new TimeOnly(14, 0), 0, [descanso], [],
            "(06:00-14:00)[Descansos:(10:00-10:15)]");
        var turno = new TurnoProgramado(
            "Turno Manana",
            new List<FranjaProgramada> { franja }.AsReadOnly(),
            "Turno Manana (06:00-14:00)[Descansos:(10:00-10:15)]");

        var resultado = turno.ConSedePorDefecto(SedeSolicitud);

        resultado.Nombre.Should().Be("Turno Manana");
        resultado.Descripcion.Should().Be(turno.Descripcion);
        resultado.FranjasOrdinarias[0].HoraInicio.Should().Be(new TimeOnly(6, 0));
        resultado.FranjasOrdinarias[0].HoraFin.Should().Be(new TimeOnly(14, 0));
        resultado.FranjasOrdinarias[0].Descripcion.Should().Be(franja.Descripcion);
        resultado.FranjasOrdinarias[0].Descansos.Should().ContainSingle().Which.Should().Be(descanso);
        resultado.FranjasOrdinarias[0].Sede.Should().Be(SedeSolicitud);
    }
}
