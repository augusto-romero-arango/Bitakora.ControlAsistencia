// Invocacion directa de los metodos estaticos de la proyeccion; oraculos armados a mano (MEF-ADR-0002).
using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;
using Bitakora.ControlAsistencia.Projections.ControlHoras;
using Bitakora.ControlAsistencia.ReadModels.ControlHoras;

namespace Bitakora.ControlAsistencia.Projections.Tests.ControlHoras;

public class AdvertenciasProgramacionSemanalProjectionTests
{
    private static readonly DateOnly Lunes = new(2026, 10, 5);
    private static readonly ColaboradorProgramado Ana = new("CC-1", "EMP-001", "Ana Ramirez");
    private static readonly Guid AusenciaA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid AusenciaB = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly JornadaProgramada JornadaA = new(Guid.Parse("aaaaaaaa-0000-0000-0000-000000000000"), 2640, 600, 240, 1);
    private static readonly JornadaProgramada JornadaB = new(Guid.Parse("bbbbbbbb-0000-0000-0000-000000000000"), 2400, 540, 240, 1);

    private static DateOnly Dia(int offset) => Lunes.AddDays(offset);
    private static string Stream(DateOnly f) => $"cd:EMP-001:{f:yyyyMMdd}";

    private static FranjaProgramada Franja(int hi, int hf) =>
        new(new TimeOnly(hi, 0), new TimeOnly(hf, 0), 0, [], [], "");

    private static TurnoDiario Manana() => new("Manana", [Franja(6, 14)], "");
    private static TurnoDiario Tarde() => new("Tarde", [Franja(14, 20)], "");
    private static TurnoDiario Libre() => new("Libre", [], "");

    private static TurnoDiarioAsignado Asigna(int offset, TurnoDiario turno, JornadaProgramada? jornada = null, string nombre = "Ana Ramirez") =>
        new(Stream(Dia(offset)), new ColaboradorProgramado("CC-1", "EMP-001", nombre), Dia(offset), turno, Guid.NewGuid(), jornada);

    private static TurnoDiarioCancelado Cancela(int offset) =>
        TurnoDiarioCancelado.Crear(Stream(Dia(offset)), Ana, Dia(offset), Guid.NewGuid());

    private static AusenciaDiariaAsignada Ausenta(int offset, Guid id, string motivo = "Vacaciones") =>
        AusenciaDiariaAsignada.Crear(Stream(Dia(offset)), Ana, Dia(offset), id, motivo);

    private static CancelacionAusenciaDiariaRegistrada CancelaAusencia(int offset, Guid id) =>
        CancelacionAusenciaDiariaRegistrada.Crear(Stream(Dia(offset)), id, Dia(offset));

    private static AdvertenciasProgramacionSemanal Aplica(AdvertenciasProgramacionSemanal? v, params object[] eventos)
    {
        foreach (var e in eventos)
        {
            v = e switch
            {
                TurnoDiarioAsignado a => v is null ? AdvertenciasProgramacionSemanalProjection.Create(a) : AdvertenciasProgramacionSemanalProjection.Apply(a, v),
                AusenciaDiariaAsignada a => v is null ? AdvertenciasProgramacionSemanalProjection.Create(a) : AdvertenciasProgramacionSemanalProjection.Apply(a, v),
                TurnoDiarioCancelado c => v is null ? AdvertenciasProgramacionSemanalProjection.Create(c) : AdvertenciasProgramacionSemanalProjection.Apply(c, v),
                CancelacionAusenciaDiariaRegistrada c => v is null ? AdvertenciasProgramacionSemanalProjection.Create(c) : AdvertenciasProgramacionSemanalProjection.Apply(c, v),
                CoberturaVecinaActualizada c => v is null ? AdvertenciasProgramacionSemanalProjection.Create(c) : AdvertenciasProgramacionSemanalProjection.Apply(c, v),
                CierreDeLaVisperaActualizado c => v is null ? AdvertenciasProgramacionSemanalProjection.Create(c) : AdvertenciasProgramacionSemanalProjection.Apply(c, v),
                _ => throw new InvalidOperationException()
            };
        }
        return v!;
    }

    private static CasillaDia Casilla(AdvertenciasProgramacionSemanal v, int offset) => v.Casillas[offset];

    [Fact]
    public void Clave_AgrupaLunesYDomingoEnLaMismaSemana()
    {
        AdvertenciasProgramacionSemanalProjection.Clave("EMP-001", new DateOnly(2026, 10, 5)).Should().Be("EMP-001:2026-W41");
        AdvertenciasProgramacionSemanalProjection.Clave("EMP-001", new DateOnly(2026, 10, 11)).Should().Be("EMP-001:2026-W41");
    }

    [Fact]
    public void Clave_DiferenciaColaboradores()
    {
        AdvertenciasProgramacionSemanalProjection.Clave("EMP-002", new DateOnly(2026, 10, 5)).Should().Be("EMP-002:2026-W41");
    }

    [Fact]
    public void Clave_AgrupaElCambioDeAnio_EnLaSemana53()
    {
        AdvertenciasProgramacionSemanalProjection.Clave("EMP-001", new DateOnly(2026, 12, 28)).Should().Be("EMP-001:2026-W53");
        AdvertenciasProgramacionSemanalProjection.Clave("EMP-001", new DateOnly(2027, 1, 3)).Should().Be("EMP-001:2026-W53");
    }

    [Fact]
    public void ClaveDesdeStream_ExtraeElCodigoDelStreamKey()
    {
        AdvertenciasProgramacionSemanalProjection.ClaveDesdeStream("cd:EMP-001:20260805", new DateOnly(2026, 10, 7))
            .Should().Be("EMP-001:2026-W41");
    }

