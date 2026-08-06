// Contrato de igualdad de DetalleEmpleado, por simetria con la familia Detalle*IgualdadTests.
// A diferencia de sus hermanos, todos sus campos son string: la igualdad por valor del record por
// defecto ya es correcta y no necesita Equals/GetHashCode propios (MEF-ADR-0012). Estos tests
// congelan esa premisa: si el record gana una coleccion, el record la compararia por referencia
// y el test rojo lo delata.

using Bitakora.ControlAsistencia.PrivateEvents.Programacion;

namespace Bitakora.ControlAsistencia.PrivateEvents.Tests.Programacion;

public class DetalleEmpleadoIgualdadTests : IgualdadTestBase<DetalleEmpleado>
{
    protected override DetalleEmpleado CrearInstancia() =>
        new("EMP-001", "CC", "1234567890", "Luis Augusto", "Barreto");

    protected override DetalleEmpleado CrearInstanciaCopia() =>
        new("EMP-001", "CC", "1234567890", "Luis Augusto", "Barreto");

    protected override IEnumerable<(string, DetalleEmpleado)> CrearInstanciasDiferentes()
    {
        yield return ("EmpleadoId",
            new DetalleEmpleado("EMP-002", "CC", "1234567890", "Luis Augusto", "Barreto"));
        yield return ("TipoIdentificacion",
            new DetalleEmpleado("EMP-001", "CE", "1234567890", "Luis Augusto", "Barreto"));
        yield return ("NumeroIdentificacion",
            new DetalleEmpleado("EMP-001", "CC", "9999999999", "Luis Augusto", "Barreto"));
        yield return ("Nombres",
            new DetalleEmpleado("EMP-001", "CC", "1234567890", "Otro Nombre", "Barreto"));
        yield return ("Apellidos",
            new DetalleEmpleado("EMP-001", "CC", "1234567890", "Luis Augusto", "Otro Apellido"));
    }
}
