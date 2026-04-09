using System.Text.Json;
using Bitakora.ControlAsistencia.ControlHoras;
using Bitakora.ControlAsistencia.ControlHoras.Infraestructura;
using Cosmos.EventDriven.CritterStack;
using Cosmos.EventDriven.CritterStack.AzureServiceBus;
using Cosmos.EventSourcing.CritterStack;
using Cosmos.EventSourcing.CritterStack.Commands;
using FluentValidation;
using Marten;
using Microsoft.Azure.Functions.Worker.Builder;
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

builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddSource("Wolverine")
        .AddSource("Marten")
        .AddSource("Bitakora.ControlAsistencia.ControlHoras.*"));

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
