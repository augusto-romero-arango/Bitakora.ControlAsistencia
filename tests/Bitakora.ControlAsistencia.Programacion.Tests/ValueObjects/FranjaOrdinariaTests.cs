using AwesomeAssertions;

using Bitakora.ControlAsistencia.Programacion.DomainEvents;

namespace Bitakora.ControlAsistencia.Programacion.Tests.ValueObjects;

/// <summary>
/// El exito del factory se verifica con ToString(), que expone la estructura de hijos.
/// </summary>
public class FranjaOrdinariaTests
{
    [Fact]
    public void Crear_RetornaFranjaOrdinariaSinHijos_CuandoDatosValidosSinHijos()
    {
        var franja = FranjaOrdinaria.Crear(new TimeOnly(6, 0), new TimeOnly(12, 0));

        franja.ToString().Should().Be("06:00-12:00");
    }

    [Fact]
    public void Crear_RetornaFranjaOrdinariaConDescanso_CuandoDescansoEstaContenido()
    {
        var descanso = SubFranja.Crear(new TimeOnly(10, 0), new TimeOnly(10, 15));

        var franja = FranjaOrdinaria.Crear(
            new TimeOnly(6, 0), new TimeOnly(12, 0),
            descansos: [descanso]);

        franja.ToString().Should().Be("06:00-12:00, descanso 10:00-10:15");
    }

    [Fact]
    public void Crear_RetornaFranjaOrdinariaConExtra_CuandoExtraEstaContenida()
    {
        var extra = SubFranja.Crear(new TimeOnly(6, 0), new TimeOnly(8, 0));

        var franja = FranjaOrdinaria.Crear(
            new TimeOnly(6, 0), new TimeOnly(12, 0),
            extras: [extra]);

        franja.ToString().Should().Be("06:00-12:00, extra 06:00-08:00");
    }

    [Fact]
    public void Crear_RetornaFranjaOrdinariaConDescansosYExtras_CuandoTodosEsanContenidos()
    {
        var descanso = SubFranja.Crear(new TimeOnly(10, 0), new TimeOnly(10, 15));
        var extra = SubFranja.Crear(new TimeOnly(6, 0), new TimeOnly(8, 0));

        var franja = FranjaOrdinaria.Crear(
            new TimeOnly(6, 0), new TimeOnly(12, 0),
            descansos: [descanso],
            extras: [extra]);

        franja.ToString().Should().Be("06:00-12:00, descanso 10:00-10:15, extra 06:00-08:00");
    }

    // Inicio == fin no tiene error propio: lo absorbe DuracionNoPositiva.
    [Fact]
    public void Crear_LanzaExcepcion_CuandoInicioYFinSonIguales()
    {
        var act = () => FranjaOrdinaria.Crear(new TimeOnly(10, 0), new TimeOnly(10, 0));

        act.Should().ThrowExactly<ArgumentException>()
            .WithMessage($"*{FranjaTemporal.Mensajes.DuracionNoPositiva}*");
    }

    [Fact]
    public void Crear_LanzaExcepcion_CuandoOffsetExplicitoProduceDuracionNegativa()
    {
        var act = () => FranjaOrdinaria.Crear(new TimeOnly(8, 0), new TimeOnly(10, 0), diaOffsetFin: -1);

        act.Should().ThrowExactly<ArgumentException>()
            .WithMessage($"*{FranjaTemporal.Mensajes.DuracionNoPositiva}*");
    }

    [Fact]
    public void Crear_LanzaExcepcion_CuandoDuracionExcedeUnDia()
    {
        // 08:00 -> 10:00+1 = 26 horas
        var act = () => FranjaOrdinaria.Crear(new TimeOnly(8, 0), new TimeOnly(10, 0), diaOffsetFin: 1);

        act.Should().ThrowExactly<ArgumentException>()
            .WithMessage($"*{FranjaOrdinaria.Mensajes.DuracionExcedeUnDia}*");
    }

