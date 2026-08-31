// Resolver mono-tenant transitorio de ControlHoras (MEF-ADR-0028 seccion 2, CA-ADR-0027). Sin este resolver
// registrado en el DI, los routers/senders de Wolverine no pueden construirse (breaking change de
// Cosmos.Event* 2.x, issue #219).
// TenantId debe resolver al tenant por defecto de Marten (JasperFx.StorageConstants.DefaultTenantId,
// valor "*DEFAULT*"), que Marten mapea a Tenancy.Default aun con AllDocumentsAreMultiTenanted().

using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.Infraestructura;
using Cosmos.MultiTenancy;
using JasperFx;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.Infraestructura;

public class TenantResolverMonoTenantPorDefectoTests
{
    [Fact]
    public void TenantId_RetornaElTenantPorDefectoDeMarten()
    {
        ITenantResolver resolver = new TenantResolverMonoTenantPorDefecto();

        resolver.TenantId.Should().Be(StorageConstants.DefaultTenantId);
    }

    [Fact]
    public void UserId_RetornaUsuarioNoAutenticado()
    {
        ITenantResolver resolver = new TenantResolverMonoTenantPorDefecto();

        resolver.UserId.Should().Be("usuario-no-autenticado");
    }
}
