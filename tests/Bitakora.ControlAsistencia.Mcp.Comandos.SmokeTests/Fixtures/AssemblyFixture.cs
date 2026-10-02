using Xunit.Sdk;
using Xunit.v3;
using Bitakora.ControlAsistencia.Mcp.Comandos.SmokeTests.Fixtures;

[assembly: Parallelization(Mode = ParallelMode.None)]
[assembly: AssemblyFixture(typeof(McpFixture))]
[assembly: AssemblyFixture(typeof(ProgramacionApiFixture))]