    [Fact]
    public void Crear_LanzaExcepcion_CuandoDuracionExcedeUnDiaPorUnMinuto()
    {
        // 08:00 -> 08:01+1 = 1441 minutos, borde justo por encima del tope
        var act = () => FranjaOrdinaria.Crear(new TimeOnly(8, 0), new TimeOnly(8, 1), diaOffsetFin: 1);

        act.Should().ThrowExactly<ArgumentException>()
            .WithMessage($"*{FranjaOrdinaria.Mensajes.DuracionExcedeUnDia}*");
    }

    // 24 h exactas se aceptan: esquemas 24x24.
    [Fact]
    public void Crear_AceptaFranjaDe24HorasExactas_CuandoOffsetExplicitoIgualaInicioYFin()
    {
        var franja = FranjaOrdinaria.Crear(new TimeOnly(8, 0), new TimeOnly(8, 0), diaOffsetFin: 1);

        franja.DuracionEnMinutos().Should().Be(1440);
        franja.ToString().Should().Be("08:00-08:00+1");
    }

    [Fact]
    public void DuracionEnMinutos_Retorna180_CuandoFranja8A11()
    {
        var franja = FranjaOrdinaria.Crear(new TimeOnly(8, 0), new TimeOnly(11, 0));

        franja.DuracionEnMinutos().Should().Be(180);
    }

    // Inicio inclusivo, fin exclusivo.
    [Fact]
    public void DuracionEnMinutos_Retorna360_CuandoFranja6A12Exactos()
    {
        var franja = FranjaOrdinaria.Crear(new TimeOnly(6, 0), new TimeOnly(12, 0));

        franja.DuracionEnMinutos().Should().Be(360);
    }

    [Fact]
    public void DuracionEnMinutos_Retorna480_CuandoFranjaNocturna22A6ConOffset()
    {
        var franja = FranjaOrdinaria.Crear(new TimeOnly(22, 0), new TimeOnly(6, 0), diaOffsetFin: 1);

        franja.DuracionEnMinutos().Should().Be(480);
    }

    [Fact]
    public void DuracionEnHorasDecimales_Retorna3Punto5_CuandoFranjaDe210Minutos()
    {
        var franja = FranjaOrdinaria.Crear(new TimeOnly(8, 0), new TimeOnly(11, 30));

        franja.DuracionEnHorasDecimales().Should().Be(3.5m);
    }

    [Fact]
    public void Crear_InfiereOffsetMasUno_CuandoFinEsMenorQueInicio()
    {
        var franja = FranjaOrdinaria.Crear(new TimeOnly(22, 0), new TimeOnly(6, 0));

        franja.ToString().Should().Be("22:00-06:00+1");
    }

    [Fact]
    public void DuracionEnMinutos_Retorna480_CuandoOffsetInferido()
    {
        var franja = FranjaOrdinaria.Crear(new TimeOnly(22, 0), new TimeOnly(6, 0));

        franja.DuracionEnMinutos().Should().Be(480);
    }

    [Fact]
    public void ToString_MuestraFormatoConParentesis_SinOffset()
    {
        var franja = FranjaOrdinaria.Crear(new TimeOnly(14, 0), new TimeOnly(18, 0));

        franja.ToString().Should().Be("14:00-18:00");
    }

    [Fact]
    public void ToString_MuestraOffsetMasUno_CuandoDiaOffsetFinEsUno()
    {
        var franja = FranjaOrdinaria.Crear(new TimeOnly(22, 0), new TimeOnly(6, 0), diaOffsetFin: 1);

        franja.ToString().Should().Be("22:00-06:00+1");
    }

