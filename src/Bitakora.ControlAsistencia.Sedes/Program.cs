using Bitakora.ControlAsistencia.Sedes.Infraestructura;
using Bitakora.ControlAsistencia.TenantResolver;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Hosting;

var builder = FunctionsApplication.CreateBuilder(args);
builder.ConfigureFunctionsWebApplication();

var martenConnectionString = Environment.GetEnvironmentVariable("MartenConnectionString")!;
var serviceBusConnectionString = Environment.GetEnvironmentVariable("SERVICE_BUS_CONNECTION")!;

builder.Services.AgregarServiciosSedes(
    martenConnectionString,
    serviceBusConnectionString,
    builder.Environment.IsDevelopment());

// Middleware del worker: no es IServiceCollection, asi que no puede vivir en el seam.
// UsarTenantContextMiddleware puebla la identidad que el ITenantResolver del seam luego lee.
builder.UsarTenantContextMiddleware();

await builder.Build().RunAsync();
