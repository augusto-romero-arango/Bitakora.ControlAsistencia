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

// Fuera del seam: es middleware del worker, no IServiceCollection. Puebla la identidad que lee el
// ITenantContext del seam.
builder.UsarTenantContextMiddleware();

await builder.Build().RunAsync();