    [Fact]
    public void ToString_MuestraMultiplesDescansosYExtras_CuandoTieneVariosHijos()
    {
        var descanso1 = SubFranja.Crear(new TimeOnly(10, 0), new TimeOnly(10, 15));
        var descanso2 = SubFranja.Crear(new TimeOnly(11, 0), new TimeOnly(11, 15));
        var extra1 = SubFranja.Crear(new TimeOnly(6, 0), new TimeOnly(7, 0));
        var extra2 = SubFranja.Crear(new TimeOnly(7, 0), new TimeOnly(8, 0));

        var franja = FranjaOrdinaria.Crear(
            new TimeOnly(6, 0), new TimeOnly(14, 0),
            descansos: [descanso1, descanso2],
            extras: [extra1, extra2]);

        franja.ToString().Should().Be(
            "06:00-14:00, descanso 10:00-10:15, descanso 11:00-11:15, extra 06:00-07:00, extra 07:00-08:00");
    }

    [Fact]
    public void Crear_LanzaExcepcion_CuandoDescansoExcedeFin()
    {
        // descanso termina a las 13:00, ordinaria termina a las 12:00
        var descanso = SubFranja.Crear(new TimeOnly(11, 0), new TimeOnly(13, 0));

        var act = () => FranjaOrdinaria.Crear(
            new TimeOnly(6, 0), new TimeOnly(12, 0),
            descansos: [descanso]);

        act.Should().ThrowExactly<ArgumentException>()
            .WithMessage($"*{FranjaTemporal.Mensajes.FranjaHijaFueraDeContenedor}*");
    }

    [Fact]
    public void Crear_LanzaExcepcion_CuandoDescansoEmpiezaAntesDeInicio()
    {
        // descanso empieza a las 7:00, ordinaria empieza a las 8:00
        var descanso = SubFranja.Crear(new TimeOnly(7, 0), new TimeOnly(9, 0));

        var act = () => FranjaOrdinaria.Crear(
            new TimeOnly(8, 0), new TimeOnly(12, 0),
            descansos: [descanso]);

        act.Should().ThrowExactly<ArgumentException>()
            .WithMessage($"*{FranjaTemporal.Mensajes.FranjaHijaFueraDeContenedor}*");
    }

    [Fact]
    public void Crear_RetornaFranjaConExtra_CuandoExtraTerminaExactamenteEnElFinDeLaOrdinaria()
    {
        // Extremo coincidente: el fin de la hija es el fin de la ordinaria - SI esta contenida.
        // Guardrail del limite superior de EstaContenidaEn (el inferior ya lo cubre
        // Crear_RetornaFranjaOrdinariaConExtra_CuandoExtraEstaContenida).
        var extra = SubFranja.Crear(new TimeOnly(11, 0), new TimeOnly(12, 0));

        var franja = FranjaOrdinaria.Crear(
            new TimeOnly(6, 0), new TimeOnly(12, 0),
            extras: [extra]);

        franja.ToString().Should().Be("06:00-12:00, extra 11:00-12:00");
    }

    [Fact]
    public void Crear_LanzaExcepcion_CuandoExtraExcedeFin()
    {
        // extra termina a las 13:00, ordinaria termina a las 12:00
        var extra = SubFranja.Crear(new TimeOnly(11, 0), new TimeOnly(13, 0));

        var act = () => FranjaOrdinaria.Crear(
            new TimeOnly(6, 0), new TimeOnly(12, 0),
            extras: [extra]);

        act.Should().ThrowExactly<ArgumentException>()
            .WithMessage($"*{FranjaTemporal.Mensajes.FranjaHijaFueraDeContenedor}*");
    }

    [Fact]
    public void Crear_RetornaFranjaConDescanso_CuandoDescansoEstaContenidoEnOrdinariaNocturna()
    {
        // Ordinaria nocturna: 22:00+0 -> 06:00+1
        // Descanso en tramo nocturno antes de medianoche: 23:00+0 -> 23:15+0
        var descanso = SubFranja.Crear(new TimeOnly(23, 0), new TimeOnly(23, 15),
            diaOffsetInicio: 0, diaOffsetFin: 0);

        var franja = FranjaOrdinaria.Crear(
            new TimeOnly(22, 0), new TimeOnly(6, 0), diaOffsetFin: 1,
            descansos: [descanso]);

        franja.ToString().Should().Be("22:00-06:00+1, descanso 23:00-23:15");
    }

