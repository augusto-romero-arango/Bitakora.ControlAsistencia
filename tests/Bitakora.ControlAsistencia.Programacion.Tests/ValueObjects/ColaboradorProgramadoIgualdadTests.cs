// Issue #319: Tests de contrato IEquatable para Empleado (record propio de Programacion.DomainEvents,
// tres islas). Todos los campos son string: la igualdad por valor del record por defecto ya es
// correcta, sin Equals/GetHashCode custom -- mismo criterio que DetalleColaborador (issue #318).

using Bitakora.ControlAsistencia.Programacion.DomainEvents;

namespace Bitakora.ControlAsistencia.Programacion.Tests.ValueObjects;

public class ColaboradorProgramadoIgualdadTests : IgualdadTestBase<ColaboradorProgramado>
{
    protected override ColaboradorProgramado CrearInstancia() =>
        new("EMP-001", "CC", "1234567890", "Luis Augusto", "Barreto");

    protected override ColaboradorProgramado CrearInstanciaCopia() =>
        new("EMP-001", "CC", "1234567890", "Luis Augusto", "Barreto");

    protected override IEnumerable<(string, ColaboradorProgramado)> CrearInstanciasDiferentes()
    {
        yield return ("EmpleadoId",
            new ColaboradorProgramado("EMP-002", "CC", "1234567890", "Luis Augusto", "Barreto"));
        yield return ("TipoIdentificacion",
            new ColaboradorProgramado("EMP-001", "CE", "1234567890", "Luis Augusto", "Barreto"));
        yield return ("NumeroIdentificacion",
            new ColaboradorProgramado("EMP-001", "CC", "9999999999", "Luis Augusto", "Barreto"));
        yield return ("Nombres",
            new ColaboradorProgramado("EMP-001", "CC", "1234567890", "Otro Nombre", "Barreto"));
        yield return ("Apellidos",
            new ColaboradorProgramado("EMP-001", "CC", "1234567890", "Luis Augusto", "Otro Apellido"));
    }
}
