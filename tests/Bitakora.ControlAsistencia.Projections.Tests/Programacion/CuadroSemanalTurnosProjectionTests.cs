// Invocacion DIRECTA de los metodos estaticos: funcion pura evento -> vista, sin abrir streams.
// Cada oraculo se arma a mano con el constructor posicional (MEF-ADR-0002); los datos de la copia
// del turno (nombre, descripcion) salen de la fixture CopiaTurno, nunca de la logica del SUT.
// BeEquivalentTo con orden estricto donde importa: los records planos no tienen igualdad por
// valor sobre sus colecciones.

using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.Projections.Programacion;
using Bitakora.ControlAsistencia.ReadModels.Programacion;
using JasperFx.Events;

namespace Bitakora.ControlAsistencia.Projections.Tests.Programacion;

public class CuadroSemanalTurnosProjectionTests
{
    private static readonly Turno CopiaTurno =
        Turno.Crear("Turno Manana", false, [FranjaOrdinaria.Crear(new TimeOnly(6, 0), new TimeOnly(14, 0))]);

    private static readonly Turno CopiaIncompleta = Turno.Crear("Turno Nuevo", false, []);

    private static CuadroSemanalTurnos Vista(
        IReadOnlyList<DiaDelCuadro> dias,
        int semanas = 1,
        bool completa = false,
        Guid? jornadaId = null,
        LimitesDelCuadro? limites = null,
        IReadOnlyList<AdvertenciaDelCuadro>? advertencias = null) =>
        new("plantilla-001", "Semana Cocina", semanas, dias, completa, jornadaId, limites, advertencias ?? []);

    private static DiaDelCuadro DiaCompleto(int semana, int dia, Guid turnoId) =>
        new(semana, dia, turnoId.ToString(), "Turno Manana", CopiaTurno.ToString(), true, false);

    private static LimitesJornada LimitesDeEjemplo() =>
        LimitesJornada.Crear(HorasYMinutos.Crear(44, 0), HorasYMinutos.Crear(10, 0), HorasYMinutos.Crear(4, 0), 1);

    private static LimitesDelCuadro LimitesDelCuadroDeEjemplo() =>
        new(2640, 600, 240, 1, LimitesDeEjemplo().ToString());

    private static readonly Guid TurnoA = Guid.Parse("019600b0-0000-7000-8000-000000000001");
    private static readonly Guid TurnoB = Guid.Parse("019600b0-0000-7000-8000-000000000002");
    private static readonly Guid JornadaId = Guid.Parse("019600b0-0000-7000-8000-0000000000a1");

    private static IReadOnlyList<DiaDelCuadro> SemanaCompletaSin(int diaFaltante, Guid turnoId) =>
        Enumerable.Range(1, 7).Where(d => d != diaFaltante).Select(d => DiaCompleto(1, d, turnoId)).ToList();

    [Fact]
    public void Create_ProyectaElCuadroVacioSinJornadaNiAdvertencias_DesdePlantillaSemanalCreada()
    {
        var payload = PlantillaSemanalCreada.Crear(Guid.Parse("019600b0-0000-7000-8000-000000000099"), "Semana Cocina", 2);
        var evento = new Event<PlantillaSemanalCreada>(payload)
        {
            StreamKey = "plantilla-001",
            Version = 1,
            Timestamp = DateTimeOffset.UtcNow,
        };

        var vista = CuadroSemanalTurnosProjection.Create(evento);

        vista.Should().BeEquivalentTo(new CuadroSemanalTurnos(
            "plantilla-001", "Semana Cocina", 2, [], false, null, null, []));
    }

    [Fact]
    public void Apply_GuardaLaCopiaDelTurnoEnElDia_CuandoDiaDePlantillaSemanalAsignado()
    {
        var evento = DiaDePlantillaSemanalAsignado.Crear(Guid.NewGuid(), 1, DiaSemana.Desde(5), TurnoA, CopiaTurno, 1);

        var vista = CuadroSemanalTurnosProjection.Apply(evento, Vista([], semanas: 2));

        vista.Should().BeEquivalentTo(Vista([DiaCompleto(1, 5, TurnoA)], semanas: 2, completa: false));
    }

