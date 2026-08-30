namespace Bitakora.ControlAsistencia.Colaboradores.Entities;

// Issue #465: resultado de ColaboradorAggregateRoot.AsignarSede. Combina los dos mecanismos que el
// ciclo de vida ya usa (CA-ADR-0030), precedente exacto ResultadoAsignacionEtiqueta (#355):
//   - "declinar con resultado": VinculacionTerminada es la unica razon de rechazo evaluable con la
//     historia del stream, sin reloj (decision de refinamiento: la sede describe la relacion
//     laboral ACTIVA, incluido un preaviso sin vencer) -- el handler la traduce a
//     InvalidOperationException/409 con mensaje .resx.
//   - "declinar en silencio": SinCambios es la variante de EXITO silenciosa -- el codigo del
//     comando es IGUAL (comparacion exacta, case-sensitive) al ya asignado, ningun evento nuevo.
// internal: mismo criterio de visibilidad que los resultados hermanos -- vive en el mismo
// ensamblado que el handler que lo consume, publicos son solo Apply(...) y ComputarStreamId.
internal enum ResultadoAsignacionSede
{
    Exitosa,
    SinCambios,
    VinculacionTerminada
}