    [Fact]
    public void Create_ProyectaEncabezadoYSieteCasillas_DesdeTurnoDiarioAsignado()
    {
        var v = AdvertenciasProgramacionSemanalProjection.Create(Asigna(0, Manana(), JornadaA));

        v.Id.Should().Be("EMP-001:2026-W41");
        v.CodigoColaborador.Should().Be("EMP-001");
        v.NombreCompleto.Should().Be("Ana Ramirez");
        v.AnioIso.Should().Be(2026);
        v.NumeroSemana.Should().Be(41);
        v.Lunes.Should().Be(new DateOnly(2026, 10, 5));
        v.Domingo.Should().Be(new DateOnly(2026, 10, 11));
        v.Casillas.Should().HaveCount(7);
        v.Casillas.Select(c => c.Fecha).Should().Equal(Enumerable.Range(0, 7).Select(Dia));
        Casilla(v, 0).Tipo.Should().Be(TipoCasilla.Trabajo);
        Casilla(v, 0).NombreTurno.Should().Be("Manana");
        Casilla(v, 0).MinutosOrdinarios.Should().Be(480);
        v.Casillas.Skip(1).Should().OnlyContain(c => c.Tipo == TipoCasilla.SinProgramar && c.MinutosOrdinarios == 0 && c.NombreTurno == "");
        v.MinutosOrdinariosProgramados.Should().Be(480);
        v.DiasSinProgramar.Should().Be(6);
        v.TieneAusencias.Should().BeFalse();
        v.EsJuzgable.Should().BeFalse();
        v.Jornada.Should().Be(new JornadaAplicada(JornadaA.JornadaId, 2640, 600, 240, 1));
    }

    [Fact]
    public void Create_ProyectaDescansoConCeroMinutos_CuandoTurnoSinFranjas()
    {
        var v = AdvertenciasProgramacionSemanalProjection.Create(Asigna(2, Libre()));

        Casilla(v, 2).Tipo.Should().Be(TipoCasilla.Descanso);
        Casilla(v, 2).NombreTurno.Should().Be("Libre");
        Casilla(v, 2).MinutosOrdinarios.Should().Be(0);
        v.DiasSinProgramar.Should().Be(6);
    }

    [Fact]
    public void Apply_AgregaDomingo_CuandoLaSemanaYaTieneElLunes()
    {
        var v = Aplica(null, Asigna(0, Manana()), Asigna(6, Tarde()));

        Casilla(v, 6).Tipo.Should().Be(TipoCasilla.Trabajo);
        Casilla(v, 6).MinutosOrdinarios.Should().Be(360);
        v.MinutosOrdinariosProgramados.Should().Be(840);
        v.DiasSinProgramar.Should().Be(5);
    }

    [Fact]
    public void Apply_ReemplazaLaCasillaYRecalculaElTotal_CuandoSeReasignaElMismoDia()
    {
        var v = Aplica(null, Asigna(0, Manana()), Asigna(0, Tarde()));

        Casilla(v, 0).NombreTurno.Should().Be("Tarde");
        Casilla(v, 0).MinutosOrdinarios.Should().Be(360);
        v.MinutosOrdinariosProgramados.Should().Be(360);
        v.DiasSinProgramar.Should().Be(6);
    }

    [Fact]
    public void Apply_RefrescaNombreCompleto_CuandoLlegaOtroEvento()
    {
        var v = Aplica(null, Asigna(0, Manana()), Asigna(1, Manana(), nombre: "Ana M. Ramirez"));

        v.NombreCompleto.Should().Be("Ana M. Ramirez");
    }

    [Fact]
    public void Apply_DejaSinProgramar_CuandoTurnoDiarioCancelado()
    {
        var v = Aplica(null, Asigna(0, Manana()), Asigna(1, Manana()), Cancela(0));

        Casilla(v, 0).Tipo.Should().Be(TipoCasilla.SinProgramar);
        Casilla(v, 0).MinutosOrdinarios.Should().Be(0);
        Casilla(v, 0).NombreTurno.Should().Be("");
        v.MinutosOrdinariosProgramados.Should().Be(480);
        v.DiasSinProgramar.Should().Be(6);
    }

    [Fact]
    public void Apply_BorraElDocumento_CuandoLasSieteCasillasQuedanSinProgramar()
    {
        var v = AdvertenciasProgramacionSemanalProjection.Create(Asigna(0, Manana()));

        AdvertenciasProgramacionSemanalProjection.Apply(Cancela(0), v).Should().BeNull();
    }

    [Fact]
    public void Create_NoCreaDocumento_CuandoTurnoDiarioCanceladoEsElPrimerEvento()
    {
        AdvertenciasProgramacionSemanalProjection.Create(Cancela(0)).Should().BeNull();
    }

    [Fact]
    public void Create_NoCreaDocumento_CuandoCancelacionAusenciaEsElPrimerEvento()
    {
        AdvertenciasProgramacionSemanalProjection.Create(CancelaAusencia(0, AusenciaA)).Should().BeNull();
    }

    [Fact]
    public void Apply_CubreElTurnoConAusenciaSinMinutos_CuandoAusenciaDiariaAsignada()
    {
        var v = Aplica(null, Asigna(0, Manana()), Asigna(1, Manana()), Ausenta(0, AusenciaA, "Incapacidad medica"));

        Casilla(v, 0).Tipo.Should().Be(TipoCasilla.Ausencia);
        Casilla(v, 0).MotivoAusencia.Should().Be("Incapacidad medica");
        Casilla(v, 0).MinutosOrdinarios.Should().Be(0);
        Casilla(v, 0).AusenciaId.Should().Be(AusenciaA);
        Casilla(v, 0).TurnoCubierto.Should().Be(new TurnoCubiertoSemana("Manana", 480, false));
        v.TieneAusencias.Should().BeTrue();
        v.EsJuzgable.Should().BeFalse();
        v.MinutosOrdinariosProgramados.Should().Be(480);
    }

    [Fact]
    public void Create_ProyectaAusenciaSinTurnoCubierto_CuandoAusenciaEsElPrimerEvento()
    {
        var v = AdvertenciasProgramacionSemanalProjection.Create(Ausenta(3, AusenciaA));

        Casilla(v, 3).Tipo.Should().Be(TipoCasilla.Ausencia);
        Casilla(v, 3).MinutosOrdinarios.Should().Be(0);
        Casilla(v, 3).TurnoCubierto.Should().BeNull();
        v.TieneAusencias.Should().BeTrue();
        v.DiasSinProgramar.Should().Be(6);
    }

