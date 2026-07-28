using Bitakora.ControlAsistencia.Projections.Infraestructura;

var builder = Host.CreateApplicationBuilder(args);

// Mismo secreto marten-connection ya custodiado en el Key Vault del BC (MEF-ADR-0025); el
// named store del read-side reutiliza la misma conexion y schema que el write-side de cada
// dominio (MEF-ADR-0034 seccion 2) -- no hay connection string nueva.
var martenConnectionString = Environment.GetEnvironmentVariable("MartenConnectionString")!;

builder.Services.ConfigurarEventos(martenConnectionString);

await builder.Build().RunAsync();
