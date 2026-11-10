using Bitakora.ControlAsistencia.Colaboradores.DomainEvents;
using Bitakora.ControlAsistencia.ReadModels.Colaboradores;
using JasperFx.Events; // IEvent<T> vive aqui, NO en Marten.Events (MEF-ADR-0034 seccion 6)
using Marten.Events.Aggregation; // SingleStreamProjection<,> vive aqui, NO en Marten.Events.Projections

namespace Bitakora.ControlAsistencia.Projections.Colaboradores;

/// <summary>
/// Proyeccion N1 sobre el stream del colaborador (MEF-ADR-0035).
///
/// partial es obligatorio: el source generator descubre Create/Apply por convencion y emite el
/// dispatcher [GeneratedEvolver]. Sin partial el build queda limpio y falla en RUNTIME al registrar la
/// proyeccion (InvalidProjectionException).
///
/// Registrarla hace que Marten aplique ProjectionDocumentPolicy sobre FichaColaborador (mt_version
/// bigint): el Function App que la consulta debe declarar Schema.For&lt;FichaColaborador&gt;()
/// .UseNumericRevisions(true), y el par de config-tests de ambos lados congela esos literales.
///
/// Create toma IEvent&lt;ColaboradorRegistrado&gt;: la identidad ("{Tipo}:{Numero}") es exactamente el
/// StreamKey, sin recomputarla desde el payload. Sin ShouldDelete: la ficha nunca se borra.
/// </summary>
public sealed partial class FichaColaboradorProjection : SingleStreamProjection<FichaColaborador, string>
{
    // El centinela de vigencia abierta vive con la vista (FichaColaborador.CentinelaVigenciaAbierta):
    // ObtenerFichaColaborador, en otro proceso, debe leer el mismo valor para traducirlo a vacio.

    // El resto de los campos queda vacio hasta que Apply(VinculacionIniciada) los complete, en el mismo
    // commit.
    public static FichaColaborador Create(IEvent<ColaboradorRegistrado> e) =>
        new(
            e.StreamKey!,
            e.Data.Nombre.NombreCompleto,
            string.Empty,
            default,
            FichaColaborador.CentinelaVigenciaAbierta,
            [],
            new Dictionary<string, string>());

    // Reemplazo incondicional ("reingreso nace limpio", espejo de ColaboradorAggregateRoot.Apply
    // (VinculacionIniciada)): vacia ambas estructuras de etiquetas y asienta CodigoSede tal cual, asi que
    // null deja la ficha sin sede. En el registro inicial el vaciado es inocuo.
    public static FichaColaborador Apply(VinculacionIniciada e, FichaColaborador vista) =>
        vista with
        {
            CodigoColaborador = e.Codigo,
            VigenteDesde = e.FechaInicio,
            VigenteHasta = FichaColaborador.CentinelaVigenciaAbierta,
            Etiquetas = [],
            EtiquetasNormalizadas = new Dictionary<string, string>(),
            CodigoSede = e.CodigoSede,
            JornadaId = null
        };

    public static FichaColaborador Apply(VinculacionTerminada e, FichaColaborador vista) =>
        vista with { VigenteHasta = e.FechaEfectiva };

    public static FichaColaborador Apply(TerminacionAnulada e, FichaColaborador vista) =>
        vista with { VigenteHasta = FichaColaborador.CentinelaVigenciaAbierta };

    public static FichaColaborador Apply(NombresCorregidos e, FichaColaborador vista) =>
        vista with { NombreCompleto = e.Nombre.NombreCompleto };

    public static FichaColaborador Apply(FechaInicioVinculacionCorregida e, FichaColaborador vista) =>
        vista with { VigenteDesde = e.FechaInicio };

    // Upsert por categoria normalizada en AMBAS estructuras: el evento representa el estado final de esa
    // categoria. Las existentes se normalizan con Etiqueta.NormalizarCategoria, nunca con una
    // reimplementacion propia.
    public static FichaColaborador Apply(EtiquetaAsignada e, FichaColaborador vista)
    {
        var etiquetas = vista.Etiquetas
            .Where(existente => Etiqueta.NormalizarCategoria(existente.Categoria) != e.Etiqueta.CategoriaNormalizada)
            .Append(new EtiquetaFicha(e.Etiqueta.Categoria, e.Etiqueta.Valor))
            .ToList();

        var normalizadas = new Dictionary<string, string>(vista.EtiquetasNormalizadas)
        {
            [e.Etiqueta.CategoriaNormalizada] = e.Etiqueta.ValorNormalizado
        };

        return vista with { Etiquetas = etiquetas, EtiquetasNormalizadas = normalizadas };
    }

    public static FichaColaborador Apply(EtiquetaRetirada e, FichaColaborador vista)
    {
        var etiquetas = vista.Etiquetas
            .Where(existente => Etiqueta.NormalizarCategoria(existente.Categoria) != e.CategoriaNormalizada)
            .ToList();

        var normalizadas = new Dictionary<string, string>(vista.EtiquetasNormalizadas);
        normalizadas.Remove(e.CategoriaNormalizada);

        return vista with { Etiquetas = etiquetas, EtiquetasNormalizadas = normalizadas };
    }

    // SedeAsignada es siempre reemplazo completo: primera asignacion y reasignacion emiten el mismo
    // evento y no existe evento de retiro.
    public static FichaColaborador Apply(SedeAsignada e, FichaColaborador vista) =>
        vista with { CodigoSede = e.CodigoSede };

    public static FichaColaborador Apply(JornadaAsignada e, FichaColaborador vista) =>
        vista with { JornadaId = e.JornadaId };
}