    [Fact]
    public void Apply_RestauraElTurno_CuandoSeCancelaLaAusenciaVigente()
    {
        var v = Aplica(null, Asigna(0, Manana()), Asigna(1, Manana()), Ausenta(0, AusenciaA), CancelaAusencia(0, AusenciaA));

        Casilla(v, 0).Tipo.Should().Be(TipoCasilla.Trabajo);
        Casilla(v, 0).NombreTurno.Should().Be("Manana");
        Casilla(v, 0).MinutosOrdinarios.Should().Be(480);
        Casilla(v, 0).MotivoAusencia.Should().BeNull();
        Casilla(v, 0).AusenciaId.Should().BeNull();
        Casilla(v, 0).TurnoCubierto.Should().BeNull();
        v.TieneAusencias.Should().BeFalse();
        v.MinutosOrdinariosProgramados.Should().Be(960);
    }

    [Fact]
    public void Apply_RestauraDescanso_CuandoElTurnoCubiertoEraDescanso()
    {
        var v = Aplica(null, Asigna(0, Libre()), Ausenta(0, AusenciaA), CancelaAusencia(0, AusenciaA));

        Casilla(v, 0).Tipo.Should().Be(TipoCasilla.Descanso);
        Casilla(v, 0).NombreTurno.Should().Be("Libre");
    }

    [Fact]
    public void Apply_NoCambiaNada_CuandoSeCancelaUnaAusenciaQueNoEsLaVigente()
    {
        var previa = Aplica(null, Asigna(0, Manana()), Ausenta(0, AusenciaA));

        var v = AdvertenciasProgramacionSemanalProjection.Apply(CancelaAusencia(0, AusenciaB), previa);

        v.Should().NotBeNull();
        Casilla(v!, 0).Tipo.Should().Be(TipoCasilla.Ausencia);
        Casilla(v!, 0).AusenciaId.Should().Be(AusenciaA);
        v!.TieneAusencias.Should().BeTrue();
    }

    [Fact]
    public void Apply_DejaSinProgramar_CuandoSeCancelaAusenciaSinTurnoDebajo()
    {
        var v = Aplica(null, Asigna(1, Manana()), Ausenta(0, AusenciaA), CancelaAusencia(0, AusenciaA));

        Casilla(v, 0).Tipo.Should().Be(TipoCasilla.SinProgramar);
        Casilla(v, 0).MotivoAusencia.Should().BeNull();
        v.TieneAusencias.Should().BeFalse();
    }

    [Fact]
    public void Apply_BorraElDocumento_CuandoSeCancelaLaUnicaAusenciaSinTurnoDebajo()
    {
        var v = AdvertenciasProgramacionSemanalProjection.Create(Ausenta(0, AusenciaA));

        AdvertenciasProgramacionSemanalProjection.Apply(CancelaAusencia(0, AusenciaA), v).Should().BeNull();
    }

    [Fact]
    public void Apply_ActualizaSoloElTurnoCubierto_CuandoSeAsignaTurnoConAusenciaVigente()
    {
        var v = Aplica(null, Asigna(0, Manana()), Ausenta(0, AusenciaA), Asigna(0, Tarde()));

        Casilla(v, 0).Tipo.Should().Be(TipoCasilla.Ausencia);
        Casilla(v, 0).MinutosOrdinarios.Should().Be(0);
        Casilla(v, 0).TurnoCubierto.Should().Be(new TurnoCubiertoSemana("Tarde", 360, false));
        v.MinutosOrdinariosProgramados.Should().Be(0);
    }

    [Fact]
    public void Apply_DescartaElTurnoCubierto_CuandoSeCancelaTurnoConAusenciaVigente()
    {
        var v = Aplica(null, Asigna(0, Manana()), Ausenta(0, AusenciaA), Cancela(0));

        Casilla(v, 0).Tipo.Should().Be(TipoCasilla.Ausencia);
        Casilla(v, 0).TurnoCubierto.Should().BeNull();
    }

    [Fact]
    public void Apply_EsJuzgable_CuandoLosSieteDiasEstanProgramadosSinAusencias()
    {
        var v = Aplica(null,
            Asigna(0, Manana()), Asigna(1, Manana()), Asigna(2, Manana()), Asigna(3, Manana()),
            Asigna(4, Tarde()), Asigna(5, Libre()), Asigna(6, Libre()));

        v.EsJuzgable.Should().BeTrue();
        v.DiasSinProgramar.Should().Be(0);
        v.TieneAusencias.Should().BeFalse();
        v.MinutosOrdinariosProgramados.Should().Be(4 * 480 + 360);
    }

    [Fact]
    public void Apply_ConservaLaUltimaJornada_CuandoLlegaUnaNueva()
    {
        var v = Aplica(null, Asigna(0, Manana(), JornadaA), Asigna(1, Manana(), JornadaB));

        v.Jornada.Should().Be(new JornadaAplicada(JornadaB.JornadaId, 2400, 540, 240, 1));
    }

    [Fact]
    public void Apply_NoSobrescribeLaJornada_CuandoElTurnoNoTraeJornada()
    {
        var v = Aplica(null, Asigna(0, Manana(), JornadaA), Asigna(1, Manana(), JornadaB), Asigna(2, Tarde(), null));

        v.Jornada.Should().Be(new JornadaAplicada(JornadaB.JornadaId, 2400, 540, 240, 1));
        Casilla(v, 2).MinutosOrdinarios.Should().Be(360);
        v.MinutosOrdinariosProgramados.Should().Be(480 + 480 + 360);
        v.DiasSinProgramar.Should().Be(4);
    }

    [Fact]
    public void Create_ConstruyeLaVistaSinJornada_CuandoNingunDiaTrae()
    {
        var v = Aplica(null, Asigna(0, Manana()), Asigna(1, Tarde()));

        v.Jornada.Should().BeNull();
        v.MinutosOrdinariosProgramados.Should().Be(840);
        v.Casillas.Should().HaveCount(7);
    }