    [Fact]
    public void Apply_MarcaElDiaIncompleto_CuandoLaCopiaDelTurnoNoTieneFranjas()
    {
        var evento = DiaDePlantillaSemanalAsignado.Crear(Guid.NewGuid(), 1, DiaSemana.Desde(2), TurnoA, CopiaIncompleta, 1);

        var vista = CuadroSemanalTurnosProjection.Apply(evento, Vista([]));

        vista.Dias.Should().BeEquivalentTo(
            [new DiaDelCuadro(1, 2, TurnoA.ToString(), "Turno Nuevo", CopiaIncompleta.ToString(), false, false)]);
    }

    [Fact]
    public void Apply_ReemplazaElDia_CuandoDiaDePlantillaSemanalAsignadoSobreElMismoSlot()
    {
        var previa = Vista([DiaCompleto(1, 5, TurnoA)], semanas: 2);
        var evento = DiaDePlantillaSemanalAsignado.Crear(Guid.NewGuid(), 1, DiaSemana.Desde(5), TurnoB, CopiaTurno, 1);

        var vista = CuadroSemanalTurnosProjection.Apply(evento, previa);

        vista.Dias.Should().BeEquivalentTo([DiaCompleto(1, 5, TurnoB)]);
    }

    [Fact]
    public void Apply_OrdenaLosDiasPorSemanaYDia_CuandoSeAsignanSlotsDesordenados()
    {
        var previa = Vista([DiaCompleto(1, 5, TurnoA)], semanas: 2);

        var tras = CuadroSemanalTurnosProjection.Apply(
            DiaDePlantillaSemanalAsignado.Crear(Guid.NewGuid(), 2, DiaSemana.Desde(1), TurnoA, CopiaTurno, 1), previa);
        var vista = CuadroSemanalTurnosProjection.Apply(
            DiaDePlantillaSemanalAsignado.Crear(Guid.NewGuid(), 1, DiaSemana.Desde(7), TurnoA, CopiaTurno, 1), tras);

        vista.Dias.Should().BeEquivalentTo(
            [DiaCompleto(1, 5, TurnoA), DiaCompleto(1, 7, TurnoA), DiaCompleto(2, 1, TurnoA)],
            o => o.WithStrictOrdering());
    }

    [Fact]
    public void Apply_MarcaCompleta_CuandoDiaDePlantillaSemanalAsignadoCierraLosSieteDiasCompletos()
    {
        var previa = Vista(SemanaCompletaSin(3, TurnoA));
        var evento = DiaDePlantillaSemanalAsignado.Crear(Guid.NewGuid(), 1, DiaSemana.Desde(3), TurnoA, CopiaTurno, 1);

        var vista = CuadroSemanalTurnosProjection.Apply(evento, previa);

        vista.Completa.Should().BeTrue();
    }

    [Fact]
    public void Apply_DejaCompletaEnFalso_CuandoElDiaAsignadoTieneTurnoIncompleto()
    {
        var previa = Vista(SemanaCompletaSin(3, TurnoA));
        var evento = DiaDePlantillaSemanalAsignado.Crear(Guid.NewGuid(), 1, DiaSemana.Desde(3), TurnoB, CopiaIncompleta, 1);

        var vista = CuadroSemanalTurnosProjection.Apply(evento, previa);

        vista.Completa.Should().BeFalse();
    }

    [Fact]
    public void Apply_QuitaElDiaYRecalculaCompleta_CuandoDiaDePlantillaSemanalQuitado()
    {
        var previa = Vista(SemanaCompletaSin(0, TurnoA), completa: true);
        var evento = DiaDePlantillaSemanalQuitado.Crear(Guid.NewGuid(), 1, DiaSemana.Desde(5));

        var vista = CuadroSemanalTurnosProjection.Apply(evento, previa);

        vista.Should().BeEquivalentTo(Vista(SemanaCompletaSin(5, TurnoA), completa: false));
    }

