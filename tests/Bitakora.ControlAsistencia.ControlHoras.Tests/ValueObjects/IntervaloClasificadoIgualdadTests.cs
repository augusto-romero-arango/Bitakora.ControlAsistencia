// Issue #114: Tests de contrato IEquatable para IntervaloClasificado.
// IntervaloClasificado es record - autogenera IEquatable<T>.
// Hereda los 8 tests de IgualdadTestBase que verifican el contrato completo.
using Bitakora.ControlAsistencia.ControlHoras.ValueObjects;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.ValueObjects;

public class IntervaloClasificadoIgualdadTests : IgualdadTestBase<IntervaloClasificado>
{
    private static readonly IntervaloTemporal IntervaloDiurno =
        IntervaloTemporal.Crear(new MomentoDelDia(new TimeOnly(8, 0)), new MomentoDelDia(new TimeOnly(17, 0)));

    private static readonly IntervaloTemporal IntervaloNocturno =
        IntervaloTemporal.Crear(new MomentoDelDia(new TimeOnly(22, 0)), new MomentoDelDia(new TimeOnly(6, 0), 1));

    protected override IntervaloClasificado CrearInstancia() =>
        new(IntervaloDiurno, Concepto.OrdinariaDiurna);

    protected override IntervaloClasificado CrearInstanciaCopia() =>
        new(IntervaloDiurno, Concepto.OrdinariaDiurna);

    protected override IEnumerable<(string, IntervaloClasificado)> CrearInstanciasDiferentes()
    {
        yield return ("Intervalo", new IntervaloClasificado(IntervaloNocturno, Concepto.OrdinariaDiurna));
        yield return ("Concepto", new IntervaloClasificado(IntervaloDiurno, Concepto.OrdinariaNocturna));
    }
}
