using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.ListarMallaSemanalTurnos;
using Bitakora.ControlAsistencia.ReadModels.ControlHoras;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.ListarMallaSemanalTurnos;

public class ComponedorMallaSemanalTests
{
    // Semana ISO 2027-03-01 (lunes) a 2027-03-07 (domingo)
    private static readonly SemanaIso Semana = new(2027, 9, new DateOnly(2027, 3, 1), new DateOnly(2027, 3, 7));

    private static DateOnly Dia(int d) => new(2027, 3, d);

    private static Bloque BloqueTrabajo(int d) =>
        new(TipoBloque.Ordinaria, new DateTime(2027, 3, d, 6, 0, 0), new DateTime(2027, 3, d, 14, 0, 0), "sede-1", "Sede Norte");

    private static TurnoVigente DocTrabajo(string codigo, int d) =>
        new($"cd:{codigo}:202703{d:00}", codigo, "Nombre", Dia(d), "Turno Manana", "06:00-14:00", [BloqueTrabajo(d)]);

    private static TurnoVigente DocDescanso(string codigo, int d) =>
        new($"cd:{codigo}:202703{d:00}", codigo, "Nombre", Dia(d), "Descanso", "", []);

    private static TurnoVigente DocAusencia(string codigo, int d, string motivo) =>
        new($"cd:{codigo}:202703{d:00}", codigo, "Nombre", Dia(d), "", "", [], motivo, Guid.NewGuid(),
            new TurnoCubierto("Turno Manana", "06:00-14:00", [BloqueTrabajo(d)]));

    [Fact]
    public void Componer_DevuelveUnaFilaPorColaboradorOrdenadaPorCodigo()
    {
        var filas = ComponedorMallaSemanal.Componer(
            Semana,
            [new("C", Dia(1), null), new("A", Dia(1), null), new("B", Dia(1), null)],
            []);

        filas.Select(f => f.CodigoColaborador).Should().Equal("A", "B", "C");
    }

    [Fact]
    public void Componer_DevuelveSieteCasillasDeLunesADomingo_CuandoNoHayDocumentos()
    {
        var filas = ComponedorMallaSemanal.Componer(Semana, [new("A", new DateOnly(2027, 1, 1), null)], []);

        var fila = filas.Should().ContainSingle().Subject;
        fila.Casillas.Select(c => c.Fecha).Should().Equal(Dia(1), Dia(2), Dia(3), Dia(4), Dia(5), Dia(6), Dia(7));
        fila.Casillas.Should().OnlyContain(c => c.Tipo == TipoCasillaMalla.SinProgramar);
    }

    [Fact]
    public void Componer_ClasificaCadaCasilla_CuandoElColaboradorTieneVigenciaDeMartesAViernes()
    {
        var filas = ComponedorMallaSemanal.Componer(
            Semana,
            [new("A", Dia(2), Dia(5))],
            [DocTrabajo("A", 1), DocDescanso("A", 2), DocAusencia("A", 3, "Vacaciones")]);

        var c = filas.Single().Casillas;
        c[0].Tipo.Should().Be(TipoCasillaMalla.Trabajo);
        c[1].Tipo.Should().Be(TipoCasillaMalla.Descanso);
        c[2].Tipo.Should().Be(TipoCasillaMalla.Ausencia);
        c[3].Tipo.Should().Be(TipoCasillaMalla.SinProgramar);
        c[4].Tipo.Should().Be(TipoCasillaMalla.SinProgramar);
        c[5].Tipo.Should().Be(TipoCasillaMalla.FueraDeVinculacion);
        c[6].Tipo.Should().Be(TipoCasillaMalla.FueraDeVinculacion);
    }

    [Fact]
    public void Componer_MuestraTurnoHorarioYBloquesConSede_CuandoLaCasillaEsTrabajo()
    {
        var filas = ComponedorMallaSemanal.Componer(Semana, [new("A", Dia(2), Dia(5))], [DocTrabajo("A", 1)]);

        var lunes = filas.Single().Casillas[0];
        lunes.NombreTurno.Should().Be("Turno Manana");
        lunes.HorarioResumido.Should().Be("06:00-14:00");
        lunes.Bloques.Should().Equal(BloqueTrabajo(1));
        lunes.MotivoAusencia.Should().BeNull();
    }

    [Fact]
    public void Componer_ExponeElMotivoSinTurnoNiBloques_CuandoLaCasillaEsAusencia()
    {
        var filas = ComponedorMallaSemanal.Componer(Semana, [new("A", Dia(1), null)], [DocAusencia("A", 3, "Incapacidad medica")]);

        var miercoles = filas.Single().Casillas[2];
        miercoles.Tipo.Should().Be(TipoCasillaMalla.Ausencia);
        miercoles.MotivoAusencia.Should().Be("Incapacidad medica");
        miercoles.Bloques.Should().BeEmpty();
        miercoles.NombreTurno.Should().BeNullOrEmpty();
        miercoles.HorarioResumido.Should().BeNullOrEmpty();
    }

    [Fact]
    public void Componer_MarcaTrabajo_CuandoElDocumentoCaeFueraDeLaVinculacion()
    {
        var filas = ComponedorMallaSemanal.Componer(Semana, [new("A", Dia(3), Dia(4))], [DocTrabajo("A", 6)]);

        filas.Single().Casillas[5].Tipo.Should().Be(TipoCasillaMalla.Trabajo);
    }

    [Fact]
    public void Componer_DejaLaVinculacionAbiertaHaciaAdelante_CuandoVigenteHastaEsNulo()
    {
        var filas = ComponedorMallaSemanal.Componer(Semana, [new("A", Dia(3), null)], []);

        var c = filas.Single().Casillas;
        c[0].Tipo.Should().Be(TipoCasillaMalla.FueraDeVinculacion);
        c[1].Tipo.Should().Be(TipoCasillaMalla.FueraDeVinculacion);
        c[2].Tipo.Should().Be(TipoCasillaMalla.SinProgramar);
        c[6].Tipo.Should().Be(TipoCasillaMalla.SinProgramar);
    }

    [Fact]
    public void Componer_IgnoraDocumentosDeOtrosColaboradoresYDeOtrasSemanas()
    {
        var otraSemana = DocTrabajo("A", 1) with { Fecha = new DateOnly(2027, 3, 8) };

        var filas = ComponedorMallaSemanal.Componer(
            Semana, [new("A", Dia(1), null)], [DocTrabajo("B", 1), otraSemana]);

        filas.Single().Casillas.Should().OnlyContain(c => c.Tipo == TipoCasillaMalla.SinProgramar);
    }
}
