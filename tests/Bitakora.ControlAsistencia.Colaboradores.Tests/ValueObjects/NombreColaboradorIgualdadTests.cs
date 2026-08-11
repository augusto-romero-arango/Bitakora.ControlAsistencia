// HU-348: Igualdad por valor de NombreColaborador (CA-5).
using Bitakora.ControlAsistencia.Colaboradores.DomainEvents;

namespace Bitakora.ControlAsistencia.Colaboradores.Tests.ValueObjects;

/// <summary>
/// Hereda los 8 tests de IgualdadTestBase que verifican el contrato IEquatable completo.
/// </summary>
public class NombreColaboradorIgualdadTests : IgualdadTestBase<NombreColaborador>
{
    protected override NombreColaborador CrearInstancia() =>
        NombreColaborador.Crear("Luis", "Augusto", "Barreto", "Gomez");

    protected override NombreColaborador CrearInstanciaCopia() =>
        NombreColaborador.Crear("Luis", "Augusto", "Barreto", "Gomez");

    protected override IEnumerable<(string, NombreColaborador)> CrearInstanciasDiferentes()
    {
        yield return ("PrimerNombre", NombreColaborador.Crear("Carlos", "Augusto", "Barreto", "Gomez"));
        yield return ("SegundoNombre", NombreColaborador.Crear("Luis", "Andres", "Barreto", "Gomez"));
        yield return ("PrimerApellido", NombreColaborador.Crear("Luis", "Augusto", "Romero", "Gomez"));
        yield return ("SegundoApellido", NombreColaborador.Crear("Luis", "Augusto", "Barreto", "Arango"));
        yield return ("SegundoNombreAusente", NombreColaborador.Crear("Luis", null, "Barreto", "Gomez"));
    }
}
