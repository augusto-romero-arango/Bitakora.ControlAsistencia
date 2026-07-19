using Bitakora.ControlAsistencia.ControlHoras.Infraestructura;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Hosting;

var builder = FunctionsApplication.CreateBuilder(args);
builder.ConfigureFunctionsWebApplication();

var martenConnectionString = Environment.GetEnvironmentVariable("MartenConnectionString")!;
var serviceBusConnectionString = Environment.GetEnvironmentVariable("SERVICE_BUS_CONNECTION")!;

builder.Services.AgregarServiciosControlHoras(
    martenConnectionString,
    serviceBusConnectionString,
    builder.Environment.IsDevelopment());

await builder.Build().RunAsync();
