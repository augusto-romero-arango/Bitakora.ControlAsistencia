// Issue #219: ITenantResolver mono-tenant para Programacion.
// Sin este resolver registrado en el DI, WolverineCommandRouter no puede construirse (breaking
// change de Cosmos.Event* 2.x). Ver docs/bitacora/field-notes/2026-07-18-1905-bug-investigation.md.
//
// TenantId debe resolver al tenant por defecto de Marten (JasperFx.StorageConstants.DefaultTenantId,
// valor "*DEFAULT*"), que Marten mapea a Tenancy.Default aun con AllDocumentsAreMultiTenanted().
// UserId es un valor fijo ("sin-identificar") porque el proyecto no distingue usuarios.

using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Cosmos.MultiTenancy;
using JasperFx;

namespace Bitakora.ControlAsistencia.Programacion.Tests.Infraestructura;

public class TenantResolverFijoTests
{
    [Fact]
    public void TenantId_RetornaElTenantPorDefectoDeMarten()
    {
        ITenantResolver resolver = new TenantResolverFijo();

        resolver.TenantId.Should().Be(StorageConstants.DefaultTenantId);
    }

    [Fact]
    public void UserId_RetornaSinIdentificar()
    {
        ITenantResolver resolver = new TenantResolverFijo();

        resolver.UserId.Should().Be("sin-identificar");
    }
}