    private static readonly JornadaProgramada JornadaC = new(Guid.Parse("cccccccc-0000-0000-0000-000000000000"), 2520, 480, 240, 1);
    private static readonly JornadaProgramada JornadaD = new(Guid.Parse("dddddddd-0000-0000-0000-000000000000"), 2520, 600, 240, 1);
    private static TurnoDiario DiezHoras() => new("Largo", [Franja(6, 16)], "");

    [Fact]
    public void Apply_AgregaSuperaTopeDiarioEnLaCasilla_CuandoTrabajoSuperaElTope()
    {
        var v = Aplica(null, Asigna(0, DiezHoras(), JornadaC));

        Casilla(v, 0).Advertencias.Should().Equal(new AdvertenciaDiaria(TipoAdvertenciaDiaria.SuperaTopeDiario, 120));
        v.AdvertenciasSemanales.Should().BeEmpty();
        v.TieneAdvertencias.Should().BeTrue();
    }

    [Fact]
    public void Apply_MideDiaSinJornadaContraLaJornadaDeLaSemana_CuandoElDiaLlegaConNull()
    {
        var v = Aplica(null, Asigna(1, Manana(), JornadaC), Asigna(0, DiezHoras()));

        Casilla(v, 0).Advertencias.Should().Equal(new AdvertenciaDiaria(TipoAdvertenciaDiaria.SuperaTopeDiario, 120));
    }

    [Fact]
    public void Apply_NoAgregaAdvertencias_CuandoSemanaSinJornada()
    {
        var v = Aplica(null, Asigna(0, DiezHoras()));

        Casilla(v, 0).Advertencias.Should().BeEmpty();
        v.AdvertenciasSemanales.Should().BeEmpty();
        v.TieneAdvertencias.Should().BeFalse();
    }

    [Fact]
    public void Apply_ReMideLaSemana_CuandoLlegaUnaJornadaNueva()
    {
        var v = Aplica(null, Asigna(0, DiezHoras(), JornadaC), Asigna(1, Manana(), JornadaD));

        Casilla(v, 0).Advertencias.Should().BeEmpty();
        v.TieneAdvertencias.Should().BeFalse();
    }

    [Fact]
    public void Apply_AgregaSuperaHorasSemanales_CuandoSemanaJuzgableSuperaElTotal()
    {
        var v = Aplica(null,
            Asigna(0, Manana(), JornadaC), Asigna(1, Manana()), Asigna(2, Manana()), Asigna(3, Manana()),
            Asigna(4, Manana()), Asigna(5, Manana()), Asigna(6, Libre()));

        v.EsJuzgable.Should().BeTrue();
        v.AdvertenciasSemanales.Should().Equal(new AdvertenciaSemanal(TipoAdvertenciaSemanal.SuperaHorasSemanales, 360));
        v.TieneAdvertencias.Should().BeTrue();
    }

    [Fact]
    public void Apply_LimpiaAdvertencias_CuandoSeCorrigeElDia()
    {
        var v = Aplica(null, Asigna(0, DiezHoras(), JornadaC), Asigna(0, Manana()));

        Casilla(v, 0).Advertencias.Should().BeEmpty();
        v.TieneAdvertencias.Should().BeFalse();
    }

    private static TurnoDiario Nocturno() => new("Noche", [Franja(17, 1)], "");

    private static CoberturaVecinaActualizada Vispera(int offset, DateTime? limite) =>
        CoberturaVecinaActualizada.Crear(Stream(Dia(offset)), DiaVecino.Vispera, limite);

    // Fin 01:00 + 4 h = 05:00 del dia del offset.
    private static DateTime CincoAm(int offset) => Dia(offset).ToDateTime(new TimeOnly(5, 0));

    private static readonly AdvertenciaDiaria Solape60 = new(TipoAdvertenciaDiaria.PorDebajoDelDescansoEntreTurnos, 60);

    [Fact]
    public void Apply_AgregaPorDebajoDelDescanso_CuandoLimiteDeLaVisperaSuperaElInicioDeCobertura()
    {
        var v = Aplica(null, Asigna(0, Nocturno()), Asigna(1, Manana()), Vispera(1, CincoAm(1)));

        Casilla(v, 1).Advertencias.Should().Equal(Solape60);
        Casilla(v, 0).Advertencias.Should().BeEmpty();
        v.TieneAdvertencias.Should().BeTrue();
    }

    [Fact]
    public void Apply_AgregaPorDebajoDelDescansoEnElLunes_CuandoLaVisperaEsElDomingoAnterior()
    {
        var v = Aplica(null, Asigna(0, Manana()), Vispera(0, CincoAm(0)));

        Casilla(v, 0).Advertencias.Should().Equal(Solape60);
        v.TieneAdvertencias.Should().BeTrue();
    }

    [Fact]
    public void Apply_NoAgregaAdvertencia_CuandoElDescansoEsDeSeisHorasOMas()
    {
        var v = Aplica(null, Asigna(1, new TurnoDiario("Tarde7", [Franja(7, 16)], "")), Vispera(1, CincoAm(1)));

        Casilla(v, 1).Advertencias.Should().BeEmpty();
    }

    [Fact]
    public void Apply_NoAgregaAdvertencia_CuandoLaVisperaNoTieneCobertura()
    {
        var v = Aplica(null, Asigna(1, Manana()), Vispera(1, null));

        Casilla(v, 1).Advertencias.Should().BeEmpty();
        v.TieneAdvertencias.Should().BeFalse();
    }

    [Fact]
    public void Apply_NoAgregaAdvertencia_CuandoElDiaEsDeDescanso()
    {
        var v = Aplica(null, Asigna(1, Libre()), Vispera(1, CincoAm(1)));

        Casilla(v, 1).Advertencias.Should().BeEmpty();
    }

    [Fact]
    public void Apply_NoAgregaAdvertencia_CuandoElDiaTieneAusencia()
    {
        var v = Aplica(null, Asigna(1, Manana()), Vispera(1, CincoAm(1)), Ausenta(1, AusenciaA));

        Casilla(v, 1).Advertencias.Should().BeEmpty();
    }

