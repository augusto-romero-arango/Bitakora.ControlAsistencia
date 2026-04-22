// Issue #114: Tests de contrato IEquatable para DetalleRetardo.
// DetalleRetardo es sealed class con IEquatable<DetalleRetardo> manual y SequenceEqual en las listas.
// CrearInstanciaCopia construye intervalos equivalentes en instancias nuevas para verificar
// que la comparacion es por valor, no por referencia.
using Bitakora.ControlAsistencia.Contracts.ControlHoras.ValueObjects;

namespace Bitakora.ControlAsistencia.Contracts.Tests.ValueObjects.ControlHoras;

public class DetalleRetardoIgualdadTests : IgualdadTestBase<DetalleRetardo>
{
    private static IntervaloTemporal Retardo30Min() =>
        IntervaloTemporal.Crear(new MomentoDelDia(new TimeOnly(8, 0)), new MomentoDelDia(new TimeOnly(8, 30)));

    private static IntervaloTemporal Compensacion20Min() =>
        IntervaloTemporal.Crear(new MomentoDelDia(new TimeOnly(12, 0)), new MomentoDelDia(new TimeOnly(12, 20)));

    private static IntervaloTemporal Retardo15Min() =>
        IntervaloTemporal.Crear(new MomentoDelDia(new TimeOnly(9, 0)), new MomentoDelDia(new TimeOnly(9, 15)));

    private static IntervaloTemporal Compensacion10Min() =>
        IntervaloTemporal.Crear(new MomentoDelDia(new TimeOnly(13, 0)), new MomentoDelDia(new TimeOnly(13, 10)));

    protected override DetalleRetardo CrearInstancia() =>
        DetalleRetardo.Crear([Retardo30Min()], [Compensacion20Min()]);

    protected override DetalleRetardo CrearInstanciaCopia() =>
        DetalleRetardo.Crear([Retardo30Min()], [Compensacion20Min()]);

    protected override IEnumerable<(string, DetalleRetardo)> CrearInstanciasDiferentes()
    {
        yield return ("TiempoRetardado",
            DetalleRetardo.Crear([Retardo15Min()], [Compensacion10Min()]));
        yield return ("TiempoCompensado",
            DetalleRetardo.Crear([Retardo30Min()], [Compensacion10Min()]));
        yield return ("CantidadDeIntervalosRetardados",
            DetalleRetardo.Crear([Retardo30Min(), Retardo15Min()], [Compensacion20Min()]));
    }
}
