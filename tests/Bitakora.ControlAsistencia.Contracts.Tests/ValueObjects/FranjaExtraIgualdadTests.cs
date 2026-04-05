// Issue #2: Tests de igualdad por valor para FranjaExtra
using Bitakora.ControlAsistencia.Contracts.ValueObjects;

namespace Bitakora.ControlAsistencia.Contracts.Tests.ValueObjects;

public class FranjaExtraIgualdadTests : IgualdadTestBase<FranjaExtra>
{
    protected override FranjaExtra CrearInstancia() =>
        FranjaExtra.Crear(new TimeOnly(6, 0), new TimeOnly(8, 0));

    protected override FranjaExtra CrearInstanciaCopia() =>
        FranjaExtra.Crear(new TimeOnly(6, 0), new TimeOnly(8, 0));

    protected override IEnumerable<(string, FranjaExtra)> CrearInstanciasDiferentes()
    {
        yield return ("HoraInicio",
            FranjaExtra.Crear(new TimeOnly(7, 0), new TimeOnly(8, 0)));
        yield return ("HoraFin",
            FranjaExtra.Crear(new TimeOnly(6, 0), new TimeOnly(9, 0)));
        yield return ("OffsetInicio",
            FranjaExtra.Crear(new TimeOnly(4, 0), new TimeOnly(6, 0), diaOffsetInicio: 1, diaOffsetFin: 0));
        yield return ("OffsetFin",
            FranjaExtra.Crear(new TimeOnly(4, 0), new TimeOnly(6, 0), diaOffsetInicio: 0, diaOffsetFin: 1));
    }
}