    [Fact]
    public void Apply_IgnoraElLimiteDelDiaSiguiente_CuandoElVecinoEsDiaSiguiente()
    {
        var v = Aplica(null, Asigna(1, Manana()),
            CoberturaVecinaActualizada.Crear(Stream(Dia(1)), DiaVecino.DiaSiguiente, CincoAm(1)));

        Casilla(v, 1).Advertencias.Should().BeEmpty();
    }

    [Fact]
    public void Apply_ConservaElLimiteDeLaVispera_CuandoSeCancelaYReasignaElTurnoDelDia()
    {
        var v = Aplica(null, Asigna(3, Manana()), Asigna(1, Manana()), Vispera(1, CincoAm(1)),
            Cancela(1), Asigna(1, Manana()));

        Casilla(v, 1).Advertencias.Should().Equal(Solape60);
    }

    [Fact]
    public void Apply_ConservaElLimiteDeLaVispera_CuandoSeAsignaYCancelaUnaAusencia()
    {
        var v = Aplica(null, Asigna(1, Manana()), Vispera(1, CincoAm(1)),
            Ausenta(1, AusenciaA), CancelaAusencia(1, AusenciaA));

        Casilla(v, 1).Advertencias.Should().Equal(Solape60);
    }

    [Fact]
    public void Apply_AgregaPorDebajoDelDescanso_CuandoElTurnoSeAsignaDuranteLaAusenciaYLuegoSeCancelaLaAusencia()
    {
        var v = Aplica(null, Ausenta(1, AusenciaA), Asigna(1, Manana()), Vispera(1, CincoAm(1)),
            CancelaAusencia(1, AusenciaA));

        Casilla(v, 1).Tipo.Should().Be(TipoCasilla.Trabajo);
        Casilla(v, 1).Advertencias.Should().Equal(Solape60);
    }

    [Fact]
    public void Apply_RecalculaLaAdvertencia_CuandoLlegaUnLimiteMayor()
    {
        var v = Aplica(null, Asigna(1, Manana()), Vispera(1, CincoAm(1)), Vispera(1, CincoAm(1).AddMinutes(30)));

        Casilla(v, 1).Advertencias.Should().Equal(new AdvertenciaDiaria(TipoAdvertenciaDiaria.PorDebajoDelDescansoEntreTurnos, 90));
    }

    [Fact]
    public void Apply_ApagaLaAdvertencia_CuandoLlegaUnLimiteMenorQueElInicioDeCobertura()
    {
        var v = Aplica(null, Asigna(1, Manana()), Vispera(1, CincoAm(1)), Vispera(1, Dia(1).ToDateTime(new TimeOnly(3, 0))));

        Casilla(v, 1).Advertencias.Should().BeEmpty();
    }

    [Fact]
    public void Apply_ApagaLaAdvertencia_CuandoLlegaUnLimiteNulo()
    {
        var v = Aplica(null, Asigna(1, Manana()), Vispera(1, CincoAm(1)), Vispera(1, null));

        Casilla(v, 1).Advertencias.Should().BeEmpty();
        v.TieneAdvertencias.Should().BeFalse();
    }

    [Fact]
    public void Apply_AgregaPorDebajoDelDescanso_CuandoLaSemanaNoTieneJornada()
    {
        var v = Aplica(null, Asigna(1, Manana()), Vispera(1, CincoAm(1)));

        v.Jornada.Should().BeNull();
        v.EsJuzgable.Should().BeFalse();
        v.AdvertenciasSemanales.Should().BeEmpty();
        Casilla(v, 1).Advertencias.Should().Equal(Solape60);
        v.TieneAdvertencias.Should().BeTrue();
    }

    [Fact]
    public void Apply_LlevaDosAdvertenciasDiarias_CuandoElDiaSuperaElTopeYSolapaConLaVispera()
    {
        var v = Aplica(null, Asigna(1, DiezHoras(), JornadaC), Vispera(1, CincoAm(1)));

        Casilla(v, 1).Advertencias.Should().BeEquivalentTo(
        [
            new AdvertenciaDiaria(TipoAdvertenciaDiaria.SuperaTopeDiario, 120),
            Solape60
        ]);
    }

    [Fact]
    public void Create_NoCreaDocumento_CuandoCoberturaVecinaEsElPrimerEvento()
    {
        AdvertenciasProgramacionSemanalProjection.Create(Vispera(1, CincoAm(1))).Should().BeNull();
    }

    private static TurnoDiario Abierto() => new("Abierto", [Franja(4, 6)], "", EsHorarioAbierto: true);

    private static DateTime DiezAm(int offset) => Dia(offset).ToDateTime(new TimeOnly(10, 0));

    [Fact]
    public void Apply_NoAdvierteDescansoEntreTurnos_CuandoElDiaEsDeHorarioAbierto()
    {
        var v = Aplica(null, Asigna(0, NocheVeintidos()), Asigna(1, Abierto()), Vispera(1, DiezAm(1)));

        Casilla(v, 1).EsHorarioAbierto.Should().BeTrue();
        Casilla(v, 1).Advertencias.Should().BeEmpty();
    }

    [Fact]
    public void Apply_AdvierteDescansoEntreTurnos_CuandoElDiaNoEsDeHorarioAbierto()
    {
        var v = Aplica(null, Asigna(0, NocheVeintidos()), Asigna(1, Manana()), Vispera(1, DiezAm(1)));

        Casilla(v, 1).EsHorarioAbierto.Should().BeFalse();
        Casilla(v, 1).Advertencias.Should().Equal(new AdvertenciaDiaria(TipoAdvertenciaDiaria.PorDebajoDelDescansoEntreTurnos, 360));
    }

    [Fact]
    public void Apply_AdvierteDescansoEntreTurnos_CuandoLaVisperaEsDeHorarioAbiertoYSuLimiteInvadeElDia()
    {
        var v = Aplica(null, Asigna(0, Abierto()), Asigna(1, Manana()), Vispera(1, CincoAm(1)));

        Casilla(v, 1).Advertencias.Should().Equal(Solape60);
    }

