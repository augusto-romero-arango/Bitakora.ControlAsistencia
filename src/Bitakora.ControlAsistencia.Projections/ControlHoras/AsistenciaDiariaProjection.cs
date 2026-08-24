using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;
using Bitakora.ControlAsistencia.ReadModels.ControlHoras;
using Marten.Events.Aggregation; // SingleStreamProjection<,> vive aqui, NO en Marten.Events.Projections

namespace Bitakora.ControlAsistencia.Projections.ControlHoras;

/// <summary>
/// Clase de proyeccion companion de AsistenciaDiaria (issue #426, receta N1 de MEF-ADR-0035: un solo
/// stream "dc:{CodigoColaborador}:{yyyyMMdd}" por fila -- mismo corte que TurnoVigenteProjection).
/// Vive en el worker, el unico ensamblado que referencia Marten y el analizador
/// JasperFx.Events.SourceGenerator.
///
/// partial es OBLIGATORIO (skills/projections/modelos-marten.md): el source generator descubre
/// Create/Apply por convencion y emite el dispatcher [GeneratedEvolver]. Sin partial el build queda
/// limpio y falla en RUNTIME al registrar la proyeccion (InvalidProjectionException); el config-test
/// ConfigurarControlHoras_RegistraAsistenciaDiariaProjectionComoAsync es lo que lo detecta.
///
/// Sin ShouldDelete: la fila nunca se borra (issue #426, notas tecnicas).
///
/// STUB de fase roja (projection-test-writer): Create/Apply lanzan NotImplementedException a
/// proposito. La derivacion de Plan y de las cuatro banderas del eje 2 es responsabilidad de
/// projection-implementer.
/// </summary>
public sealed partial class AsistenciaDiariaProjection : SingleStreamProjection<AsistenciaDiaria, string>
{
    public static AsistenciaDiaria Create(DepuracionDiaRecibida evento) =>
        throw new NotImplementedException();

    // "El ultimo gana" (CA-6): cada foto reemplaza Plan, NombreTurno, las cuatro banderas y
    // HorasPorConcepto. Id/CodigoColaborador/Fecha invariantes (identidad del stream). Estado no
    // cambia en este issue.
    public static AsistenciaDiaria Apply(DepuracionDiaRecibida evento, AsistenciaDiaria vista) =>
        throw new NotImplementedException();
}
