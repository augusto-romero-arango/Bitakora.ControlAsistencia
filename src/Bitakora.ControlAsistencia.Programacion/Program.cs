using Cosmos.EventDriven.CritterStack;
using Cosmos.EventDriven.CritterStack.AzureServiceBus;
using Cosmos.EventSourcing.CritterStack;
using Cosmos.EventSourcing.CritterStack.Commands;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Trace;
using Bitakora.ControlAsistencia.Programacion;

var builder = FunctionsApplication.CreateBuilder(args);
builder.ConfigureFunctionsWebApplication();

var martenConnectionString = Environment.GetEnvironmentVariable("MartenConnectionString")!;
var serviceBusConnectionString = Environment.GetEnvironmentVariable("SERVICE_BUS_CONNECTION")!;

builder.Services.AgregarWolverineParaComandosServerless(
    typeof(IProgramacionAssemblyMarker).Assembly,
    martenConnectionString,
    "programacion",
    builder.Environment.IsDevelopment(),
    options =>
    {
        options.HabilitarAzureServiceBusParaServerLess(serviceBusConnectionString);
        // options.PublicarEventoServerless<MiEvento>("eventos-programacion");
    });

builder.Services.AgregarMartenEventStore();
builder.Services.AgregarWolverineCommandRouter();
builder.Services.AgregarWolverineEventSender();

builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddSource("Wolverine")
        .AddSource("Marten")
        .AddSource("Bitakora.ControlAsistencia.Programacion.*"));

await builder.Build().RunAsync();
