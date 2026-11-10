using Bitakora.ControlAsistencia.Projections.Infraestructura;

var builder = Host.CreateApplicationBuilder(args);

// El mismo secreto marten-connection del write-side: el read-side comparte conexion y schema con
// cada dominio (MEF-ADR-0034 seccion 2).
var martenConnectionString = Environment.GetEnvironmentVariable("MartenConnectionString")!;

builder.Services.ConfigurarEventos(martenConnectionString);
builder.Services.ConfigurarObservabilidad();

await builder.Build().RunAsync();
