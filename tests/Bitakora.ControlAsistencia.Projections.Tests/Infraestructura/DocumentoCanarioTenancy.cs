namespace Bitakora.ControlAsistencia.Projections.Tests.Infraestructura;

/// <summary>
/// Issue #268 CA-1/CA-3: documento canario para <see cref="AssertsProyecciones.AssertDocumentosMultiTenant{TCanario}"/>.
/// Policies.AllDocumentsAreMultiTenanted() es una politica que se observa por su efecto en el
/// mapping que Marten resuelve para cualquier tipo (FindOrResolveDocumentType) -- el worker
/// todavia no registra ningun read model real, asi que un tipo sin relacion con ningun dominio
/// alcanza para verificarla sin necesitar Postgres.
/// </summary>
public sealed record DocumentoCanarioTenancy(Guid Id);