    [Fact]
    public void Apply_ApagaLaMarca_CuandoReasignacionEsComun()
    {
        var v = Aplica(null, Asigna(0, Abierto()), Asigna(0, Manana()));

        Casilla(v, 0).EsHorarioAbierto.Should().BeFalse();
    }

    [Fact]
    public void Apply_GuardaLaMarcaEnTurnoCubierto_CuandoAusenciaAsignadaSobreHorarioAbierto()
    {
        var v = Aplica(null, Asigna(0, Abierto()), Ausenta(0, AusenciaA));

        Casilla(v, 0).EsHorarioAbierto.Should().BeFalse();
        Casilla(v, 0).TurnoCubierto!.EsHorarioAbierto.Should().BeTrue();
    }

    [Fact]
    public void Apply_GuardaLaMarcaEnTurnoCubierto_CuandoTurnoDiarioAsignadoConAusenciaVigente()
    {
        var v = Aplica(null, Ausenta(0, AusenciaA), Asigna(0, Abierto()));

        Casilla(v, 0).EsHorarioAbierto.Should().BeFalse();
        Casilla(v, 0).TurnoCubierto!.EsHorarioAbierto.Should().BeTrue();
    }

    [Fact]
    public void Apply_RestableceLaMarcaYNoAdvierte_CuandoCancelacionDeAusenciaSobreHorarioAbierto()
    {
        var v = Aplica(null, Asigna(0, NocheVeintidos()), Asigna(1, Abierto()), Vispera(1, DiezAm(1)),
            Ausenta(1, AusenciaA), CancelaAusencia(1, AusenciaA));

        Casilla(v, 1).EsHorarioAbierto.Should().BeTrue();
        Casilla(v, 1).Advertencias.Should().BeEmpty();
    }

    [Fact]
    public void Deserializar_LeeFalse_CuandoElDocumentoNoTraeLaClaveEsHorarioAbierto()
    {
        var casilla = System.Text.Json.JsonSerializer.Deserialize<CasillaDia>(
            """{"Fecha":"2026-10-05","Tipo":0,"NombreTurno":"T","MinutosOrdinarios":0,"TurnoCubierto":{"NombreTurno":"C","MinutosOrdinarios":0,"EsDescanso":false}}""")!;

        casilla.EsHorarioAbierto.Should().BeFalse();
        casilla.TurnoCubierto!.EsHorarioAbierto.Should().BeFalse();
    }

    private static FranjaProgramada FranjaNoche(int hi, int hf) =>
        new(new TimeOnly(hi, 0), new TimeOnly(hf, 0), 1, [], [], "");

    private static TurnoDiario NocheVeintidos() => new("Noche22", [FranjaNoche(22, 6)], "");
    private static TurnoDiario FijoCuatro() => new("Fijo4", [Franja(4, 12)], "");
    private static TurnoDiario ContiguoSeis() => new("Seis", [Franja(6, 14)], "");

    private static CierreDeLaVisperaActualizado CierreVispera(int offset, DateTime? cierre) =>
        CierreDeLaVisperaActualizado.Crear(Stream(Dia(offset)), cierre);

    // Cierre de la Noche22 asignada el dia anterior: 06:00 del dia del offset.
    private static DateTime SeisAm(int offset) => Dia(offset).ToDateTime(new TimeOnly(6, 0));

    private static readonly AdvertenciaDiaria Solape120 = new(TipoAdvertenciaDiaria.SeSolapaConTurnoDeLaVispera, 120);

    [Fact]
    public void Apply_AgregaSeSolapaConTurnoDeLaVispera_CuandoElCierreDeLaVisperaSuperaLaAperturaDelTurno()
    {
        var v = Aplica(null, Asigna(1, NocheVeintidos()), Asigna(2, FijoCuatro()), CierreVispera(2, SeisAm(2)));

        Casilla(v, 2).Advertencias.Should().Equal(Solape120);
        Casilla(v, 1).Advertencias.Should().BeEmpty();
        v.TieneAdvertencias.Should().BeTrue();
    }

    [Fact]
    public void Apply_AgregaSeSolapaConTurnoDeLaVisperaEnElLunes_CuandoLaVisperaEsElDomingoAnterior()
    {
        var v = Aplica(null, Asigna(0, FijoCuatro()), CierreVispera(0, SeisAm(0)));

        Casilla(v, 0).Advertencias.Should().Equal(Solape120);
        v.TieneAdvertencias.Should().BeTrue();
    }

    [Fact]
    public void Apply_ProyectaAperturaYCierreInternos_CuandoHaySolape()
    {
        var v = Aplica(null, Asigna(2, FijoCuatro()), CierreVispera(2, SeisAm(2)));

        Casilla(v, 2).Advertencias.Should().Equal(Solape120);
        Casilla(v, 2).CierreVentanaEntrada.Should().Be(Dia(2).ToDateTime(new TimeOnly(4, 0)));
        Casilla(v, 2).TieneVentanaDeEntrada.Should().BeFalse();
        Casilla(v, 2).CierreTurnoVispera.Should().Be(SeisAm(2));
    }

    [Fact]
    public void Apply_NoAgregaSolape_CuandoLosTurnosSonContiguos()
    {
        var v = Aplica(null, Asigna(2, ContiguoSeis()), CierreVispera(2, SeisAm(2)));

        Casilla(v, 2).Advertencias.Should().BeEmpty();
    }

    [Fact]
    public void Apply_NoAgregaSolape_CuandoElDiaEsDeDescanso()
    {
        var v = Aplica(null, Asigna(2, Libre()), CierreVispera(2, SeisAm(2)));

        Casilla(v, 2).Advertencias.Should().BeEmpty();
    }

    [Fact]
    public void Apply_NoAgregaSolape_CuandoElDiaTieneAusencia()
    {
        var v = Aplica(null, Asigna(2, FijoCuatro()), CierreVispera(2, SeisAm(2)), Ausenta(2, AusenciaA));

        Casilla(v, 2).Advertencias.Should().BeEmpty();
    }

    [Fact]
    public void Apply_NoAgregaSolape_CuandoElCierreEsNulo()
    {
        var v = Aplica(null, Asigna(2, FijoCuatro()), CierreVispera(2, null));

        Casilla(v, 2).Advertencias.Should().BeEmpty();
    }

