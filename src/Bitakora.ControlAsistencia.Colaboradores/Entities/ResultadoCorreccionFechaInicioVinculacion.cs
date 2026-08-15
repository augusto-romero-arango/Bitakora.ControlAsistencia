namespace Bitakora.ControlAsistencia.Colaboradores.Entities;

// Issue #352: resultado de ColaboradorAggregateRoot.CorregirFechaInicio. Combina los dos
// mecanismos que el ciclo de vida ya usa (CA-ADR-0030):
//   - "declinar con resultado" (ResultadoTerminacionVinculacion #349 / ResultadoInicioVinculacion
//     #378): las dos razones de rechazo (coherencia interna, no-solape hacia atras) que el handler
//     traduce a InvalidOperationException/409 con mensaje .resx.
//   - "declinar en silencio" (ColaboradorAggregateRoot.CorregirNombres #351): SinCambios es la
//     variante de EXITO silenciosa -- la fecha corregida es igual a la fecha de inicio actual, no
//     hay nada que corregir, ningun evento nuevo y ninguna excepcion.
// Issue #379 (MEF-ADR-0043 paso 4): CodigoNoCorresponde -- el {codigo} de la ruta no coincide con
// el codigo de la vinculacion vigente (_codigoVinculacionVigente). Se evalua PRIMERO, antes que
// SinCambios y las demas reglas de estado: un comando dirigido a la vinculacion equivocada no debe
// filtrar informacion sobre el estado de la vigente (ni siquiera "no habia nada que corregir").
// 409, no 404.
// internal: mismo criterio de visibilidad que ResultadoTerminacionVinculacion/
// ResultadoInicioVinculacion -- vive en el mismo ensamblado que el handler que lo consume
// (Entities/ y CommandHandler/ en el mismo proyecto Function App).
internal enum ResultadoCorreccionFechaInicioVinculacion
{
    Exitosa,
    CodigoNoCorresponde,
    SinCambios,
    FechaPosteriorATerminacionPropia,
    FechaSolapaVinculacionAnterior
}
