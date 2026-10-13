using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Hosting;

var builder = FunctionsApplication.CreateBuilder(args);
builder.ConfigureFunctionsWebApplication();

// Debe correr justo despues de ConfigureFunctionsWebApplication() (para que Items ya traiga el
// ToolInvocationContext que deja FunctionsMcpContextMiddleware) y antes de cualquier middleware
// que haga BindInputAsync<ToolInvocationContext>: ese bind cachea el ConversionResult en
// IBindingCache bajo el nombre del parametro, y la tool recibiria el diccionario coercionado.
builder.UseMiddleware<ArgumentosCrudosMcpMiddleware>();

builder.Services.ConfigurarServiciosMcp(builder.Configuration);
builder.Services.ConfigurarObservabilidadMcp();
builder.UseMiddleware<AutorizacionMcpMiddleware>();

// Deriva la identidad del usuario autenticado (org_id/sub/organization_membership_id) para cada
// tool call y la puebla en el ambiente (TenantExecutionContext); PropagadorIdentidadTenantHandler
// la prefiere sobre el tenant fijo (MEF-ADR-0047 decision 6, CA-ADR-0032).
builder.UseMiddleware<IdentidadTenantMcpMiddleware>();

await builder.Build().RunAsync();
