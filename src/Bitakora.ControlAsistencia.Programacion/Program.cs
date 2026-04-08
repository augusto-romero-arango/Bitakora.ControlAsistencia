using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Bitakora.ControlAsistencia.Contracts.Eventos;
using Bitakora.ControlAsistencia.Contracts.ValueObjects;
using Bitakora.ControlAsistencia.Programacion;
using Bitakora.ControlAsistencia.Programacion.CrearTurnoFunction.Eventos;
using Bitakora.ControlAsistencia.Programacion.Infraestructura;
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
    typeof(IProgramacionAssemblyMarker).Assembly,
    martenConnectionString,
    "programacion",
    builder.Environment.IsDevelopment(),
    options =>
    {
        options.HabilitarAzureServiceBusParaServerLess(serviceBusConnectionString);
        options.PublicarEventoServerless<ProgramacionTurnoDiarioSolicitada>(
            "programacion-turno-diario-solicitada");
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
            var resolver = new DefaultJsonTypeInfoResolver();
            SubFranja.ConfigurarSerializacion(resolver);
            FranjaOrdinaria.ConfigurarSerializacion(resolver);
            TurnoCreado.ConfigurarSerializacion(resolver);
            jsonOptions.TypeInfoResolver = resolver;
        });
    }
});

builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddSource("Wolverine")
        .AddSource("Marten")
        .AddSource("Bitakora.ControlAsistencia.Programacion.*"));

// Serializacion JSON global: camelCase hacia el cliente, case-insensitive en lectura
builder.Services.Configure<JsonSerializerOptions>(options =>
{
    options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.PropertyNameCaseInsensitive = true;
});

// Validacion de requests
builder.Services.AddScoped<IRequestValidator, RequestValidator>();
builder.Services.AddValidatorsFromAssemblyContaining<IProgramacionAssemblyMarker>();

await builder.Build().RunAsync();