    [Fact]
    public void Apply_DejaLaVistaSinCambios_CuandoDiaDePlantillaSemanalQuitadoSobreSlotAusente()
    {
        var previa = Vista([DiaCompleto(1, 6, TurnoA)], semanas: 2);

        var vista = CuadroSemanalTurnosProjection.Apply(
            DiaDePlantillaSemanalQuitado.Crear(Guid.NewGuid(), 1, DiaSemana.Desde(5)), previa);

        vista.Should().BeEquivalentTo(previa);
    }

    [Fact]
    public void Apply_ActualizaLaCopiaEnTodosLosDiasConEseTurnoId_CuandoTurnoDePlantillaSemanalSincronizado()
    {
        var turnoNuevo = Turno.Crear("Turno Tarde", false, [FranjaOrdinaria.Crear(new TimeOnly(14, 0), new TimeOnly(22, 0))]);
        var previa = Vista([DiaCompleto(1, 1, TurnoA), DiaCompleto(1, 2, TurnoB), DiaCompleto(1, 3, TurnoA)]);
        var evento = TurnoDePlantillaSemanalSincronizado.Crear(Guid.NewGuid(), TurnoA, turnoNuevo, 2, false);

        var vista = CuadroSemanalTurnosProjection.Apply(evento, previa);

        var sincronizado = new DiaDelCuadro(0, 0, "", "Turno Tarde", turnoNuevo.ToString(), true, false);
        vista.Dias.Should().BeEquivalentTo(
            [
                sincronizado with { Semana = 1, Dia = 1, TurnoId = TurnoA.ToString() },
                DiaCompleto(1, 2, TurnoB),
                sincronizado with { Semana = 1, Dia = 3, TurnoId = TurnoA.ToString() },
            ],
            o => o.WithStrictOrdering());
    }

    [Fact]
    public void Apply_MarcaRetiradoYQuitaCompleta_CuandoTurnoDePlantillaSemanalSincronizadoRetirado()
    {
        var previa = Vista(SemanaCompletaSin(0, TurnoA), completa: true);
        var evento = TurnoDePlantillaSemanalSincronizado.Crear(Guid.NewGuid(), TurnoA, CopiaTurno, 3, true);

        var vista = CuadroSemanalTurnosProjection.Apply(evento, previa);

        vista.Completa.Should().BeFalse();
        vista.Dias.Should().OnlyContain(d => d.Retirado);
    }

    [Fact]
    public void Apply_RecalculaCompleta_CuandoTurnoDePlantillaSemanalSincronizadoCompletaElTurno()
    {
        var dias = Enumerable.Range(1, 7)
            .Select(d => new DiaDelCuadro(1, d, TurnoA.ToString(), "Turno Nuevo", CopiaIncompleta.ToString(), false, false))
            .ToList();
        var evento = TurnoDePlantillaSemanalSincronizado.Crear(Guid.NewGuid(), TurnoA, CopiaTurno, 2, false);

        var vista = CuadroSemanalTurnosProjection.Apply(evento, Vista(dias));

        vista.Completa.Should().BeTrue();
    }

    [Fact]
    public void Apply_DejaLaVistaIgual_CuandoTurnoDePlantillaSemanalSincronizadoDeTurnoSinDias()
    {
        var previa = Vista([DiaCompleto(1, 1, TurnoA)]);
        var evento = TurnoDePlantillaSemanalSincronizado.Crear(Guid.NewGuid(), TurnoB, CopiaTurno, 2, false);

        var vista = CuadroSemanalTurnosProjection.Apply(evento, previa);

        vista.Should().BeEquivalentTo(previa);
    }

