using Xunit.Sdk;
using Xunit.v3;
using Bitakora.ControlAsistencia.Mcp.Asistente.SmokeTests.Fixtures;

[assembly: Parallelization(Mode = ParallelMode.None)]
[assembly: AssemblyFixture(typeof(McpFixture))]
[assembly: AssemblyFixture(typeof(ProgramacionApiFixture))]
