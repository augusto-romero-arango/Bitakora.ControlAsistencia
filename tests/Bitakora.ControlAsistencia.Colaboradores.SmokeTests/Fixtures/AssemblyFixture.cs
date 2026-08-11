using Bitakora.ControlAsistencia.Colaboradores.SmokeTests.Fixtures;

[assembly: CollectionBehavior(DisableTestParallelization = true)]
[assembly: AssemblyFixture(typeof(ApiFixture))]
[assembly: AssemblyFixture(typeof(ServiceBusFixture))]
[assembly: AssemblyFixture(typeof(PostgresFixture))]
