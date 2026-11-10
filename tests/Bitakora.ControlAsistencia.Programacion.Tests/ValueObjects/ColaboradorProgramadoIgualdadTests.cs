// Todos los campos son string: la igualdad por valor del record por defecto basta, sin
// Equals/GetHashCode custom.

using Bitakora.ControlAsistencia.Programacion.DomainEvents;

namespace Bitakora.ControlAsistencia.Programacion.Tests.ValueObjects;

public class ColaboradorProgramadoIgualdadTests : IgualdadTestBase<ColaboradorProgramado>
{
    protected override ColaboradorProgramado CrearInstancia() =>
        new("CC-1234567890", "EMP-001", "Luis Augusto Barreto");

    protected override ColaboradorProgramado CrearInstanciaCopia() =>
        new("CC-1234567890", "EMP-001", "Luis Augusto Barreto");

    protected override IEnumerable<(string, ColaboradorProgramado)> CrearInstanciasDiferentes()
    {
        yield return ("Identificacion",
            new ColaboradorProgramado("CE-9999999999", "EMP-001", "Luis Augusto Barreto"));
        yield return ("CodigoColaborador",
            new ColaboradorProgramado("CC-1234567890", "EMP-002", "Luis Augusto Barreto"));
        yield return ("NombreCompleto",
            new ColaboradorProgramado("CC-1234567890", "EMP-001", "Otro Nombre Apellido"));
    }
}
