using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.ListarAdvertenciasProgramacionSemanal;
using Bitakora.ControlAsistencia.ReadModels.ControlHoras;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.ListarAdvertenciasProgramacionSemanal;

public class PresentadorAdvertenciasTests
{
    private static readonly DateOnly Lunes = new(2026, 10, 5);
    private static readonly Guid JornadaId = Guid.NewGuid();
    private static readonly JornadaAplicada Jornada = new(JornadaId, 42 * 60, 8 * 60, 0, 1);

    private static CasillaDia Casilla(int dia, int minutos, params AdvertenciaDiaria[] adv) =>
        new(Lunes.AddDays(dia), TipoCasilla.Trabajo, "Turno X", minutos) { Advertencias = adv };

    private static AdvertenciasProgramacionSemanal Doc(
        JornadaAplicada? jornada, int minutos, int sinProgramar, bool ausencias, bool juzgable,
        IReadOnlyList<CasillaDia> casillas, params AdvertenciaSemanal[] semanales) =>
        new("A:2026-W41", "A", "Ana Perez", 2026, 41, Lunes, Lunes.AddDays(6), jornada, minutos,
            sinProgramar, ausencias, juzgable, casillas)
        { AdvertenciasSemanales = semanales, TieneAdvertencias = true };

    private static IReadOnlyList<CasillaDia> SieteCasillas(int minutosDia0) =>
        Enumerable.Range(0, 7).Select(i => Casilla(i, i == 0 ? minutosDia0 : 480)).ToList();

    [Fact]
    public void TiempoHM_DesdeMinutos_SeparaHorasYMinutos()
    {
        TiempoHM.DesdeMinutos(90).Should().Be(new TiempoHM(1, 30));
        TiempoHM.DesdeMinutos(2880).Should().Be(new TiempoHM(48, 0));
    }

    [Fact]
    public void Presentar_ArmaHorasYDescripciones_CuandoSuperaSemanaYTopeDiario()
    {
        var doc = Doc(Jornada, 2880, 0, false, true,
            Enumerable.Range(0, 7).Select(i => i == 0
                ? Casilla(i, 600, new AdvertenciaDiaria(TipoAdvertenciaDiaria.SuperaTopeDiario, 120))
                : Casilla(i, 380)).ToList(),
            new AdvertenciaSemanal(TipoAdvertenciaSemanal.SuperaHorasSemanales, 360));

        var e = PresentadorAdvertencias.Presentar(doc);

        e.CodigoColaborador.Should().Be("A");
        e.NombreCompleto.Should().Be("Ana Perez");
        e.HorasOrdinariasProgramadas.Should().Be(new TiempoHM(48, 0));
        e.Jornada!.Id.Should().Be(JornadaId);
        e.Jornada.HorasSemanales.Should().Be(new TiempoHM(42, 0));
        e.Jornada.TopeDiario.Should().Be(new TiempoHM(8, 0));
        e.Jornada.MinimoDiario.Should().Be(new TiempoHM(0, 0));
        e.Jornada.DiasDescansoPorSemana.Should().Be(1);
        e.Jornada.Descripcion.Should().NotBeNullOrWhiteSpace();
        e.MotivoNoJuzgable.Should().BeNull();
        e.Casillas.Should().HaveCount(7);
        var semanal = e.Advertencias.Should().ContainSingle().Subject;
        semanal.Tipo.Should().Be("SuperaHorasSemanales");
        semanal.Horas.Should().Be(new TiempoHM(6, 0));
        semanal.Descripcion.Should().Be("supera las horas semanales en 6 h");
        var diaria = e.Casillas[0].Advertencias.Should().ContainSingle().Subject;
        diaria.Tipo.Should().Be("SuperaTopeDiario");
        diaria.Horas.Should().Be(new TiempoHM(2, 0));
        diaria.Descripcion.Should().Be("supera el tope diario en 2 h");
        e.Casillas[0].Tipo.Should().Be("Trabajo");
        e.Casillas[0].NombreTurno.Should().Be("Turno X");
        e.Casillas[0].HorasOrdinarias.Should().Be(new TiempoHM(10, 0));
    }

    [Fact]
    public void Presentar_ArmaDescripcionesDeDescansoYDeficit()
    {
        var doc = Doc(Jornada, 2160, 0, false, true,
            Enumerable.Range(0, 7).Select(i => i == 0
                ? Casilla(i, 480, new AdvertenciaDiaria(TipoAdvertenciaDiaria.PorDebajoDelMinimoDiario, 90))
                : Casilla(i, 480)).ToList(),
            new AdvertenciaSemanal(TipoAdvertenciaSemanal.PorDebajoDeHorasSemanales, 240),
            new AdvertenciaSemanal(TipoAdvertenciaSemanal.FaltanDiasDeDescanso, 1),
            new AdvertenciaSemanal(TipoAdvertenciaSemanal.SobranDiasDeDescanso, 1));

        var e = PresentadorAdvertencias.Presentar(doc);

        e.Advertencias.Select(a => a.Descripcion).Should().Equal(
            "4 h por debajo de las horas semanales",
            "le faltan 1 día de descanso",
            "le sobran 1 día de descanso");
        e.Advertencias[0].Horas.Should().Be(new TiempoHM(4, 0));
        e.Advertencias[1].Dias.Should().Be(1);
        e.Casillas[0].Advertencias.Single().Descripcion.Should().Be("1 h 30 min por debajo del mínimo diario");
    }

    [Fact]
    public void Presentar_PluralizaDias_CuandoFaltanVarios()
    {
        var doc = Doc(Jornada, 0, 0, false, true, SieteCasillas(480),
            new AdvertenciaSemanal(TipoAdvertenciaSemanal.FaltanDiasDeDescanso, 2));

        PresentadorAdvertencias.Presentar(doc).Advertencias.Single().Descripcion
            .Should().Be("le faltan 2 días de descanso");
    }

    [Fact]
    public void Presentar_ExplicaDiasSinProgramar_CuandoLaSemanaNoEsJuzgable()
    {
        var doc = Doc(Jornada, 0, 2, false, false, SieteCasillas(480));

        var e = PresentadorAdvertencias.Presentar(doc);

        e.EsJuzgable.Should().BeFalse();
        e.DiasSinProgramar.Should().Be(2);
        e.MotivoNoJuzgable.Should().Be("2 días sin programar");
    }

    [Fact]
    public void Presentar_ExplicaAusencias_CuandoLaSemanaNoEsJuzgablePorAusencias()
    {
        var doc = Doc(Jornada, 0, 0, true, false, SieteCasillas(480));

        PresentadorAdvertencias.Presentar(doc).MotivoNoJuzgable.Should().Be("tiene ausencias");
    }

    [Fact]
    public void Presentar_CombinaMotivos_CuandoHaySinProgramarYAusencias()
    {
        var doc = Doc(Jornada, 0, 1, true, false, SieteCasillas(480));

        PresentadorAdvertencias.Presentar(doc).MotivoNoJuzgable.Should()
            .Contain("1 día sin programar").And.Contain("tiene ausencias");
    }

    [Fact]
    public void Presentar_DejaJornadaNula_CuandoLaSemanaNoTieneJornada()
    {
        var doc = Doc(null, 0, 0, false, true, SieteCasillas(480));

        PresentadorAdvertencias.Presentar(doc).Jornada.Should().BeNull();
    }
}
