namespace Bitakora.ControlAsistencia.TenantResolver.Tests;

/// <summary>
/// Reproduce el escenario real (MEF-ADR-0032): el middleware de Functions puebla la identidad en el scope
/// de la invocacion, pero Wolverine reconstruye el resolver en un IServiceScope propio (hijo de la
/// raiz) al generar los handlers -> es OTRA instancia. La identidad debe cruzar ese limite de scope,
/// por eso el estado vive en un AsyncLocal ambiente y no en campos de instancia.
/// </summary>
public class TenantExecutionContextTests
{
    [Fact]
    public async Task La_identidad_poblada_es_visible_desde_otra_instancia_aguas_abajo_en_el_flujo_async()
    {
        // Middleware (scope de Functions): puebla la identidad de la invocacion.
        TenantExecutionContext.Set("tenant-A", "user-A");

        // Aguas abajo, en la misma cadena async (como InvokeInlineAsync de Wolverine): OTRA instancia,
        // la que Wolverine construye en su scope hijo de la raiz.
        await Task.Yield();
        var delHandler = new TenantExecutionContext();

        Assert.Equal("tenant-A", delHandler.TenantId);
        Assert.Equal("user-A", delHandler.UserId);
    }

    [Fact]
    public void Sin_identidad_poblada_los_getters_fallan_ruidosamente()
    {
        var ctx = new TenantExecutionContext();

        Assert.Throws<InvalidOperationException>(() => ctx.TenantId);
        Assert.Throws<InvalidOperationException>(() => ctx.UserId);
    }

    // El caso de los webhooks: el gateway no estampo nada porque el proveedor no presenta un JWT, y
    // el tenant se derivo del payload ya verificado.
    [Fact]
    public async Task La_identidad_derivada_tambien_es_visible_aguas_abajo()
    {
        TenantExecutionContext.SetDerivedIdentity("tenant-derivado", "workos_webhook");

        await Task.Yield();
        var delSender = new TenantExecutionContext();

        Assert.Equal("tenant-derivado", delSender.TenantId);
        Assert.Equal("workos_webhook", delSender.UserId);
    }

    // Poblar a medias dejaria el fallo para el publish, que es mas lejos del error.
    [Theory]
    [InlineData("", "workos_webhook")]
    [InlineData("  ", "workos_webhook")]
    [InlineData("tenant-derivado", "")]
    [InlineData("tenant-derivado", "  ")]
    public void La_identidad_derivada_se_rechaza_incompleta(string tenantId, string actor)
        => Assert.Throws<ArgumentException>(
            () => TenantExecutionContext.SetDerivedIdentity(tenantId, actor));

    [Fact]
    public void TryObtener_RetornaFalseConAmbosNulos_CuandoNoHayIdentidadPoblada()
    {
        var obtuvo = TenantExecutionContext.TryObtener(out var tenantId, out var userId);

        Assert.False(obtuvo);
        Assert.Null(tenantId);
        Assert.Null(userId);
    }

    [Fact]
    public async Task TryObtener_RetornaTrueConLosValoresPoblados_CuandoLaIdentidadYaSeEstablecio()
    {
        TenantExecutionContext.SetDerivedIdentity("tenant-C", "user-C");

        await Task.Yield();
        var obtuvo = TenantExecutionContext.TryObtener(out var tenantId, out var userId);

        Assert.True(obtuvo);
        Assert.Equal("tenant-C", tenantId);
        Assert.Equal("user-C", userId);
    }
}
