// Issue #112: Tests de igualdad por valor para IntervaloTemporal
using Bitakora.ControlAsistencia.Contracts.ControlHoras.ValueObjects;

namespace Bitakora.ControlAsistencia.Contracts.Tests.ValueObjects.ControlHoras;

/// <summary>
/// Igualdad por valor de IntervaloTemporal (Inicio + Fin).
/// Hereda los 8 tests de IgualdadTestBase que verifican el contrato IEquatable completo.
/// </summary>
public class IntervaloTemporalIgualdadTests : IgualdadTestBase<IntervaloTemporal>
{
    private static readonly MomentoDelDia Las8 = new(new TimeOnly(8, 0));
    private static readonly MomentoDelDia Las17 = new(new TimeOnly(17, 0));

    protected override IntervaloTemporal CrearInstancia() =>
        IntervaloTemporal.Crear(Las8, Las17);

    protected override IntervaloTemporal CrearInstanciaCopia() =>
        IntervaloTemporal.Crear(Las8, Las17);

    protected override IEnumerable<(string, IntervaloTemporal)> CrearInstanciasDiferentes()
    {
        yield return ("Inicio",
            IntervaloTemporal.Crear(new MomentoDelDia(new TimeOnly(9, 0)), Las17));
        yield return ("Fin",
            IntervaloTemporal.Crear(Las8, new MomentoDelDia(new TimeOnly(18, 0))));
        yield return ("OffsetFin",
            IntervaloTemporal.Crear(Las8, new MomentoDelDia(new TimeOnly(6, 0), 1)));
    }
}