    [Fact]
    public void Apply_ReemplazaElDescansoEntreTurnos_CuandoHaySolape()
    {
        var v = Aplica(null, Asigna(2, FijoCuatro()), Vispera(2, SeisAm(2).AddHours(4)), CierreVispera(2, SeisAm(2)));

        Casilla(v, 2).Advertencias.Should().Equal(Solape120);
    }

    [Fact]
    public void Apply_ReemplazaElDescansoEntreTurnos_SinImportarElOrdenDeLlegada()
    {
        var v = Aplica(null, Asigna(2, FijoCuatro()), CierreVispera(2, SeisAm(2)), Vispera(2, SeisAm(2).AddHours(4)));

        Casilla(v, 2).Advertencias.Should().Equal(Solape120);
    }

    [Fact]
    public void Apply_ConservaElDescansoEntreTurnos_CuandoNoHaySolape()
    {
        var v = Aplica(null, Asigna(2, new TurnoDiario("Tarde7", [Franja(5, 13)], "")),
            Vispera(2, Dia(2).ToDateTime(new TimeOnly(4, 0)).AddHours(2)), CierreVispera(2, Dia(2).ToDateTime(new TimeOnly(5, 0))));

        Casilla(v, 2).Advertencias.Should().Equal(new AdvertenciaDiaria(TipoAdvertenciaDiaria.PorDebajoDelDescansoEntreTurnos, 180));
        Casilla(v, 2).Advertencias.Should().NotContain(x => x.Tipo == TipoAdvertenciaDiaria.SeSolapaConTurnoDeLaVispera);
    }

    [Fact]
    public void Apply_ConservaElCierreDeLaVispera_CuandoSeCancelaYReasignaElTurnoDelDia()
    {
        var v = Aplica(null, Asigna(4, Manana()), Asigna(2, FijoCuatro()), CierreVispera(2, SeisAm(2)),
            Cancela(2), Asigna(2, FijoCuatro()));

        Casilla(v, 2).Advertencias.Should().Equal(Solape120);
    }

    [Fact]
    public void Apply_ConservaElCierreDeLaVispera_CuandoSeAsignaYCancelaUnaAusencia()
    {
        var v = Aplica(null, Asigna(2, FijoCuatro()), CierreVispera(2, SeisAm(2)),
            Ausenta(2, AusenciaA), CancelaAusencia(2, AusenciaA));

        Casilla(v, 2).Advertencias.Should().Equal(Solape120);
    }

    [Fact]
    public void Apply_RecalculaElSolape_CuandoLlegaUnCierreMayor()
    {
        var v = Aplica(null, Asigna(2, FijoCuatro()), CierreVispera(2, SeisAm(2)), CierreVispera(2, SeisAm(2).AddMinutes(30)));

        Casilla(v, 2).Advertencias.Should().Equal(new AdvertenciaDiaria(TipoAdvertenciaDiaria.SeSolapaConTurnoDeLaVispera, 150));
    }

    [Fact]
    public void Apply_ApagaElSolape_CuandoLlegaUnCierreMenorQueLaApertura()
    {
        var v = Aplica(null, Asigna(2, FijoCuatro()), CierreVispera(2, SeisAm(2)), CierreVispera(2, Dia(2).ToDateTime(new TimeOnly(3, 0))));

        Casilla(v, 2).Advertencias.Should().BeEmpty();
    }

    [Fact]
    public void Apply_ApagaElSolape_CuandoLlegaUnCierreNulo()
    {
        var v = Aplica(null, Asigna(2, FijoCuatro()), CierreVispera(2, SeisAm(2)), CierreVispera(2, null));

        Casilla(v, 2).Advertencias.Should().BeEmpty();
        v.TieneAdvertencias.Should().BeFalse();
    }

    [Fact]
    public void Apply_MuestraElSolape_CuandoLaSemanaNoTieneJornadaNiEsJuzgable()
    {
        var v = Aplica(null, Asigna(2, FijoCuatro()), CierreVispera(2, SeisAm(2)));

        v.Jornada.Should().BeNull();
        v.EsJuzgable.Should().BeFalse();
        v.AdvertenciasSemanales.Should().BeEmpty();
        Casilla(v, 2).Advertencias.Should().Equal(Solape120);
        v.TieneAdvertencias.Should().BeTrue();
    }

    [Fact]
    public void Create_NoCreaDocumento_CuandoCierreDeLaVisperaEsElPrimerEvento()
    {
        AdvertenciasProgramacionSemanalProjection.Create(CierreVispera(2, SeisAm(2))).Should().BeNull();
    }

    // --- Turno con ventana de entrada (#1229) ---

    private static DateTime En(int offset, int h) => Dia(offset).ToDateTime(new TimeOnly(h, 0));

    private static TurnoDiario ConVentana(int hi, int hf, int amplitudMin) =>
        new("Ventana", [Franja(hi, hf)], "", amplitudMin);

    private static AdvertenciaDiaria Descanso(int minutos) => new(TipoAdvertenciaDiaria.PorDebajoDelDescansoEntreTurnos, minutos);

    [Fact]
    public void Apply_NoAdvierte_CuandoElDescansoSeCumpleEntrandoMasTarde_CasoOcoa()
    {
        // Noche 22:00-06:00 y Abierto con franja 04:00-06:00 y ventana hasta las 22:00.
        var v = Aplica(null, Asigna(1, NocheVeintidos()), Asigna(2, ConVentana(4, 6, 1080)),
            CierreVispera(2, SeisAm(2)));

        Casilla(v, 2).Advertencias.Should().BeEmpty();
        v.TieneAdvertencias.Should().BeFalse();
    }

    [Fact]
    public void Apply_NoAdvierteDescanso_CuandoLaVentanaPermiteCumplirloAunqueLaCoberturaLoInvada()
    {
        // Vispera 17:00-01:00 (C = 01:00); dia 04:00-10:00 con ventana hasta las 18:00.
        var v = Aplica(null, Asigna(2, ConVentana(4, 10, 840)), Vispera(2, En(2, 5)), CierreVispera(2, En(2, 1)));

        Casilla(v, 2).Advertencias.Should().BeEmpty();
    }