    [Fact]
    public void Crear_LanzaExcepcion_CuandoExtraExcedeLaOrdinariaNocturna()
    {
        // Ordinaria nocturna: 22:00+0 -> 06:00+1
        // Extra: 05:00+1 -> 07:00+1 - termina despues de las 06:00+1
        var extra = SubFranja.Crear(new TimeOnly(5, 0), new TimeOnly(7, 0),
            diaOffsetInicio: 1, diaOffsetFin: 1);

        var act = () => FranjaOrdinaria.Crear(
            new TimeOnly(22, 0), new TimeOnly(6, 0), diaOffsetFin: 1,
            extras: [extra]);

        act.Should().ThrowExactly<ArgumentException>()
            .WithMessage($"*{FranjaTemporal.Mensajes.FranjaHijaFueraDeContenedor}*");
    }

    [Fact]
    public void Crear_LanzaExcepcion_CuandoExtraYDescansoSeSolapan()
    {
        // extra 09:00-11:00 y descanso 10:00-10:15 se solapan
        var extra = SubFranja.Crear(new TimeOnly(9, 0), new TimeOnly(11, 0));
        var descanso = SubFranja.Crear(new TimeOnly(10, 0), new TimeOnly(10, 15));

        var act = () => FranjaOrdinaria.Crear(
            new TimeOnly(6, 0), new TimeOnly(12, 0),
            descansos: [descanso],
            extras: [extra]);

        act.Should().ThrowExactly<ArgumentException>()
            .WithMessage($"*{FranjaTemporal.Mensajes.FranjasHijasSeSuperponen}*");
    }

    [Fact]
    public void Crear_LanzaExcepcion_CuandoDosDescansosSeSolapan()
    {
        var descanso1 = SubFranja.Crear(new TimeOnly(9, 0), new TimeOnly(10, 0));
        var descanso2 = SubFranja.Crear(new TimeOnly(9, 30), new TimeOnly(10, 30));

        var act = () => FranjaOrdinaria.Crear(
            new TimeOnly(6, 0), new TimeOnly(12, 0),
            descansos: [descanso1, descanso2]);

        act.Should().ThrowExactly<ArgumentException>()
            .WithMessage($"*{FranjaTemporal.Mensajes.FranjasHijasSeSuperponen}*");
    }

    [Fact]
    public void Crear_RetornaFranjaConHijosContiguos_CuandoFinDeUnoEsInicioDelOtro()
    {
        // extra termina exactamente donde empieza el descanso - contiguas, no solapadas
        var extra = SubFranja.Crear(new TimeOnly(6, 0), new TimeOnly(8, 0));
        var descanso = SubFranja.Crear(new TimeOnly(8, 0), new TimeOnly(8, 15));

        var franja = FranjaOrdinaria.Crear(
            new TimeOnly(6, 0), new TimeOnly(12, 0),
            descansos: [descanso],
            extras: [extra]);

        franja.ToString().Should().Be("06:00-12:00, descanso 08:00-08:15, extra 06:00-08:00");
    }

    [Fact]
    public void Crear_LanzaExcepcion_CuandoDosDescansosSeSolapanConOffsets()
    {
        // Ordinaria nocturna: 22:00+0 -> 06:00+1
        // Dos descansos que se solapan en el tramo antes de medianoche
        var descanso1 = SubFranja.Crear(new TimeOnly(23, 0), new TimeOnly(23, 30),
            diaOffsetInicio: 0, diaOffsetFin: 0);
        var descanso2 = SubFranja.Crear(new TimeOnly(23, 15), new TimeOnly(23, 45),
            diaOffsetInicio: 0, diaOffsetFin: 0);

        var act = () => FranjaOrdinaria.Crear(
            new TimeOnly(22, 0), new TimeOnly(6, 0), diaOffsetFin: 1,
            descansos: [descanso1, descanso2]);

        act.Should().ThrowExactly<ArgumentException>()
            .WithMessage($"*{FranjaTemporal.Mensajes.FranjasHijasSeSuperponen}*");
    }

