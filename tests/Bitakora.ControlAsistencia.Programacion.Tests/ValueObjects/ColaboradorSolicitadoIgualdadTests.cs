// Tests de contrato IEquatable para ColaboradorSolicitado, el DTO del body de POST
// programacion/solicitudes (issue #436). Todos los campos son string: la igualdad por valor del
// record por defecto ya es correcta, sin Equals/GetHashCode custom. El dia que gane una coleccion,
// esa igualdad pasa a comparar por referencia y estos tests lo delatan (MEF-ADR-0012).

using Bitakora.ControlAsistencia.Programacion.SolicitarProgramacionTurnoFunction;

namespace Bitakora.ControlAsistencia.Programacion.Tests.ValueObjects;

public class ColaboradorSolicitadoIgualdadTests : IgualdadTestBase<ColaboradorSolicitado>
{
    protected override ColaboradorSolicitado CrearInstancia() =>
        new("CC-1234567890", "EMP-001", "Luis Augusto Barreto");

    protected override ColaboradorSolicitado CrearInstanciaCopia() =>
        new("CC-1234567890", "EMP-001", "Luis Augusto Barreto");

    protected override IEnumerable<(string, ColaboradorSolicitado)> CrearInstanciasDiferentes()
    {
        yield return ("Identificacion",
            new ColaboradorSolicitado("CE-9999999999", "EMP-001", "Luis Augusto Barreto"));
        yield return ("CodigoColaborador",
            new ColaboradorSolicitado("CC-1234567890", "EMP-002", "Luis Augusto Barreto"));
        yield return ("NombreCompleto",
            new ColaboradorSolicitado("CC-1234567890", "EMP-001", "Otro Nombre Apellido"));
    }
}