    [Fact]
    public void Apply_GuardaJornadaYLimites_CuandoJornadaDePlantillaSemanalAsignada()
    {
        var evento = JornadaDePlantillaSemanalAsignada.Crear(Guid.NewGuid(), JornadaId, LimitesDeEjemplo(), 1);

        var vista = CuadroSemanalTurnosProjection.Apply(evento, Vista([DiaCompleto(1, 5, TurnoA)]));

        vista.Should().BeEquivalentTo(Vista(
            [DiaCompleto(1, 5, TurnoA)], jornadaId: JornadaId, limites: LimitesDelCuadroDeEjemplo()));
    }

    [Fact]
    public void Apply_DejaJornadaYLimitesEnNull_CuandoJornadaDePlantillaSemanalQuitada()
    {
        var previa = Vista([], jornadaId: JornadaId, limites: LimitesDelCuadroDeEjemplo());

        var vista = CuadroSemanalTurnosProjection.Apply(JornadaDePlantillaSemanalQuitada.Crear(Guid.NewGuid()), previa);

        vista.Should().BeEquivalentTo(Vista([]));
    }

    [Fact]
    public void Apply_ReemplazaLosLimites_CuandoLimitesDeJornadaDePlantillaSemanalSincronizados()
    {
        var previa = Vista([], jornadaId: JornadaId, limites: LimitesDelCuadroDeEjemplo());
        var nuevos = LimitesJornada.Crear(
            HorasYMinutos.Crear(40, 30), HorasYMinutos.Crear(9, 0), HorasYMinutos.Crear(2, 15), 2);

        var vista = CuadroSemanalTurnosProjection.Apply(
            LimitesDeJornadaDePlantillaSemanalSincronizados.Crear(Guid.NewGuid(), nuevos, 2), previa);

        vista.Should().BeEquivalentTo(Vista(
            [], jornadaId: JornadaId, limites: new LimitesDelCuadro(2430, 540, 135, 2, nuevos.ToString())));
    }

    [Fact]
    public void Apply_ReemplazaLaListaCompleta_CuandoAdvertenciasDePlantillaSemanalCalculadas()
    {
        var previa = Vista([], advertencias: [new AdvertenciaDelCuadro("DiaSinTurno", 1, 3, 0)]);
        var evento = AdvertenciasDePlantillaSemanalCalculadas.Crear(Guid.NewGuid(),
        [
            AdvertenciaPlantillaSemanal.PlantillaSinJornada(),
            AdvertenciaPlantillaSemanal.SuperaTopeDiario(1, DiaSemana.Desde(2), 30),
            AdvertenciaPlantillaSemanal.FaltanDiasDeDescanso(2, 1),
        ]);

        var vista = CuadroSemanalTurnosProjection.Apply(evento, previa);

        vista.Advertencias.Should().BeEquivalentTo(
            [
                new AdvertenciaDelCuadro("PlantillaSinJornada", null, null, 0),
                new AdvertenciaDelCuadro("SuperaTopeDiario", 1, 2, 30),
                new AdvertenciaDelCuadro("FaltanDiasDeDescanso", 2, null, 1),
            ],
            o => o.WithStrictOrdering());
    }

    [Fact]
    public void Apply_VaciaLasAdvertencias_CuandoAdvertenciasDePlantillaSemanalCalculadasSinElementos()
    {
        var previa = Vista([], advertencias: [new AdvertenciaDelCuadro("DiaSinTurno", 1, 3, 0)]);

        var vista = CuadroSemanalTurnosProjection.Apply(
            AdvertenciasDePlantillaSemanalCalculadas.Crear(Guid.NewGuid(), []), previa);

        vista.Advertencias.Should().BeEmpty();
    }

    [Fact]
    public void ShouldDelete_BorraElCuadro_CuandoPlantillaSemanalRetirada()
    {
        CuadroSemanalTurnosProjection.ShouldDelete(PlantillaSemanalRetirada.Crear(Guid.NewGuid())).Should().BeTrue();
    }
}
