namespace Bitakora.ControlAsistencia.Colaboradores.Entities;

// Issue #355: resultado de ColaboradorAggregateRoot.AsignarEtiqueta. Combina los dos mecanismos
// que el ciclo de vida ya usa (CA-ADR-0030):
//   - "declinar con resultado" (ResultadoTerminacionVinculacion #349 y hermanos): VinculacionTerminada
//     es la unica razon de rechazo evaluable con la historia del stream, sin reloj (decision de
//     refinamiento 2026-08-11: las etiquetas describen la relacion laboral ACTIVA, incluido un
//     preaviso sin vencer) -- el handler la traduce a InvalidOperationException/409 con mensaje
//     .resx.
//   - "declinar en silencio" (ColaboradorAggregateRoot.CorregirNombres #351 / CorregirFechaInicio
//     #352): SinCambios es la variante de EXITO silenciosa -- la etiqueta nueva es igual por valor
//     (Etiqueta.Equals, #353) a la que ya existe para esa categoria, ningun evento nuevo.
// internal: mismo criterio de visibilidad que los resultados hermanos -- vive en el mismo
// ensamblado que el handler que lo consume (Entities/ y CommandHandler/ en el mismo proyecto
// Function App), publicos son solo Apply(...) y ComputarStreamId.
internal enum ResultadoAsignacionEtiqueta
{
    Exitosa,
    SinCambios,
    VinculacionTerminada
}
