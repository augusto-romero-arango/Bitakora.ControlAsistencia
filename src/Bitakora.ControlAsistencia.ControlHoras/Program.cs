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
using OpenTelemetry.Trace;

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
// AgregarWolverineCommandRouter se conserva: RegistrarMarcacion (HTTP) lo sigue usando.
builder.Services.AgregarWolverineCommandRouter();
// Issue #209/#210 (ADR-0024 decision #8): los eventos privados intra-BC (MarcacionRegistrada,
// ProgramacionTurnoDiarioSolicitada) se consumen directo con IPrivateEventHandlerAsync via
// IPrivateEventRouter, sin comando espejo.
builder.Services.AgregarWolverinePrivateEventRouter();
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
//
// ADR-0009 Capa 2 (control de costos): con telemetryMode=OpenTelemetry el sampling de
// host.json (logging.applicationInsights) deja de aplicar, asi que el volumen de trazas se
// controla aqui con un sampler OTel. Ratio configurable via la app setting
// TELEMETRY_SAMPLING_RATIO (default 0.2); para un diagnostico puntual se sube a 1.0 y se baja
// despues. El daily cap (Capa 3) sigue siendo el tope duro y la alerta de spike (Capa 4) sigue
// activa. Nota: el sampling head-based tambien muestrea excepciones (a diferencia del sampling
// adaptativo clasico que las preservaba al 100%); el cap y la alerta de spike lo compensan.
var samplingRatio = double.TryParse(
    Environment.GetEnvironmentVariable("TELEMETRY_SAMPLING_RATIO"),
    System.Globalization.NumberStyles.Float,
    System.Globalization.CultureInfo.InvariantCulture,
    out var ratio) && ratio is >= 0.0 and <= 1.0
        ? ratio
        : 0.2;

builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .SetSampler(new ParentBasedSampler(new TraceIdRatioBasedSampler(samplingRatio)))
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
