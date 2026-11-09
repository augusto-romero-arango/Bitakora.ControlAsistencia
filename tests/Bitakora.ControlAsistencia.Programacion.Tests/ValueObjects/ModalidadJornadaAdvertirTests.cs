using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;

namespace Bitakora.ControlAsistencia.Programacion.Tests.ValueObjects;

public class ModalidadJornadaAdvertirTests
{
    private static LimitesJornada Limites(int horas, int topeH, int topeM, int minH = 0, int minM = 0, int descansos = 1) =>
        LimitesJornada.Crear(
            HorasYMinutos.Crear(horas, 0), HorasYMinutos.Crear(topeH, topeM),
            HorasYMinutos.Crear(minH, minM), descansos);

    private static (TipoAdvertenciaJornada Tipo, int Magnitud, int ValorDeLey)[] Resumen(
        ModalidadJornada modalidad, LimitesJornada limites) =>
        modalidad.Advertir(limites).Select(a => (a.Tipo, a.Magnitud, a.ValorDeLey)).ToArray();

    [Fact]
    public void Ordinaria_AdvierteTopeDiario_CuandoElTopeSuperaOchoHorasVeinticuatro()
    {
        var resultado = Resumen(ModalidadJornada.Ordinaria, Limites(42, 9, 0));

        resultado.Should().Equal((TipoAdvertenciaJornada.SuperaTopeDiarioDeLaModalidad, 36, 504));
    }

    [Fact]
    public void Ordinaria_NoAdvierte_CuandoElTopeEsOchoHorasVeinticuatroEnCincoDias()
    {
        Resumen(ModalidadJornada.Ordinaria, Limites(42, 8, 24, descansos: 2)).Should().BeEmpty();
    }

    [Fact]
    public void Ordinaria_AdvierteHorasSemanales_CuandoSuperanCuarentaYDosHoras()
    {
        var resultado = Resumen(ModalidadJornada.Ordinaria, Limites(43, 8, 24));

        resultado.Should().Equal((TipoAdvertenciaJornada.SuperaHorasSemanalesDeLaModalidad, 60, 2520));
    }

    [Fact]
    public void Flexible_AdvierteHorasSemanales_CuandoSuperanCuarentaYDosHoras()
    {
        var resultado = Resumen(ModalidadJornada.Flexible, Limites(43, 9, 0, 4, 0));

        resultado.Should().Equal((TipoAdvertenciaJornada.SuperaHorasSemanalesDeLaModalidad, 60, 2520));
    }

    [Fact]
    public void Flexible_NoAdvierteHorasSemanales_CuandoIgualanElTecho()
    {
        Resumen(ModalidadJornada.Flexible, Limites(42, 9, 0, 4, 0)).Should().BeEmpty();
    }

    [Fact]
    public void TurnosSucesivos_AdvierteHorasSemanales_CuandoSuperanTreintaYSeisHoras()
    {
        var resultado = Resumen(ModalidadJornada.TurnosSucesivos, Limites(40, 6, 0, descansos: 0));

        resultado.Should().Equal((TipoAdvertenciaJornada.SuperaHorasSemanalesDeLaModalidad, 240, 2160));
    }

    [Fact]
    public void TurnosSucesivos_NoAdvierteHorasSemanales_CuandoIgualanElTecho()
    {
        Resumen(ModalidadJornada.TurnosSucesivos, Limites(36, 6, 0, descansos: 1)).Should().BeEmpty();
    }

    [Fact]
    public void SinSolucionDeContinuidad_AdvierteHorasSemanales_CuandoSuperanCincuentaYSeisHoras()
    {
        var resultado = Resumen(ModalidadJornada.SinSolucionDeContinuidad, Limites(57, 12, 0, descansos: 0));

        resultado.Should().Equal((TipoAdvertenciaJornada.SuperaHorasSemanalesDeLaModalidad, 60, 3360));
    }

    [Fact]
    public void SinSolucionDeContinuidad_NoAdvierte_CuandoLasHorasIgualanElTechoYElTopeEsAlto()
    {
        Resumen(ModalidadJornada.SinSolucionDeContinuidad, Limites(56, 12, 0, descansos: 0)).Should().BeEmpty();
    }

    [Fact]
    public void Flexible_AdvierteTopeDiario_CuandoElTopeSuperaNueveHoras()
    {
        var resultado = Resumen(ModalidadJornada.Flexible, Limites(42, 9, 1, 4, 0));

        resultado.Should().Equal((TipoAdvertenciaJornada.SuperaTopeDiarioDeLaModalidad, 1, 540));
    }

    [Fact]
    public void Flexible_AdvierteMinimoDiario_CuandoElMinimoEsMenorQueCuatroHoras()
    {
        var resultado = Resumen(ModalidadJornada.Flexible, Limites(42, 9, 0, 3, 0));

        resultado.Should().Equal((TipoAdvertenciaJornada.PorDebajoDelMinimoDiarioDeLaModalidad, 60, 240));
    }

    [Fact]
    public void Flexible_AdvierteMinimoCompleto_CuandoElMinimoEsCero()
    {
        var resultado = Resumen(ModalidadJornada.Flexible, Limites(42, 9, 0));

        resultado.Should().Equal((TipoAdvertenciaJornada.PorDebajoDelMinimoDiarioDeLaModalidad, 240, 240));
    }

    [Fact]
    public void Flexible_NoAdvierteMinimo_CuandoElMinimoEsCuatroHoras()
    {
        Resumen(ModalidadJornada.Flexible, Limites(42, 9, 0, 4, 0)).Should().BeEmpty();
    }

    [Fact]
    public void Flexible_OrdenaLasAdvertenciasPorTipo_CuandoHayVarias()
    {
        var resultado = Resumen(ModalidadJornada.Flexible, Limites(43, 10, 0, 2, 0));

        resultado.Select(r => r.Tipo).Should().Equal(
            TipoAdvertenciaJornada.SuperaTopeDiarioDeLaModalidad,
            TipoAdvertenciaJornada.SuperaHorasSemanalesDeLaModalidad,
            TipoAdvertenciaJornada.PorDebajoDelMinimoDiarioDeLaModalidad);
    }

    [Fact]
    public void TurnosSucesivos_AdvierteTopeDiario_CuandoElTopeSuperaSeisHoras()
    {
        var resultado = Resumen(ModalidadJornada.TurnosSucesivos, Limites(36, 6, 30, descansos: 1));

        resultado.Should().Equal((TipoAdvertenciaJornada.SuperaTopeDiarioDeLaModalidad, 30, 360));
    }

    [Fact]
    public void Ordinaria_NoAdvierteMinimo_CuandoElMinimoEsCero()
    {
        Resumen(ModalidadJornada.Ordinaria, Limites(42, 8, 24)).Should().BeEmpty();
    }

    [Fact]
    public void ExcluidaDeJornadaMaxima_NoAdvierteNada_CuandoLosLimitesSonExtremos()
    {
        Resumen(ModalidadJornada.ExcluidaDeJornadaMaxima, Limites(90, 15, 0, 0, 0, descansos: 0)).Should().BeEmpty();
    }

    [Fact]
    public void Ordinaria_AdvierteIgualConCualquierCantidadDeDescansos_CuandoSoloCambianLosDias()
    {
        var esperado = new[] { (TipoAdvertenciaJornada.SuperaTopeDiarioDeLaModalidad, 36, 504) };

        for (var descansos = 0; descansos <= 6; descansos++)
            Resumen(ModalidadJornada.Ordinaria, Limites(8, 9, 0, descansos: descansos)).Should().Equal(esperado);
    }
}
