namespace Bitakora.ControlAsistencia.ReadModels;

/// <summary>
/// Marca un documento que materializa una proyeccion del worker, donde Marten le fija mt_version
/// bigint: el Function App que lo consulte debe declarar Schema.For&lt;T&gt;().UseNumericRevisions(true)
/// o cada request intenta un alter column que Postgres rechaza (42804). DocumentosMarcadosPorProyeccionTests
/// (worker) y ComposicionServiciosTests (Function App) verifican ambos lados.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class MaterializadoPorProyeccionAttribute : Attribute;
