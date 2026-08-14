// HU-348: Igualdad por valor de Identificacion.
// Issue #381 (CA-3): la limpieza del numero (elimina no-alfanumerico, letras a MAYUSCULAS)
// REEMPLAZA la normalizacion trim+MAYUSCULAS de #348 -- ahora "AB-123" y "ab123" son la MISMA
// identidad porque ambos limpian a "AB123", no porque el guion se conserve entre ambos.
using Bitakora.ControlAsistencia.Colaboradores.DomainEvents;

namespace Bitakora.ControlAsistencia.Colaboradores.Tests.ValueObjects;

/// <summary>
/// Hereda los 8 tests de IgualdadTestBase que verifican el contrato IEquatable completo.
/// CA-3: la limpieza (elimina no-alfanumerico + MAYUSCULAS) hace que "AB-123" y "ab123" sean la
/// misma identidad (ambos limpian a "AB123"). El tipo tambien participa en la igualdad: CC-AB123
/// != CE-AB123.
/// </summary>
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
