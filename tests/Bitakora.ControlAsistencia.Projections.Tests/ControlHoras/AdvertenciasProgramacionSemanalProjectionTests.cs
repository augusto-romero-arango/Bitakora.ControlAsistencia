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
                _ => throw new InvalidOperationException()
            };
        }
        return v!;
    }

    private static CasillaDia Casilla(AdvertenciasProgramacionSemanal v, int offset) => v.Casillas[offset];

    // CA-2
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

    // CA-3
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

    // CA-4
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

    // CA-5
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

    // CA-6
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
}
