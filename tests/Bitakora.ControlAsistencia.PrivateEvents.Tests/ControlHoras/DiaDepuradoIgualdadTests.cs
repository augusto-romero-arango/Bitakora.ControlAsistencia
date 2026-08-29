// Issue #424: DiaDepurado gana dos colecciones nuevas (Franjas, Marcaciones) -- el record por
// defecto compararia por referencia (ADR-0015). Equals/GetHashCode propios comparan por valor
// (SequenceEqual), precedente TurnoDiario/FranjaProgramada/HorasDiscriminadas.
//
// Issue #464 (CA-6): dos payloads que difieren SOLO en los campos nuevos de sede (dentro de una
// Franja o de una Marcacion) deben distinguirse -- el SequenceEqual delega en el Equals por valor
// de FranjaDepurada/MarcacionDelDia, que ya cubre esos campos (ver sus propios IgualdadTests).

using AwesomeAssertions;
using Bitakora.ControlAsistencia.PrivateEvents.Colaboradores;
using Bitakora.ControlAsistencia.PrivateEvents.ControlHoras;

namespace Bitakora.ControlAsistencia.PrivateEvents.Tests.ControlHoras;

public class DiaDepuradoIgualdadTests : IgualdadTestBase<DiaDepurado>
{
    private static readonly DateOnly Fecha = new(2026, 3, 15);

    private static readonly ResumenColaborador Colaborador =
        new("CC-1234567890", "EMP-001", "Luis Augusto Barreto");

    private static FranjaDepurada Franja() => new(
        new TimeOnly(6, 0), new TimeOnly(14, 0), 0,
        new DateTime(2026, 3, 15, 7, 0, 0), new DateTime(2026, 3, 15, 15, 0, 0), false);

    private static MarcacionDelDia Marcacion() =>
        new(new DateTime(2026, 3, 15, 7, 0, 0), "ENTRADA");

    private static FranjaDepurada FranjaConSedeProgramada() =>
        Franja() with { CodigoSedeProgramada = "001", NombreSedeProgramada = "Sede Principal", CentroDeCostosProgramado = "CC-100" };

    private static MarcacionDelDia MarcacionConSede() =>
        Marcacion() with { CodigoSede = "001", NombreSede = "Sede Principal", CentroDeCostos = "CC-100" };

    private static HorasDiscriminadas Horas() =>
        new(new Dictionary<string, decimal> { ["DominicalFestivaDiurna"] = 7.00m }, ["nota"]);

    protected override DiaDepurado CrearInstancia() =>
        new("EMP-001", Fecha, Colaborador, "Turno Manana", [Franja()], [Marcacion()], Horas());

    protected override DiaDepurado CrearInstanciaCopia() =>
        new("EMP-001", Fecha, Colaborador, "Turno Manana", [Franja()], [Marcacion()], Horas());

    protected override IEnumerable<(string, DiaDepurado)> CrearInstanciasDiferentes()
    {
        yield return ("CodigoColaborador",
            new DiaDepurado("EMP-002", Fecha, Colaborador, "Turno Manana", [Franja()], [Marcacion()], Horas()));
        yield return ("Fecha",
            new DiaDepurado("EMP-001", Fecha.AddDays(1), Colaborador, "Turno Manana", [Franja()], [Marcacion()], Horas()));
        yield return ("Colaborador",
            new DiaDepurado("EMP-001", Fecha, null, "Turno Manana", [Franja()], [Marcacion()], Horas()));
        yield return ("NombreTurno",
            new DiaDepurado("EMP-001", Fecha, Colaborador, "Otro Turno", [Franja()], [Marcacion()], Horas()));
        yield return ("Franjas (vacia)",
            new DiaDepurado("EMP-001", Fecha, Colaborador, "Turno Manana", [], [Marcacion()], Horas()));
        yield return ("Marcaciones (vacia)",
            new DiaDepurado("EMP-001", Fecha, Colaborador, "Turno Manana", [Franja()], [], Horas()));
        yield return ("HorasDiscriminadas",
            new DiaDepurado("EMP-001", Fecha, Colaborador, "Turno Manana", [Franja()], [Marcacion()],
                new HorasDiscriminadas(new Dictionary<string, decimal>(), [])));
        // CA-6: difiere SOLO en los campos nuevos de sede dentro de una Franja/Marcacion.
        yield return ("Franjas (sede programada)",
            new DiaDepurado("EMP-001", Fecha, Colaborador, "Turno Manana", [FranjaConSedeProgramada()], [Marcacion()], Horas()));
        yield return ("Marcaciones (sede)",
            new DiaDepurado("EMP-001", Fecha, Colaborador, "Turno Manana", [Franja()], [MarcacionConSede()], Horas()));
    }

    // Cobertura especifica del override: las colecciones se comparan por valor, no por referencia.

    [Fact]
    public void Equals_RetornaTrue_CuandoColeccionesSonInstanciasDiferentesConMismoContenido()
    {
        var a = new DiaDepurado("EMP-001", Fecha, Colaborador, "Turno Manana", [Franja()], [Marcacion()], Horas());
        var b = new DiaDepurado("EMP-001", Fecha, Colaborador, "Turno Manana", [Franja()], [Marcacion()], Horas());

        a.Equals(b).Should().BeTrue();
    }

    [Fact]
    public void GetHashCode_RetornaMismoHash_CuandoColeccionesSonInstanciasDiferentesConMismoContenido()
    {
        var a = new DiaDepurado("EMP-001", Fecha, Colaborador, "Turno Manana", [Franja()], [Marcacion()], Horas());
        var b = new DiaDepurado("EMP-001", Fecha, Colaborador, "Turno Manana", [Franja()], [Marcacion()], Horas());

        a.GetHashCode().Should().Be(b.GetHashCode());
    }
}