    [Fact]
    public void Apply_AdviertePorDebajoDelDescanso_CuandoNoAlcanzaNiEntrandoAlCierreDeLaVentana()
    {
        var v = Aplica(null, Asigna(1, NocheVeintidos()), Asigna(2, ConVentana(5, 13, 120)),
            CierreVispera(2, SeisAm(2)));

        Casilla(v, 2).Advertencias.Should().Equal(Descanso(300));
    }

    [Fact]
    public void Apply_AdvierteSoloSolape_CuandoElCierreDeLaVisperaSuperaElCierreDeLaVentana()
    {
        var v = Aplica(null, Asigna(2, ConVentana(5, 13, 120)), CierreVispera(2, En(2, 9)));

        Casilla(v, 2).Advertencias.Should().Equal(Solape120);
    }

    [Fact]
    public void Apply_AdvierteSoloDescanso_CuandoElCierreDeLaVisperaIgualaElCierreDeLaVentana()
    {
        var v = Aplica(null, Asigna(2, ConVentana(5, 13, 120)), CierreVispera(2, En(2, 7)));

        Casilla(v, 2).Advertencias.Should().Equal(Descanso(360));
    }

    [Fact]
    public void Apply_ProyectaCierreDeLaVentanaYSuBandera_CuandoElTurnoTieneVentana()
    {
        var v = Aplica(null, Asigna(2, ConVentana(5, 13, 120)));

        Casilla(v, 2).CierreVentanaEntrada.Should().Be(En(2, 7));
        Casilla(v, 2).TieneVentanaDeEntrada.Should().BeTrue();
    }

    [Fact]
    public void Apply_ProyectaLaAperturaComoCierreDeLaVentana_CuandoElTurnoEsFijo()
    {
        var v = Aplica(null, Asigna(2, FijoCuatro()));

        Casilla(v, 2).CierreVentanaEntrada.Should().Be(En(2, 4));
        Casilla(v, 2).TieneVentanaDeEntrada.Should().BeFalse();
    }

    [Fact]
    public void Apply_ConservaElEstadoDeLaVentana_CuandoSeCancelaYSeReasignaSinEventoNuevoDeLaVispera()
    {
        var v = Aplica(null, Asigna(2, ConVentana(5, 13, 120)), CierreVispera(2, SeisAm(2)),
            Cancela(2), Asigna(2, ConVentana(5, 13, 120)));

        Casilla(v, 2).Advertencias.Should().Equal(Descanso(300));
    }

    [Fact]
    public void Apply_ReevaluaConLaNuevaVentana_CuandoSeReasignaOtroTurnoSinEventoNuevoDeLaVispera()
    {
        var v = Aplica(null, Asigna(2, ConVentana(5, 13, 120)), CierreVispera(2, SeisAm(2)),
            Cancela(2), Asigna(2, FijoCuatro()));

        Casilla(v, 2).TieneVentanaDeEntrada.Should().BeFalse();
        Casilla(v, 2).Advertencias.Should().Equal(Solape120);
    }

    [Fact]
    public void Apply_ConservaElEstadoDeLaVentana_CuandoSeCancelaLaAusencia()
    {
        var v = Aplica(null, Asigna(2, ConVentana(5, 13, 120)), CierreVispera(2, SeisAm(2)),
            Ausenta(2, AusenciaA), CancelaAusencia(2, AusenciaA));

        Casilla(v, 2).Tipo.Should().Be(TipoCasilla.Trabajo);
        Casilla(v, 2).CierreVentanaEntrada.Should().Be(En(2, 7));
        Casilla(v, 2).TieneVentanaDeEntrada.Should().BeTrue();
        Casilla(v, 2).Advertencias.Should().Equal(Descanso(300));
    }

    [Fact]
    public void Apply_FijaElEstadoDeLaVentana_CuandoElTurnoSeAsignaDuranteLaAusencia()
    {
        var v = Aplica(null, Ausenta(2, AusenciaA), CierreVispera(2, SeisAm(2)), Asigna(2, ConVentana(5, 13, 120)));

        Casilla(v, 2).Advertencias.Should().BeEmpty();
        Casilla(v, 2).CierreVentanaEntrada.Should().Be(En(2, 7));
        Casilla(v, 2).TieneVentanaDeEntrada.Should().BeTrue();

        v = Aplica(v, CancelaAusencia(2, AusenciaA));

        Casilla(v, 2).Advertencias.Should().Equal(Descanso(300));
    }

    [Fact]
    public void Apply_NoAdvierte_CuandoElDiaConVentanaTieneAusencia()
    {
        var v = Aplica(null, Asigna(2, ConVentana(5, 13, 120)), CierreVispera(2, SeisAm(2)), Ausenta(2, AusenciaA));

        Casilla(v, 2).Advertencias.Should().BeEmpty();
    }

    [Fact]
    public void Apply_NoAdvierte_CuandoElDiaConVentanaEsDeDescanso()
    {
        var v = Aplica(null, Asigna(2, new TurnoDiario("Libre", [], "", 120)), CierreVispera(2, SeisAm(2)));

        Casilla(v, 2).Advertencias.Should().BeEmpty();
    }

    [Fact]
    public void Apply_NoAdvierte_CuandoElCierreDeLaVisperaEsNuloConVentana()
    {
        var v = Aplica(null, Asigna(2, ConVentana(5, 13, 120)), CierreVispera(2, null));

        Casilla(v, 2).Advertencias.Should().BeEmpty();
    }

    [Fact]
    public void Apply_AdviertePorDebajoDelDescansoEnElLunes_CuandoLaVisperaEsElDomingoAnteriorYHayVentana()
    {
        var v = Aplica(null, Asigna(0, ConVentana(5, 13, 120)), CierreVispera(0, SeisAm(0)));

        Casilla(v, 0).Advertencias.Should().Equal(Descanso(300));
        v.EsJuzgable.Should().BeFalse();
        v.Jornada.Should().BeNull();
    }
}
