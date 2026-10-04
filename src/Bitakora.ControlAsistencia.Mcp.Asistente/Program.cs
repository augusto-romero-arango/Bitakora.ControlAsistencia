using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = FunctionsApplication.CreateBuilder(args);
builder.ConfigureFunctionsWebApplication();

// Debe correr justo despues de ConfigureFunctionsWebApplication() (para que Items ya traiga el
// ToolInvocationContext que deja FunctionsMcpContextMiddleware) y antes de cualquier middleware
// que haga BindInputAsync<ToolInvocationContext>: ese bind cachea el ConversionResult en
// IBindingCache bajo el nombre del parametro, y la tool recibiria el diccionario coercionado.
builder.UseMiddleware<ArgumentosCrudosMcpMiddleware>();

builder.Services.ConfigurarIdentidadTenant(builder.Configuration);
builder.Services.ConfigurarClientesHttp(builder.Configuration);
builder.Services.ConfigurarObservabilidadMcp();

// Defensa en profundidad (MEF-ADR-0047 decision 7): el gate real vive en la politica dedicada de
// APIM (MEF-ADR-0032 seccion 9). ValidateAudience = false -- la audiencia ya la exige esa politica.
// Sin Mcp__AuthorizationServer resoluble el validador degrada a "todo token es invalido"; no
// fail-fast de arranque, a diferencia de las base URLs de los clientes tipados: aquellas sin las
// que ninguna tool puede responder, esta solo apaga una defensa secundaria.
builder.Services.AddSingleton(
    ValidadorTokenAuthKit.ParaAuthorizationServer(builder.Configuration["Mcp:AuthorizationServer"]));
builder.UseMiddleware<AutorizacionMcpMiddleware>();

await builder.Build().RunAsync();