    [Fact]
    public void Crear_RetornaFranjaConHijosConOffsets_CuandoFranjasConContiguasConOffsets()
    {
        // Ordinaria nocturna: 22:00+0 -> 06:00+1
        // descanso termina exactamente donde empieza la extra - contiguas, no solapadas
        var descanso = SubFranja.Crear(new TimeOnly(23, 0), new TimeOnly(23, 15),
            diaOffsetInicio: 0, diaOffsetFin: 0);
        var extra = SubFranja.Crear(new TimeOnly(23, 15), new TimeOnly(0, 0),
            diaOffsetInicio: 0, diaOffsetFin: 1);

        var franja = FranjaOrdinaria.Crear(
            new TimeOnly(22, 0), new TimeOnly(6, 0), diaOffsetFin: 1,
            descansos: [descanso],
            extras: [extra]);

        franja.ToString().Should().Be("22:00-06:00+1, descanso 23:00-23:15, extra 23:15-00:00+1");
    }

    [Fact]
    public void ToString_IncluyeLabelDeSede_CuandoFranjaTieneSedeAsignada()
    {
        var sede = new SedeProgramada("SEDE-SUBA", "Suba");

        var franja = FranjaOrdinaria.Crear(new TimeOnly(6, 0), new TimeOnly(12, 0), sede: sede);

        franja.ToString().Should().Be("06:00-12:00");
    }

    [Fact]
    public void Crear_LanzaExcepcion_CuandoSedeTieneIdVacio()
    {
        var sedeInvalida = new SedeProgramada("", "Suba");

        var act = () => FranjaOrdinaria.Crear(
            new TimeOnly(6, 0), new TimeOnly(12, 0), sede: sedeInvalida);

        act.Should().ThrowExactly<ArgumentException>()
            .WithMessage($"*{FranjaOrdinaria.Mensajes.SedeIncompleta}*");
    }

    [Fact]
    public void Crear_LanzaExcepcion_CuandoSedeTieneNombreEnBlanco()
    {
        var sedeInvalida = new SedeProgramada("SEDE-SUBA", "   ");

        var act = () => FranjaOrdinaria.Crear(
            new TimeOnly(6, 0), new TimeOnly(12, 0), sede: sedeInvalida);

        act.Should().ThrowExactly<ArgumentException>()
            .WithMessage($"*{FranjaOrdinaria.Mensajes.SedeIncompleta}*");
    }

    [Fact]
    public void ConDescanso_InfiereOffsetsCero_CuandoHijaCaeAntesDeMedianoche()
    {
        var franja = FranjaOrdinaria.Crear(new TimeOnly(22, 0), new TimeOnly(6, 0))
            .ConDescanso(new TimeOnly(23, 0), new TimeOnly(23, 30));

        franja.ToString().Should().Be("22:00-06:00+1, descanso 23:00-23:30");
    }

    [Fact]
    public void ConDescanso_InfiereOffsetInicioCeroYOffsetFinUno_CuandoHijaCruzaMedianoche()
    {
        var franja = FranjaOrdinaria.Crear(new TimeOnly(22, 0), new TimeOnly(6, 0))
            .ConDescanso(new TimeOnly(23, 30), new TimeOnly(0, 30));

        franja.ToString().Should().Be("22:00-06:00+1, descanso 23:30-00:30+1");
    }

