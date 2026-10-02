namespace Bitakora.ControlAsistencia.TenantResolver.Tests;

public class OrganizationMembershipIdTests
{
    [Fact]
    public async Task OrganizationMembershipId_RetornaElValorPoblado_CuandoSePoblaDerivado()
    {
        TenantExecutionContext.SetDerivedIdentity("tenant-A", "user-A", "om_A");

        await Task.Yield();
        var delHandler = new TenantExecutionContext();

        Assert.Equal("om_A", delHandler.OrganizationMembershipId);
    }

    [Fact]
    public void OrganizationMembershipId_LanzaInvalidOperation_CuandoNoSePoblo()
    {
        TenantExecutionContext.Set("tenant-B", "user-B");
        var ctx = new TenantExecutionContext();

        var ex = Assert.Throws<InvalidOperationException>(() => ctx.OrganizationMembershipId);

        Assert.Contains("X-Organization-Membership-Id", ex.Message);
        Assert.Contains("organization_membership_id", ex.Message);
    }

    [Fact]
    public void Middleware_PueblaLosTresDatos_CuandoLosHeadersHttpEstanPresentes()
    {
        var headers = new Dictionary<string, string?>
        {
            ["X-Tenant-Id"] = "tenant-H",
            ["X-User-Id"] = "user-H",
            ["X-Organization-Membership-Id"] = "om_H",
        };

        TenantContextMiddleware.PoblarDesdeHttp(h => headers.GetValueOrDefault(h));
        var ctx = new TenantExecutionContext();

        Assert.Equal("tenant-H", ctx.TenantId);
        Assert.Equal("user-H", ctx.UserId);
        Assert.Equal("om_H", ctx.OrganizationMembershipId);
    }

    [Fact]
    public void Middleware_DejaMembershipAusente_CuandoElHeaderHttpNoViene()
    {
        var headers = new Dictionary<string, string?>
        {
            ["X-Tenant-Id"] = "tenant-H",
            ["X-User-Id"] = "user-H",
        };

        TenantContextMiddleware.PoblarDesdeHttp(h => headers.GetValueOrDefault(h));
        var ctx = new TenantExecutionContext();

        Assert.Equal("tenant-H", ctx.TenantId);
        Assert.Throws<InvalidOperationException>(() => ctx.OrganizationMembershipId);
    }

    [Fact]
    public void Middleware_PueblaLosTresDatos_CuandoLasApplicationPropertiesTienenLosTres()
    {
        var props = new Dictionary<string, object>
        {
            ["tenant-id"] = "tenant-S",
            ["user_id"] = "user-S",
            ["organization_membership_id"] = "om_S",
        };

        TenantContextMiddleware.PoblarDesdeServiceBus(props);
        var ctx = new TenantExecutionContext();

        Assert.Equal("tenant-S", ctx.TenantId);
        Assert.Equal("user-S", ctx.UserId);
        Assert.Equal("om_S", ctx.OrganizationMembershipId);
    }

    [Fact]
    public void Middleware_DejaMembershipAusente_CuandoLaApplicationPropertyNoViene()
    {
        var props = new Dictionary<string, object>
        {
            ["tenant-id"] = "tenant-S",
            ["user_id"] = "user-S",
        };

        TenantContextMiddleware.PoblarDesdeServiceBus(props);
        var ctx = new TenantExecutionContext();

        Assert.Equal("tenant-S", ctx.TenantId);
        Assert.Throws<InvalidOperationException>(() => ctx.OrganizationMembershipId);
    }
}
