using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.Projections.Programacion;
using Bitakora.ControlAsistencia.ReadModels.Programacion;
using JasperFx.Events;

namespace Bitakora.ControlAsistencia.Projections.Tests.Programacion;

public class CuadroDeTurnosProjectionTests
{
    private static readonly Turno CopiaTurno =
        Turno.Crear("Turno Manana", false, [FranjaOrdinaria.Crear(new TimeOnly(6, 0), new TimeOnly(14, 0))]);

    private static readonly Turno CopiaIncompleta = Turno.Crear("Turno Nuevo", false, []);

    private static readonly Guid TurnoA = Guid.Parse("019600b0-0000-7000-8000-000000000001");
    private static readonly Guid TurnoB = Guid.Parse("019600b0-0000-7000-8000-000000000002");
    private static readonly Guid JornadaId = Guid.Parse("019600b0-0000-7000-8000-0000000000a1");

    private static CuadroDeTurnos Vista(
        IReadOnlyList<DiaDelCuadroDeTurnos> dias,
        int diasDelCiclo = 3,
        bool completa = false,
        Guid? jornadaId = null,
        LimitesDelCuadro? limites = null,
        IReadOnlyList<AdvertenciaDelCuadroDeTurnos>? advertencias = null) =>
        new("plantilla-001", "Ciclo Cocina", diasDelCiclo, dias, completa, jornadaId, limites, advertencias ?? []);

    private static DiaDelCuadroDeTurnos DiaCompleto(int dia, Guid turnoId) =>
        new(dia, turnoId.ToString("D"), "Turno Manana", "06:00-14:00", true, false);

    private static LimitesJornada LimitesDeEjemplo() =>
        LimitesJornada.Crear(HorasYMinutos.Crear(44, 0), HorasYMinutos.Crear(10, 0), HorasYMinutos.Crear(4, 0), 1);

    private static DiaDePlantillaDeTurnosAsignado Asignar(int dia, Guid turnoId, Turno turno) =>
        DiaDePlantillaDeTurnosAsignado.Crear(Guid.NewGuid(), dia, turnoId, turno, 1);

    [Fact]
    public void Create_ProyectaElCuadroVacio_DesdePlantillaDeTurnosCreada()
    {
        var payload = PlantillaDeTurnosCreada.Crear(Guid.Parse("019600b0-0000-7000-8000-000000000099"), "Ciclo Cocina", 6);
        var evento = new Event<PlantillaDeTurnosCreada>(payload)
        {
            StreamKey = "plantilla-001",
            Version = 1,
            Timestamp = DateTimeOffset.UtcNow,
        };

        var vista = CuadroDeTurnosProjection.Create(evento);

        vista.Should().BeEquivalentTo(new CuadroDeTurnos(
            "plantilla-001", "Ciclo Cocina", 6, [], false, null, null, []));
    }

    [Fact]
    public void Apply_GuardaLaCopiaDelTurnoEnElDia_CuandoDiaDePlantillaDeTurnosAsignado()
    {
        var vista = CuadroDeTurnosProjection.Apply(Asignar(2, TurnoA, CopiaTurno), Vista([]));

        vista.Should().BeEquivalentTo(Vista([DiaCompleto(2, TurnoA)]));
    }

    [Fact]
    public void Apply_ReemplazaElDiaExistente_CuandoSeAsignaOtroTurnoAlMismoDia()
    {
        var previa = Vista([DiaCompleto(2, TurnoA)]);

        var vista = CuadroDeTurnosProjection.Apply(Asignar(2, TurnoB, CopiaTurno), previa);

        vista.Dias.Should().BeEquivalentTo([DiaCompleto(2, TurnoB)], o => o.WithStrictOrdering());
    }

    [Fact]
    public void Apply_OrdenaLosDiasPorNumero_CuandoLlegaUnDiaMenorDespues()
    {
        var previa = Vista([DiaCompleto(3, TurnoA)]);

        var vista = CuadroDeTurnosProjection.Apply(Asignar(1, TurnoA, CopiaTurno), previa);

        vista.Dias.Should().BeEquivalentTo(
            [DiaCompleto(1, TurnoA), DiaCompleto(3, TurnoA)], o => o.WithStrictOrdering());
    }