    [Fact]
    public void ConDescanso_InfiereOffsetsUno_CuandoHijaCaeEnLaMadrugada()
    {
        var franja = FranjaOrdinaria.Crear(new TimeOnly(22, 0), new TimeOnly(6, 0))
            .ConDescanso(new TimeOnly(2, 0), new TimeOnly(2, 30));

        franja.ToString().Should().Be("22:00-06:00+1, descanso 02:00+1-02:30+1");
    }

    [Fact]
    public void ConExtra_InfiereOffsetsUno_CuandoFinDeLaHijaCoincideConFinDeLaFranja()
    {
        var franja = FranjaOrdinaria.Crear(new TimeOnly(22, 0), new TimeOnly(6, 0))
            .ConExtra(new TimeOnly(5, 30), new TimeOnly(6, 0));

        franja.ToString().Should().Be("22:00-06:00+1, extra 05:30+1-06:00+1");
    }

    [Fact]
    public void ConDescanso_LanzaExcepcion_CuandoOffsetInferidoDejaLaHijaFueraDelContenedor()
    {
        var franja = FranjaOrdinaria.Crear(new TimeOnly(6, 0), new TimeOnly(14, 0));

        var act = () => franja.ConDescanso(new TimeOnly(5, 0), new TimeOnly(5, 30));

        act.Should().ThrowExactly<ArgumentException>()
            .WithMessage($"*{FranjaTemporal.Mensajes.FranjaHijaFueraDeContenedor}*");
    }

    // La franja original queda intacta (inmutabilidad).
    [Fact]
    public void ConDescanso_LanzaExcepcion_CuandoInicioYFinDeLaHijaSonIguales()
    {
        var franjaOriginal = FranjaOrdinaria.Crear(new TimeOnly(22, 0), new TimeOnly(6, 0));

        var act = () => franjaOriginal.ConDescanso(new TimeOnly(23, 30), new TimeOnly(23, 30));

        act.Should().ThrowExactly<ArgumentException>()
            .WithMessage($"*{FranjaTemporal.Mensajes.DuracionNoPositiva}*");
        franjaOriginal.ToString().Should().Be("22:00-06:00+1");
    }

    // La franja original queda intacta (inmutabilidad).
    [Fact]
    public void ConDescanso_LanzaExcepcion_CuandoHijaNuevaSeSolapaConHijaExistente()
    {
        var franjaConDescanso = FranjaOrdinaria.Crear(new TimeOnly(22, 0), new TimeOnly(6, 0))
            .ConDescanso(new TimeOnly(23, 0), new TimeOnly(23, 30));

        var act = () => franjaConDescanso.ConDescanso(new TimeOnly(23, 15), new TimeOnly(23, 45));

        act.Should().ThrowExactly<ArgumentException>()
            .WithMessage($"*{FranjaTemporal.Mensajes.FranjasHijasSeSuperponen}*");
        franjaConDescanso.ToString().Should().Be("22:00-06:00+1, descanso 23:00-23:30");
    }

    // Agregar una hija reconstruye la franja desde sus campos: la sede prearmada y las hijas ya
    // presentes deben sobrevivir esa reconstruccion.
    [Fact]
    public void ConExtra_ConservaLaSedePrearmadaYElDescansoPrevio_CuandoSeEncadenaSobreConDescanso()
    {
        var sede = new SedeProgramada("SEDE-SUBA", "Suba");

        var franja = FranjaOrdinaria.Crear(new TimeOnly(22, 0), new TimeOnly(6, 0), sede: sede)
            .ConDescanso(new TimeOnly(2, 0), new TimeOnly(2, 30))
            .ConExtra(new TimeOnly(5, 0), new TimeOnly(5, 30));

        franja.ToString().Should().Be(
            "22:00-06:00+1, descanso 02:00+1-02:30+1, extra 05:00+1-05:30+1");
    }

