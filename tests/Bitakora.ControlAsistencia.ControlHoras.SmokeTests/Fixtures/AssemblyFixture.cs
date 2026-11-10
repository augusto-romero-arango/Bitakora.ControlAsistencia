using Xunit.Sdk;
using Xunit.v3;
using Bitakora.ControlAsistencia.ControlHoras.SmokeTests.Fixtures;

[assembly: Parallelization(Mode = ParallelMode.None)]
[assembly: AssemblyFixture(typeof(ApiFixture))]
[assembly: AssemblyFixture(typeof(ServiceBusFixture))]
[assembly: AssemblyFixture(typeof(PostgresFixture))]
// Su orden frente a los demas es indiferente: WarmupFixture construye sus propias dependencias
// (xUnit v3 no inyecta un AssemblyFixture en otro).
[assembly: AssemblyFixture(typeof(WarmupFixture))]