    [Fact]
    public void Apply_MarcaElDiaIncompleto_CuandoLaCopiaDelTurnoNoTieneFranjas()
    {
        var vista = CuadroDeTurnosProjection.Apply(Asignar(1, TurnoA, CopiaIncompleta), Vista([]));

        vista.Dias.Should().BeEquivalentTo(
            [new DiaDelCuadroDeTurnos(1, TurnoA.ToString("D"), "Turno Nuevo", CopiaIncompleta.ToString(), false, false)]);
        vista.Completa.Should().BeFalse();
    }

    [Fact]
    public void Apply_MarcaCompleta_CuandoTodosLosDiasDelCicloTienenTurnoCompleto()
    {
        var previa = Vista([DiaCompleto(1, TurnoA), DiaCompleto(2, TurnoA)]);

        var vista = CuadroDeTurnosProjection.Apply(Asignar(3, TurnoA, CopiaTurno), previa);

        vista.Completa.Should().BeTrue();
    }

    [Fact]
    public void Apply_NoMarcaCompleta_CuandoFaltaUnDiaDelCiclo()
    {
        var previa = Vista([DiaCompleto(1, TurnoA)]);

        var vista = CuadroDeTurnosProjection.Apply(Asignar(3, TurnoA, CopiaTurno), previa);

        vista.Completa.Should().BeFalse();
    }

    [Fact]
    public void Apply_NoMarcaCompleta_CuandoUnDiaTieneTurnoIncompleto()
    {
        var previa = Vista([DiaCompleto(1, TurnoA), DiaCompleto(2, TurnoA)]);

        var vista = CuadroDeTurnosProjection.Apply(Asignar(3, TurnoB, CopiaIncompleta), previa);

        vista.Completa.Should().BeFalse();
    }

    [Fact]
    public void Apply_MarcaCompleta_CuandoElCicloDeUnDiaTieneSuTurnoCompleto()
    {
        var vista = CuadroDeTurnosProjection.Apply(Asignar(1, TurnoA, CopiaTurno), Vista([], diasDelCiclo: 1));

        vista.Completa.Should().BeTrue();
    }

    [Fact]
    public void Apply_LlenaJornadaYLimites_CuandoJornadaDePlantillaDeTurnosAsignada()
    {
        var limites = LimitesDeEjemplo();
        var evento = JornadaDePlantillaDeTurnosAsignada.Crear(Guid.NewGuid(), JornadaId, limites, 1);

        var vista = CuadroDeTurnosProjection.Apply(evento, Vista([]));

        vista.JornadaId.Should().Be(JornadaId);
        vista.Limites.Should().BeEquivalentTo(new LimitesDelCuadro(2640, 600, 240, 1, limites.ToString()));
    }

    [Fact]
    public void Apply_DejaJornadaYLimitesEnNull_CuandoJornadaDePlantillaDeTurnosQuitada()
    {
        var previa = Vista([], jornadaId: JornadaId, limites: new LimitesDelCuadro(2640, 600, 240, 1, "x"));

        var vista = CuadroDeTurnosProjection.Apply(JornadaDePlantillaDeTurnosQuitada.Crear(Guid.NewGuid()), previa);

        vista.JornadaId.Should().BeNull();
        vista.Limites.Should().BeNull();
    }

    [Fact]
    public void Apply_ReemplazaLaListaCompleta_CuandoAdvertenciasDePlantillaDeTurnosCalculadas()
    {
        var previa = Vista([], advertencias: [new AdvertenciaDelCuadroDeTurnos("DiaSinTurno", 3, 0)]);
        var evento = AdvertenciasDePlantillaDeTurnosCalculadas.Crear(Guid.NewGuid(),
        [
            AdvertenciaPlantillaTurnos.PlantillaSinJornada(),
            AdvertenciaPlantillaTurnos.SuperaTopeDiario(2, 30),
        ]);

        var vista = CuadroDeTurnosProjection.Apply(evento, previa);

        vista.Advertencias.Should().BeEquivalentTo(
            [
                new AdvertenciaDelCuadroDeTurnos("PlantillaSinJornada", null, 0),
                new AdvertenciaDelCuadroDeTurnos("SuperaTopeDiario", 2, 30),
            ],
            o => o.WithStrictOrdering());
    }

    [Fact]
    public void ShouldDelete_DevuelveTrue_CuandoPlantillaDeTurnosRetirada()
    {
        CuadroDeTurnosProjection.ShouldDelete(PlantillaDeTurnosRetirada.Crear(Guid.NewGuid())).Should().BeTrue();
    }
}
