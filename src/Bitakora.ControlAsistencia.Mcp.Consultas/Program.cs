using System.Globalization;
using Azure.Monitor.OpenTelemetry.Exporter;
using Bitakora.ControlAsistencia.Mcp.Consultas.Infraestructura;
using Bitakora.ControlAsistencia.Mcp.Consultas.MetadataRecursoProtegido;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Azure.Functions.Worker.OpenTelemetry;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using OpenTelemetry;
using OpenTelemetry.Trace;

var builder = FunctionsApplication.CreateBuilder(args);
builder.ConfigureFunctionsWebApplication();

// AuthKit protege este servidor MCP (issue #554): mismo issuer client-specific verificado en vivo
// para el gateway de APIM (MEF-ADR-0032 B5) -- nunca el generico "https://api.workos.com".
const string AuthorizationServerAuthKit =
    "https://api.workos.com/user_management/client_01M1CKPECJ5DBRMS3ZVFRQW8GW";
var authorizationServerUri = new Uri(AuthorizationServerAuthKit);

// La identidad publica de este servidor (para el documento PRM y el WWW-Authenticate) llega por
// app setting: Azure Functions no expone su propia URL publica en tiempo de arranque.
var resourceUri = new Uri(
    builder.Configuration["Mcp:ResourceUri"]
    ?? throw new InvalidOperationException("Falta el app setting Mcp__ResourceUri"));
var prmUri = new Uri(resourceUri, "/api/.well-known/oauth-protected-resource");

builder.Services.AddSingleton(new ConstructorMetadataRecursoProtegido(resourceUri, authorizationServerUri));
builder.Services.AddSingleton(prmUri);

// ConfigurationManager<OpenIdConnectConfiguration> cachea el discovery doc/JWKS y los refresca
// periodicamente -- nunca se asume un issuer/llave fijos (MEF-ADR-0032 B5).
builder.Services.AddSingleton<IConfigurationManager<OpenIdConnectConfiguration>>(
    new ConfigurationManager<OpenIdConnectConfiguration>(
        $"{authorizationServerUri}/.well-known/openid-configuration",
        new OpenIdConnectConfigurationRetriever()));
builder.Services.AddSingleton<IValidadorTokenAuthKit, ValidadorTokenAuthKit>();

// El middleware conserva la system key mcp_extension (defensa en profundidad, MEF-ADR-0047):
// desviacion documentada en el resumen del pipeline (seguimiento harness#797).
builder.UseMiddleware<AutorizacionMcpMiddleware>();

builder.Services.AddSingleton(ConfiguracionIdentidadTenant.Leer(
    builder.Configuration["Tenant:Id"], builder.Configuration["Tenant:UserId"]));
// Transient, no Singleton: HttpClientFactory desecha la cadena de handlers cada vez que rota el
// pipeline de un cliente, asi que un Singleton quedaria desechado para los pipelines siguientes.
builder.Services.AddTransient<PropagadorIdentidadTenantHandler>();

// Un HttpClient tipado por dominio consumido (issue #502). Las base URLs llegan por app setting
// (Api__{Dominio}__BaseUrl), fijadas por Terraform en el provisionamiento (#508); el fallo por
// setting ausente es en el arranque, no en la primera tool call.
builder.Services.AddHttpClient<ProgramacionApi>(c => c.BaseAddress = LeerBaseUrl("Programacion"))
    .AddHttpMessageHandler<PropagadorIdentidadTenantHandler>();
builder.Services.AddHttpClient<SedesApi>(c => c.BaseAddress = LeerBaseUrl("Sedes"))
    .AddHttpMessageHandler<PropagadorIdentidadTenantHandler>();
builder.Services.AddHttpClient<ControlHorasApi>(c => c.BaseAddress = LeerBaseUrl("ControlHoras"))
    .AddHttpMessageHandler<PropagadorIdentidadTenantHandler>();
builder.Services.AddHttpClient<ColaboradoresApi>(c => c.BaseAddress = LeerBaseUrl("Colaboradores"))
    .AddHttpMessageHandler<PropagadorIdentidadTenantHandler>();

// El back jamas resuelve "hoy" (decision #373): quien lo hace es listar_colaboradores, con este
// reloj, en la zona del BC.
builder.Services.AddSingleton(TimeProvider.System);

// Observabilidad con el mismo control de costos que los dominios (CA-ADR-0009): sampling ratio
// configurable, y el SetSampler propio va DESPUES de UseAzureMonitorExporter() porque el exporter
// instala un RateLimitedSampler interno que pisaria al configurado antes (hallazgo issue #308).
var samplingRatio = double.TryParse(
    Environment.GetEnvironmentVariable("TELEMETRY_SAMPLING_RATIO"),
    NumberStyles.Float,
    CultureInfo.InvariantCulture,
    out var ratio) && ratio is >= 0.0 and <= 1.0
        ? ratio
        : 0.2;

builder.Services.AddOpenTelemetry()
    .UseFunctionsWorkerDefaults()
    .UseAzureMonitorExporter()
    .WithTracing(tracing => tracing
        .SetSampler(new ParentBasedSampler(new TraceIdRatioBasedSampler(samplingRatio))));

await builder.Build().RunAsync();

Uri LeerBaseUrl(string dominio)
{
    var clave = $"Api:{dominio}:BaseUrl";
    var valor = builder.Configuration[clave];
    return string.IsNullOrWhiteSpace(valor)
        ? throw new InvalidOperationException($"Falta el app setting Api__{dominio}__BaseUrl")
        : new Uri(valor);
}
