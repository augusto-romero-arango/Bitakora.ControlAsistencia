using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.ListarAusenciasDelEquipo;
using Bitakora.ControlAsistencia.ReadModels.Programacion;

namespace Bitakora.ControlAsistencia.Programacion.Tests.ListarAusenciasDelEquipo;

// Composicion pura de la respuesta sobre las vistas que ya cruzan el periodo (el filtro por cruce y
// por codigos lo resuelve la consulta Marten). Oraculos armados a mano (MEF-ADR-0002).
public class ListaAusenciasDelEquipoComponerTests
{
    private static readonly Guid VacacionesAna = Guid.Parse("019600b0-0000-7000-8000-000000000001");
    private static readonly Guid IncapacidadLuis = Guid.Parse("019600b0-0000-7000-8000-000000000002");
    private static readonly Guid LicenciaAna = Guid.Parse("019600b0-0000-7000-8000-000000000003");

    private static DateOnly Oct(int dia) => new(2026, 10, dia);

    private static readonly RangoAplicado Semana13Al19 = new(Oct(19), false);

    private static AusenciaVigente Vista(Guid id, string codigo, string nombre, string motivo, params TramoDeAusencia[] tramos) =>
        new(id, codigo, nombre, motivo, tramos, tramos[0].Desde, tramos[^1].Hasta);

    private static AusenciaVigente LuisIncapacidad15Al16() =>
        Vista(IncapacidadLuis, "EMP-002", "Luis Perez", "IncapacidadMedica", new TramoDeAusencia(Oct(15), Oct(16)));

    private static AusenciaVigente AnaVacaciones1Al30() =>
        Vista(VacacionesAna, "EMP-001", "Ana Ramirez", "Vacaciones", new TramoDeAusencia(Oct(1), Oct(30)));

    // CA-2: tramos recortados al periodo, agrupados por colaborador y ordenados por nombre.
    [Fact]
    public void Componer_AgrupaPorColaboradorOrdenadoPorNombre_ConLosTramosRecortadosAlPeriodo()
    {
        var lista = ListaAusenciasDelEquipo.Componer(Oct(13), Semana13Al19, [LuisIncapacidad15Al16(), AnaVacaciones1Al30()]);

        lista.Should().BeEquivalentTo(new ListaAusenciasDelEquipo(Oct(13), Oct(19), false,
        [
            new AusenciasDeColaborador("EMP-001", "Ana Ramirez",
                [new AusenciaDelPeriodo(VacacionesAna, "Vacaciones", [new TramoAplicado(Oct(13), Oct(19))])]),
            new AusenciasDeColaborador("EMP-002", "Luis Perez",
                [new AusenciaDelPeriodo(IncapacidadLuis, "IncapacidadMedica", [new TramoAplicado(Oct(15), Oct(16))])]),
        ]), o => o.WithStrictOrdering());
    }

    // Una ausencia partida cuyos tramos caen fuera del periodo no aparece; las de un mismo
    // colaborador se ordenan por su primer dia.
    [Fact]
    public void Componer_DescartaAusenciasSinTramosEnElPeriodo_YOrdenaLasDelColaboradorPorFecha()
    {
        var partidaFueraDelPeriodo = Vista(IncapacidadLuis, "EMP-002", "Luis Perez", "IncapacidadMedica",
            new TramoDeAusencia(Oct(10), Oct(12)), new TramoDeAusencia(Oct(20), Oct(22)));
        var licencia = Vista(LicenciaAna, "EMP-001", "Ana Ramirez", "LicenciaRemunerada", new TramoDeAusencia(Oct(18), Oct(18)));
        var vacacionesPartidas = Vista(VacacionesAna, "EMP-001", "Ana Ramirez", "Vacaciones",
            new TramoDeAusencia(Oct(12), Oct(14)), new TramoDeAusencia(Oct(16), Oct(16)));

        var lista = ListaAusenciasDelEquipo.Componer(Oct(13), Semana13Al19, [licencia, partidaFueraDelPeriodo, vacacionesPartidas]);

        lista.Colaboradores.Should().BeEquivalentTo(new[]
        {
            new AusenciasDeColaborador("EMP-001", "Ana Ramirez",
            [
                new AusenciaDelPeriodo(VacacionesAna, "Vacaciones",
                    [new TramoAplicado(Oct(13), Oct(14)), new TramoAplicado(Oct(16), Oct(16))]),
                new AusenciaDelPeriodo(LicenciaAna, "LicenciaRemunerada", [new TramoAplicado(Oct(18), Oct(18))]),
            ]),
        }, o => o.WithStrictOrdering());
    }

    // El periodo aplicado y la senal viajan en la respuesta; incluye dias 32 a 35, no el 36.
    [Fact]
    public void Componer_RecortaLosTramosAlHastaAplicado_CuandoElRangoFueRecortado()
    {
        var desde = Oct(7);
        var hastaPedido = new DateOnly(2026, 11, 11);
        var ausenciaEnElBorde = Vista(VacacionesAna, "EMP-001", "Ana Ramirez", "Vacaciones",
            new TramoDeAusencia(new DateOnly(2026, 11, 7), new DateOnly(2026, 11, 11)));
        var ausenciaPosterior = Vista(LicenciaAna, "EMP-001", "Ana Ramirez", "LicenciaRemunerada",
            new TramoDeAusencia(new DateOnly(2026, 11, 12), new DateOnly(2026, 11, 13)));

        var lista = ListaAusenciasDelEquipo.Componer(desde, RangoConsulta.Recortar(desde, hastaPedido),
            [ausenciaEnElBorde, ausenciaPosterior]);

        lista.Desde.Should().Be(Oct(7));
        lista.Hasta.Should().Be(new DateOnly(2026, 11, 10));
        lista.RangoRecortado.Should().BeTrue();
        lista.Colaboradores.Single().Ausencias.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new AusenciaDelPeriodo(VacacionesAna, "Vacaciones",
                [new TramoAplicado(new DateOnly(2026, 11, 7), new DateOnly(2026, 11, 10))]));
    }

    // CA-6: periodo sin ausencias -> lista vacia.
    [Fact]
    public void Componer_RetornaListaVacia_CuandoNoHayAusenciasEnElPeriodo()
    {
        var lista = ListaAusenciasDelEquipo.Componer(Oct(13), Semana13Al19, []);

        lista.Should().BeEquivalentTo(new ListaAusenciasDelEquipo(Oct(13), Oct(19), false, []));
    }
}
