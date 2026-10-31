using Xunit.Sdk;
using Xunit.v3;

// Los benchmarks miden el sistema desplegado: dos corriendo a la vez se contaminan las metricas
// entre si, asi que el ensamblado corre en serie.
[assembly: Parallelization(Mode = ParallelMode.None)]
