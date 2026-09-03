using Bitakora.ControlAsistencia.Mcp.Comandos.Infraestructura;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = FunctionsApplication.CreateBuilder(args);
builder.ConfigureFunctionsWebApplication();

builder.Services.ConfigurarIdentidadTenant(builder.Configuration);
builder.Services.ConfigurarClientesHttp(builder.Configuration);
builder.Services.ConfigurarObservabilidadMcp();

// Defensa en profundidad (MEF-ADR-0047 decision 7): el gate real vive en la politica dedicada de
// APIM (MEF-ADR-0032 seccion 9). ValidateAudience = false -- la audiencia ya la exige esa politica.
// Sin Mcp__AuthorizationServer resoluble el validador degrada a "todo token es invalido"; no
// fail-fast de arranque, a diferencia de las base URLs de los clientes tipados: aquellas sin las
// que ninguna tool puede responder, esta solo apaga una defensa secundaria.
builder.Services.AddSingleton<IValidadorTokenAuthKit>(
    ValidadorTokenAuthKit.ParaAuthorizationServer(builder.Configuration["Mcp:AuthorizationServer"]));
builder.UseMiddleware<AutorizacionMcpMiddleware>();

// Deriva la identidad del usuario autenticado (org_id/sub) para cada tool call y la puebla en el
// ambiente (TenantExecutionContext); PropagadorIdentidadTenantHandler la prefiere sobre el tenant
// fijo interino (MEF-ADR-0047 decision 6, issue #572).
builder.UseMiddleware<IdentidadTenantMcpMiddleware>();

// Restaura el texto original de los argumentos string que la extension MCP coerciona a
// DateTimeOffset/Guid (issue #586). Debe correr despues de ConfigureFunctionsWebApplication() para
// que context.Items ya traiga el ToolInvocationContext bindeado por FunctionsMcpContextMiddleware.
builder.UseMiddleware<ArgumentosCrudosMcpMiddleware>();

await builder.Build().RunAsync();
