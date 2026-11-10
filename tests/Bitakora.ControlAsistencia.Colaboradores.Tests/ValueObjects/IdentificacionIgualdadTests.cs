// La limpieza del numero (quita no-alfanumericos, letras a MAYUSCULAS) hace que "AB-123" y "ab123"
// sean la misma identidad.

using Bitakora.ControlAsistencia.Colaboradores.DomainEvents;

namespace Bitakora.ControlAsistencia.Colaboradores.Tests.ValueObjects;

public class IdentificacionIgualdadTests : IgualdadTestBase<Identificacion>
{
    protected override Identificacion CrearInstancia() =>
        Identificacion.Crear(TipoIdentificacion.CC, "AB-123");

    protected override Identificacion CrearInstanciaCopia() =>
        Identificacion.Crear(TipoIdentificacion.CC, "ab123");

    protected override IEnumerable<(string, Identificacion)> CrearInstanciasDiferentes()
    {
        yield return ("Tipo", Identificacion.Crear(TipoIdentificacion.CE, "AB-123"));
        yield return ("Numero", Identificacion.Crear(TipoIdentificacion.CC, "XY-999"));
    }
}
