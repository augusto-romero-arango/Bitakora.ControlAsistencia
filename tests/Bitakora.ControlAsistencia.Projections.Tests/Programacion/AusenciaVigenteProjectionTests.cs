// Invocacion DIRECTA de los metodos estaticos de AusenciaVigenteProjection (N2, MEF-ADR-0035):
// funciones puras evento -> vista, sin abrir streams. Oraculos armados a mano (MEF-ADR-0002).
// BeEquivalentTo: el record tiene colecciones sin igualdad por valor.

using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.Projections.Programacion;
using Bitakora.ControlAsistencia.ReadModels.Programacion;

namespace Bitakora.ControlAsistencia.Projections.Tests.Programacion;

public class AusenciaVigenteProjectionTests
{
    private static readonly Guid AusenciaId = Guid.Parse("019600b0-0000-7000-8000-000000000755");

    private static ColaboradorProgramado Ana() => new("CC-1098765432", "EMP-001", "Ana Ramirez");

    private static DateOnly Oct(int dia) => new(2026, 10, dia);

    private static AusenciaVigente VistaDeVacaciones1Al30() =>
        new(AusenciaId, "EMP-001", "Ana Ramirez", "Vacaciones", [new TramoDeAusencia(Oct(1), Oct(30))], Oct(1), Oct(30));

    private static AusenciaCancelada Cancelar(params int[] dias) =>
        new(AusenciaId, Ana(), dias.Select(Oct).ToList());

    // CA-1: el documento nace con un unico tramo, los limites y la terna del evento.
    [Fact]
    public void Create_ProyectaLaAusenciaConUnTramo_DesdeAusenciaProgramada()
    {
        var evento = new AusenciaProgramada(AusenciaId, Ana(), Oct(1), Oct(30), MotivoAusencia.Vacaciones);

        var vista = AusenciaVigenteProjection.Create(evento);

        vista!.Should().BeEquivalentTo(VistaDeVacaciones1Al30());
    }

    // CA-1: cancelar dias del medio parte el tramo en dos; los limites no cambian.
    [Fact]
    public void Apply_PartaElTramoEnDos_CuandoSeCancelanDiasDelMedio()
    {
        var vista = AusenciaVigenteProjection.Apply(Cancelar(10, 11, 12), VistaDeVacaciones1Al30());

        vista!.Should().BeEquivalentTo(VistaDeVacaciones1Al30() with
        {
            TramosVigentes = [new TramoDeAusencia(Oct(1), Oct(9)), new TramoDeAusencia(Oct(13), Oct(30))],
        });
    }

    // CA-1: cancelar el inicio mueve PrimerDiaVigente.
    [Fact]
    public void Apply_AjustaPrimerDiaVigente_CuandoSeCancelaElInicio()
    {
        var vista = AusenciaVigenteProjection.Apply(Cancelar(1, 2, 3), VistaDeVacaciones1Al30());

        vista!.Should().BeEquivalentTo(VistaDeVacaciones1Al30() with
        {
            TramosVigentes = [new TramoDeAusencia(Oct(4), Oct(30))],
            PrimerDiaVigente = Oct(4),
        });
    }

    // CA-1: cancelar el final mueve UltimoDiaVigente ("volvio el 20").
    [Fact]
    public void Apply_AjustaUltimoDiaVigente_CuandoSeCancelaElFinal()
    {
        var vista = AusenciaVigenteProjection.Apply(Cancelar(20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30), VistaDeVacaciones1Al30());

        vista!.Should().BeEquivalentTo(VistaDeVacaciones1Al30() with
        {
            TramosVigentes = [new TramoDeAusencia(Oct(1), Oct(19))],
            UltimoDiaVigente = Oct(19),
        });
    }

    // Apply nunca lanza (MEF-ADR-0004 capa 4): fechas que no estaban vigentes dejan la vista igual.
    [Fact]
    public void Apply_DejaLaVistaIgual_CuandoLasFechasNoEstabanVigentes()
    {
        var previa = VistaDeVacaciones1Al30() with
        {
            TramosVigentes = [new TramoDeAusencia(Oct(1), Oct(9)), new TramoDeAusencia(Oct(13), Oct(30))],
        };

        var vista = AusenciaVigenteProjection.Apply(Cancelar(10, 11), previa);

        vista!.Should().BeEquivalentTo(previa);
    }

    // Correlacion N2: la cancelacion conserva la identidad (AusenciaId) del documento creado.
    [Fact]
    public void Apply_ConservaLaIdentidadDeLaAusencia_CuandoSeCancelaParcialmente()
    {
        var vista = AusenciaVigenteProjection.Apply(Cancelar(5), VistaDeVacaciones1Al30());

        vista!.Id.Should().Be(AusenciaId);
    }

    // CA-1: cancelar todas las fechas elimina el documento: Apply retorna null, que el evolver
    // generado traduce a Delete (ShouldDelete(e, vista) + Apply no compila con el generador, CS8120).
    [Fact]
    public void Apply_RetornaNull_CuandoSeCancelanTodasLasFechasVigentes()
    {
        var vista = AusenciaVigenteProjection.Apply(Cancelar(Enumerable.Range(1, 30).ToArray()), VistaDeVacaciones1Al30());

        vista.Should().BeNull();
    }

    [Fact]
    public void Apply_RetornaNull_CuandoSeCancelanLosUltimosDiasVigentesDeUnaAusenciaYaPartida()
    {
        var previa = VistaDeVacaciones1Al30() with
        {
            TramosVigentes = [new TramoDeAusencia(Oct(4), Oct(5))],
            PrimerDiaVigente = Oct(4),
            UltimoDiaVigente = Oct(5),
        };

        AusenciaVigenteProjection.Apply(Cancelar(4, 5), previa).Should().BeNull();
    }
}
