// HU-348: Igualdad por valor de Identificacion (CA-1, CA-5).
using Bitakora.ControlAsistencia.Colaboradores.DomainEvents;

namespace Bitakora.ControlAsistencia.Colaboradores.Tests.ValueObjects;

/// <summary>
/// Hereda los 8 tests de IgualdadTestBase que verifican el contrato IEquatable completo.
/// CA-1: la normalizacion (trim + MAYUSCULAS) hace que " ab-123 " y "AB-123" sean la misma
/// identidad. CA-5: CC:123 != CE:123 (difiere el tipo).
/// </summary>
public class IdentificacionIgualdadTests : IgualdadTestBase<Identificacion>
{
    protected override Identificacion CrearInstancia() =>
        Identificacion.Crear(TipoIdentificacion.CC, " ab-123 ");

    protected override Identificacion CrearInstanciaCopia() =>
        Identificacion.Crear(TipoIdentificacion.CC, "AB-123");

    protected override IEnumerable<(string, Identificacion)> CrearInstanciasDiferentes()
    {
        yield return ("Tipo", Identificacion.Crear(TipoIdentificacion.CE, "AB-123"));
        yield return ("Numero", Identificacion.Crear(TipoIdentificacion.CC, "XY-999"));
    }
}
