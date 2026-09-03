using Bitakora.ControlAsistencia.Colaboradores.DomainEvents;
using Bitakora.ControlAsistencia.ReadModels.Colaboradores;
using JasperFx.Events; // IEvent<T> vive aqui, NO en Marten.Events (MEF-ADR-0034 seccion 6)
using Marten.Events.Aggregation; // SingleStreamProjection<,> vive aqui, NO en Marten.Events.Projections

namespace Bitakora.ControlAsistencia.Projections.Colaboradores;

/// <summary>
/// Clase de proyeccion companion de DirectorioColaborador (receta N1 -- un solo stream, el mismo
/// del colaborador que proyecta FichaColaboradorProjection; MEF-ADR-0035).
/// </summary>
/// <remarks>
/// partial es obligatorio: el source generator descubre Create/Apply por convencion y emite el
/// dispatcher [GeneratedEvolver]. Sin partial el build queda limpio y falla en RUNTIME al registrar
/// la proyeccion (skills/projections/modelos-marten.md).
///
/// Create toma IEvent&lt;ColaboradorRegistrado&gt; y no el payload a secas porque la identidad del
/// documento es exactamente el StreamKey del stream, sin recomponerla a mano desde el payload.
///
/// Solo declara Apply para los 7 eventos del directorio: sin Apply de EtiquetaAsignada/
/// EtiquetaRetirada (el directorio no lleva etiquetas) y sin ShouldDelete (nunca borra).
/// </remarks>
public sealed partial class DirectorioColaboradorProjection
    : SingleStreamProjection<DirectorioColaborador, string>
{
    // El resto de los campos queda en su forma vacia hasta que Apply(VinculacionIniciada) los
    // complete, en el mismo commit que ColaboradorRegistrado.
    public static DirectorioColaborador Create(IEvent<ColaboradorRegistrado> e) =>
        new(
            e.StreamKey!,
            e.Data.Identificacion.Tipo.ToString(),
            DirectorioColaborador.NormalizarNumeroDocumento(e.Data.Identificacion.Numero),
            e.Data.Nombre.NombreCompleto,
            DirectorioColaborador.TokenizarNombre(e.Data.Nombre.NombreCompleto),
            string.Empty,
            default,
            DirectorioColaborador.CentinelaVigenciaAbierta);

    // Reemplazo incondicional, tambien de la sede: e.CodigoSede se asienta tal cual, asi que un
    // reingreso sin sede LIMPIA la anterior en vez de heredarla ("reingreso nace limpio", espejo de
    // ColaboradorAggregateRoot.Apply(VinculacionIniciada)).
    public static DirectorioColaborador Apply(VinculacionIniciada e, DirectorioColaborador vista) =>
        vista with
        {
            CodigoColaborador = e.Codigo,
            VigenteDesde = e.FechaInicio,
            VigenteHasta = DirectorioColaborador.CentinelaVigenciaAbierta,
            CodigoSede = e.CodigoSede
        };

    public static DirectorioColaborador Apply(VinculacionTerminada e, DirectorioColaborador vista) =>
        vista with { VigenteHasta = e.FechaEfectiva };

    public static DirectorioColaborador Apply(TerminacionAnulada e, DirectorioColaborador vista) =>
        vista with { VigenteHasta = DirectorioColaborador.CentinelaVigenciaAbierta };

    // Corregir el nombre obliga a recalcular los tokens, o la entrada deja de encontrarse por su
    // nombre nuevo. La regla la aporta la vista (Tell-don't-Ask, MEF-ADR-0012), nunca un algoritmo
    // propio de la proyeccion.
    public static DirectorioColaborador Apply(NombresCorregidos e, DirectorioColaborador vista) =>
        vista with
        {
            NombreCompleto = e.Nombre.NombreCompleto,
            TokensNombre = DirectorioColaborador.TokenizarNombre(e.Nombre.NombreCompleto)
        };

    public static DirectorioColaborador Apply(FechaInicioVinculacionCorregida e, DirectorioColaborador vista) =>
        vista with { VigenteDesde = e.FechaInicio };

    // SedeAsignada es siempre reemplazo completo: primera asignacion y reasignacion emiten el mismo
    // evento y no existe evento de retiro (DomainEvents/SedeAsignada.cs).
    public static DirectorioColaborador Apply(SedeAsignada e, DirectorioColaborador vista) =>
        vista with { CodigoSede = e.CodigoSede };
}
