// Descansos/Extras se comparan por valor, no por referencia (MEF-ADR-0012). Sede entra en la
// identidad de la franja; Descripcion no.

using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;
using Bitakora.ControlAsistencia.ControlHoras.Tests.ValueObjects;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.AsignarTurnosCuandoProgramacionTurnoEnFechasSolicitada.Eventos;

public class FranjaProgramadaIgualdadTests : IgualdadTestBase<FranjaProgramada>
{
    private static SubFranjaProgramada Descanso() =>
        new(new TimeOnly(12, 0), new TimeOnly(13, 0), 0, 0, "");

    private static SubFranjaProgramada Extra() =>
        new(new TimeOnly(17, 0), new TimeOnly(19, 0), 0, 0, "");

    protected override FranjaProgramada CrearInstancia() =>
        new(new TimeOnly(8, 0), new TimeOnly(17, 0), 0, [Descanso()], [Extra()], "");

    protected override FranjaProgramada CrearInstanciaCopia() =>
        new(new TimeOnly(8, 0), new TimeOnly(17, 0), 0, [Descanso()], [Extra()], "");

    protected override IEnumerable<(string, FranjaProgramada)> CrearInstanciasDiferentes()
    {
        yield return ("HoraInicio",
            new FranjaProgramada(new TimeOnly(7, 0), new TimeOnly(17, 0), 0, [Descanso()], [Extra()], ""));
        yield return ("HoraFin",
            new FranjaProgramada(new TimeOnly(8, 0), new TimeOnly(18, 0), 0, [Descanso()], [Extra()], ""));
        yield return ("DiaOffsetFin",
            new FranjaProgramada(new TimeOnly(8, 0), new TimeOnly(17, 0), 1, [Descanso()], [Extra()], ""));
        yield return ("Descansos",
            new FranjaProgramada(new TimeOnly(8, 0), new TimeOnly(17, 0), 0, [], [Extra()], ""));
        yield return ("Extras",
            new FranjaProgramada(new TimeOnly(8, 0), new TimeOnly(17, 0), 0, [Descanso()], [], ""));
        yield return ("Sede",
            new FranjaProgramada(new TimeOnly(8, 0), new TimeOnly(17, 0), 0, [Descanso()], [Extra()], "",
                new SedeProgramada("SEDE-SUBA", "Suba")));
    }

    [Fact]
    public void Equals_RetornaTrue_CuandoListasSonInstanciasDiferentesConMismoContenido()
    {
        var a = new FranjaProgramada(new TimeOnly(8, 0), new TimeOnly(17, 0), 0,
            [new SubFranjaProgramada(new TimeOnly(12, 0), new TimeOnly(13, 0), 0, 0, "")],
            [new SubFranjaProgramada(new TimeOnly(17, 0), new TimeOnly(19, 0), 0, 0, "")], "");
        var b = new FranjaProgramada(new TimeOnly(8, 0), new TimeOnly(17, 0), 0,
            [new SubFranjaProgramada(new TimeOnly(12, 0), new TimeOnly(13, 0), 0, 0, "")],
            [new SubFranjaProgramada(new TimeOnly(17, 0), new TimeOnly(19, 0), 0, 0, "")], "");

        a.Equals(b).Should().BeTrue();
    }

    [Fact]
    public void GetHashCode_RetornaMismoHash_CuandoListasSonInstanciasDiferentesConMismoContenido()
    {
        var a = new FranjaProgramada(new TimeOnly(8, 0), new TimeOnly(17, 0), 0,
            [new SubFranjaProgramada(new TimeOnly(12, 0), new TimeOnly(13, 0), 0, 0, "")],
            [], "");
        var b = new FranjaProgramada(new TimeOnly(8, 0), new TimeOnly(17, 0), 0,
            [new SubFranjaProgramada(new TimeOnly(12, 0), new TimeOnly(13, 0), 0, 0, "")],
            [], "");

        a.GetHashCode().Should().Be(b.GetHashCode());
    }
}
