// Issue #112: Tests de igualdad por valor para MomentoDelDia
using Bitakora.ControlAsistencia.Contracts.ControlHoras.ValueObjects;

namespace Bitakora.ControlAsistencia.Contracts.Tests.ValueObjects.ControlHoras;

/// <summary>
/// CA-7: Dos MomentoDelDia con misma Hora y DiaOffset son iguales.
/// Hereda los 8 tests de IgualdadTestBase que verifican el contrato IEquatable completo.
/// </summary>
public class MomentoDelDiaIgualdadTests : IgualdadTestBase<MomentoDelDia>
{
    protected override MomentoDelDia CrearInstancia() =>
        new MomentoDelDia(new TimeOnly(8, 0));

    protected override MomentoDelDia CrearInstanciaCopia() =>
        new MomentoDelDia(new TimeOnly(8, 0));

    protected override IEnumerable<(string, MomentoDelDia)> CrearInstanciasDiferentes()
    {
        yield return ("Hora", new MomentoDelDia(new TimeOnly(9, 0)));
        yield return ("DiaOffset", new MomentoDelDia(new TimeOnly(8, 0), 1));
    }
}
