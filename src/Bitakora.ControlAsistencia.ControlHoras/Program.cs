using System.Text.Json;
using Azure.Monitor.OpenTelemetry.Exporter;
using Bitakora.ControlAsistencia.Contracts.ControlHoras.Eventos;
using Bitakora.ControlAsistencia.ControlHoras;
using Bitakora.ControlAsistencia.ControlHoras.Infraestructura;
using Cosmos.EventDriven.CritterStack;
using Cosmos.EventDriven.CritterStack.AzureServiceBus;
using Cosmos.EventSourcing.CritterStack;
using Cosmos.EventSourcing.CritterStack.Commands;
using FluentValidation;
using Marten;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Azure.Functions.Worker.OpenTelemetry;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = FunctionsApplication.CreateBuilder(args);
builder.ConfigureFunctionsWebApplication();

var martenConnectionString = Environment.GetEnvironmentVariable("MartenConnectionString")!;
var serviceBusConnectionString = Environment.GetEnvironmentVariable("SERVICE_BUS_CONNECTION")!;

builder.Services.AgregarWolverineParaComandosServerless(
    typeof(IControlHorasAssemblyMarker).Assembly,
    martenConnectionString,
    "control_horas",
    builder.Environment.IsDevelopment(),
    options =>
    {
        options.HabilitarAzureServiceBusParaServerLess(serviceBusConnectionString);
        // HU-108: registra el topic destino para DiaCalculado.
        // ADR-0004 + ADR-0005: un topic por evento, naming kebab-case en participio.
        options.PublicarEventoServerless<DiaCalculado>("dia-calculado");
    });

builder.Services.AgregarMartenEventStore();
builder.Services.AgregarWolverineCommandRouter();
builder.Services.AgregarWolverineEventSender();

// Registrar serializacion custom para tipos con constructores privados
builder.Services.ConfigureMarten(options =>
{
    if (options.Serializer() is Marten.Services.SystemTextJsonSerializer stj)
    {
        stj.Configure(jsonOptions =>
        {
            var resolver = new System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver();
            ConfiguracionSerializacionControlHoras.ConfigurarResolver(resolver);
            jsonOptions.TypeInfoResolver = resolver;
        });
    }
});

// Observabilidad: exporta las trazas del worker (Npgsql, Marten, Wolverine, handlers) a
// Application Insights via OpenTelemetry. Sin esto el proceso worker no emite dependencies
// ni el desglose de latencia por capa. Se usa el exporter -no el distro
// Azure.Monitor.OpenTelemetry.AspNetCore- para evitar request telemetry duplicado en el worker.
// Guia oficial: https://learn.microsoft.com/azure/azure-functions/opentelemetry-howto?pivots=programming-language-csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddSource("Wolverine")
        .AddSource("Marten")
        .AddSource("Npgsql")
        .AddSource("Bitakora.ControlAsistencia.ControlHoras.*"))
    .UseFunctionsWorkerDefaults()
    .UseAzureMonitorExporter();

// Serializacion JSON global: camelCase hacia el cliente, case-insensitive en lectura
builder.Services.Configure<JsonSerializerOptions>(options =>
{
    options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.PropertyNameCaseInsensitive = true;
});

// Validacion de requests
builder.Services.AddScoped<IRequestValidator, RequestValidator>();
builder.Services.AddValidatorsFromAssemblyContaining<IControlHorasAssemblyMarker>();

await builder.Build().RunAsync();
