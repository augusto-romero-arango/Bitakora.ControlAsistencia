// Issue #2: Tests de igualdad por valor para FranjaDescanso
using Bitakora.ControlAsistencia.Contracts.ValueObjects;

namespace Bitakora.ControlAsistencia.Contracts.Tests.ValueObjects;

public class FranjaDescansoIgualdadTests : IgualdadTestBase<FranjaDescanso>
{
    protected override FranjaDescanso CrearInstancia() =>
        FranjaDescanso.Crear(new TimeOnly(10, 0), new TimeOnly(10, 15));

    protected override FranjaDescanso CrearInstanciaCopia() =>
        FranjaDescanso.Crear(new TimeOnly(10, 0), new TimeOnly(10, 15));

    protected override IEnumerable<(string, FranjaDescanso)> CrearInstanciasDiferentes()
    {
        yield return ("HoraInicio",
            FranjaDescanso.Crear(new TimeOnly(10, 5), new TimeOnly(10, 15)));
        yield return ("HoraFin",
            FranjaDescanso.Crear(new TimeOnly(10, 0), new TimeOnly(10, 30)));
        yield return ("OffsetInicio",
            FranjaDescanso.Crear(new TimeOnly(1, 0), new TimeOnly(1, 15), diaOffsetInicio: 1, diaOffsetFin: 0));
        yield return ("OffsetFin",
            FranjaDescanso.Crear(new TimeOnly(23, 50), new TimeOnly(0, 10), diaOffsetInicio: 0, diaOffsetFin: 1));
    }
}