    // SeSolapaCon es publico para que CatalogoTurnos.AgregarFranja decida el solape sin acceder a
    // FranjaTemporal.SeSolapaCon (internal a este ensamblado).
    [Fact]
    public void SeSolapaCon_EsFalse_CuandoLasFranjasSonContiguas()
    {
        var primera = FranjaOrdinaria.Crear(new TimeOnly(6, 0), new TimeOnly(14, 0));
        var segunda = FranjaOrdinaria.Crear(new TimeOnly(14, 0), new TimeOnly(22, 0));

        primera.SeSolapaCon(segunda).Should().BeFalse();
    }

    [Fact]
    public void SeSolapaCon_EsTrue_CuandoLasFranjasSeSuperponenParcialmente()
    {
        var primera = FranjaOrdinaria.Crear(new TimeOnly(6, 0), new TimeOnly(14, 0));
        var segunda = FranjaOrdinaria.Crear(new TimeOnly(10, 0), new TimeOnly(18, 0));

        primera.SeSolapaCon(segunda).Should().BeTrue();
    }

    [Fact]
    public void SeSolapaCon_EsSimetrico_CuandoSeInvierteElOrdenDeLasFranjas()
    {
        var primera = FranjaOrdinaria.Crear(new TimeOnly(6, 0), new TimeOnly(14, 0));
        var segunda = FranjaOrdinaria.Crear(new TimeOnly(10, 0), new TimeOnly(18, 0));

        segunda.SeSolapaCon(primera).Should().BeTrue();
    }

    [Fact]
    public void EmpiezaA_EsTrue_CuandoLaHoraCoincideConElInicioDeLaFranja()
    {
        var franja = FranjaOrdinaria.Crear(new TimeOnly(22, 0), new TimeOnly(6, 0));

        franja.EmpiezaA(new TimeOnly(22, 0)).Should().BeTrue();
    }

    [Fact]
    public void EmpiezaA_EsFalse_CuandoLaHoraNoCoincideConElInicioDeLaFranja()
    {
        var franja = FranjaOrdinaria.Crear(new TimeOnly(22, 0), new TimeOnly(6, 0));

        franja.EmpiezaA(new TimeOnly(23, 0)).Should().BeFalse();
    }

    // Overload usado por Apply(DescansoAgregado)/Apply(ExtraAgregado) para localizar la franja
    // original a reemplazar a partir de la franja RESULTANTE que trae el evento.
    [Fact]
    public void EmpiezaALaMismaHoraQue_EsTrue_CuandoOtraFranjaComienzaALaMismaHora()
    {
        var franja = FranjaOrdinaria.Crear(new TimeOnly(22, 0), new TimeOnly(6, 0));
        var franjaConDescanso = franja.ConDescanso(new TimeOnly(2, 0), new TimeOnly(2, 30));

        franja.EmpiezaALaMismaHoraQue(franjaConDescanso).Should().BeTrue();
    }

    [Fact]
    public void EmpiezaALaMismaHoraQue_EsFalse_CuandoOtraFranjaComienzaAOtraHora()
    {
        var primera = FranjaOrdinaria.Crear(new TimeOnly(6, 0), new TimeOnly(14, 0));
        var segunda = FranjaOrdinaria.Crear(new TimeOnly(14, 0), new TimeOnly(22, 0));

        primera.EmpiezaALaMismaHoraQue(segunda).Should().BeFalse();
    }

    [Fact]
    public void SinDescanso_RetornaFranjaSinLaHija_CuandoOtroDescansoQuedaEnLaFranja()
    {
        var franja = FranjaOrdinaria.Crear(new TimeOnly(22, 0), new TimeOnly(6, 0))
            .ConDescanso(new TimeOnly(23, 0), new TimeOnly(23, 30))
            .ConDescanso(new TimeOnly(2, 0), new TimeOnly(2, 30));

        var resultado = franja.SinDescanso(new TimeOnly(23, 0));

        resultado!.ToString().Should().Be("22:00-06:00+1, descanso 02:00+1-02:30+1");
    }

