using Xunit.Sdk;
using Xunit.v3;
using Bitakora.ControlAsistencia.Colaboradores.SmokeTests.Fixtures;

[assembly: Parallelization(Mode = ParallelMode.None)]
[assembly: AssemblyFixture(typeof(ApiFixture))]
[assembly: AssemblyFixture(typeof(ServiceBusFixture))]
[assembly: AssemblyFixture(typeof(PostgresFixture))]
