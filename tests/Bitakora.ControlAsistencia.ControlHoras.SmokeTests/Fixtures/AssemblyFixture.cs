using Bitakora.ControlAsistencia.ControlHoras.SmokeTests.Fixtures;

[assembly: CollectionBehavior(DisableTestParallelization = true)]
[assembly: AssemblyFixture(typeof(ApiFixture))]
[assembly: AssemblyFixture(typeof(ServiceBusFixture))]
[assembly: AssemblyFixture(typeof(PostgresFixture))]
// Issue #166: warm-up funcional de la cadena SB. Va al final, pero su orden frente a los demas
// es indiferente: WarmupFixture construye sus propias dependencias (xUnit v3 no inyecta un
// AssemblyFixture en otro). Todos los AssemblyFixture corren su InitializeAsync antes de
// cualquier test, asi que el cebado se ejecuta una vez antes de toda la suite.
[assembly: AssemblyFixture(typeof(WarmupFixture))]