    [Fact]
    public void SinDescanso_RetornaNull_CuandoNingunDescansoEmpiezaAEsaHora()
    {
        var franja = FranjaOrdinaria.Crear(new TimeOnly(22, 0), new TimeOnly(6, 0))
            .ConDescanso(new TimeOnly(23, 0), new TimeOnly(23, 30))
            .ConDescanso(new TimeOnly(2, 0), new TimeOnly(2, 30));

        franja.SinDescanso(new TimeOnly(23, 15)).Should().BeNull();
    }

    // La hija a esa hora existe, pero es un descanso, no un extra.
    [Fact]
    public void SinExtra_RetornaNull_CuandoLaHijaAEsaHoraEsUnDescanso()
    {
        var franja = FranjaOrdinaria.Crear(new TimeOnly(22, 0), new TimeOnly(6, 0))
            .ConDescanso(new TimeOnly(23, 0), new TimeOnly(23, 30))
            .ConDescanso(new TimeOnly(2, 0), new TimeOnly(2, 30));

        franja.SinExtra(new TimeOnly(23, 0)).Should().BeNull();
    }

    [Fact]
    public void SinDescanso_DejaLaFranjaOriginalIntacta_CuandoQuitaUnaHija()
    {
        var franja = FranjaOrdinaria.Crear(new TimeOnly(22, 0), new TimeOnly(6, 0))
            .ConDescanso(new TimeOnly(23, 0), new TimeOnly(23, 30))
            .ConDescanso(new TimeOnly(2, 0), new TimeOnly(2, 30));

        franja.SinDescanso(new TimeOnly(23, 0));

        franja.ToString().Should().Be(
            "22:00-06:00+1, descanso 23:00-23:30, descanso 02:00+1-02:30+1");
    }

    [Fact]
    public void ConSede_AsignaLaSedeNueva_YConservaLasHijasExistentes()
    {
        var franja = FranjaOrdinaria.Crear(new TimeOnly(14, 0), new TimeOnly(22, 0))
            .ConDescanso(new TimeOnly(18, 0), new TimeOnly(18, 30));

        var conSede = franja.ConSede(new SedeProgramada("SEDE-CHAPINERO", "Chapinero"));

        conSede.ToString().Should().Be(
            "14:00-22:00, descanso 18:00-18:30");
    }

    [Fact]
    public void ConSede_QuitaElLabelDeSede_CuandoRecibeNull()
    {
        var franja = FranjaOrdinaria.Crear(
            new TimeOnly(14, 0), new TimeOnly(22, 0),
            sede: new SedeProgramada("SEDE-SUBA", "Suba"));

        var sinSede = franja.ConSede(null);

        sinSede.ToString().Should().Be("14:00-22:00");
    }

    [Fact]
    public void ConSede_LanzaExcepcion_CuandoLaSedeNuevaEsIncompleta()
    {
        var franja = FranjaOrdinaria.Crear(new TimeOnly(14, 0), new TimeOnly(22, 0));

        var act = () => franja.ConSede(new SedeProgramada("", "Chapinero"));

        act.Should().ThrowExactly<ArgumentException>()
            .WithMessage($"*{FranjaOrdinaria.Mensajes.SedeIncompleta}*");
    }

    [Fact]
    public void TieneSedePrearmada_EsTrue_CuandoLaFranjaTraeSede()
    {
        var franja = FranjaOrdinaria.Crear(
            new TimeOnly(14, 0), new TimeOnly(22, 0),
            sede: new SedeProgramada("SEDE-SUBA", "Suba"));

        franja.TieneSedePrearmada().Should().BeTrue();
    }

    [Fact]
    public void TieneSedePrearmada_EsFalse_CuandoLaFranjaNoTraeSede()
    {
        var franja = FranjaOrdinaria.Crear(new TimeOnly(14, 0), new TimeOnly(22, 0));

        franja.TieneSedePrearmada().Should().BeFalse();
    }
}
