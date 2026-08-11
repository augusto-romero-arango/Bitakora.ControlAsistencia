namespace Bitakora.ControlAsistencia.Colaboradores.Entities;

// Issue #350: resultado de ColaboradorAggregateRoot.Reingresar. Gemelo de
// ResultadoTerminacionVinculacion (#349) -- mismo mecanismo "declinar con resultado"
// (CA-ADR-0030): el aggregate nunca lanza y nunca emite un evento de fallo persistido -- responde
// el resultado de la operacion (exito o razon del rechazo) y el handler traduce la razon a
// InvalidOperationException con mensaje .resx en el borde (MEF-ADR-0004 capa 2).
// Dos razones de rechazo, invariante de no-solape (doctrina del preaviso, #349 "vigente y operable
// hasta su fecha"), evaluables solo con la historia del stream, sin reloj:
//   - VinculacionAbierta: la ultima vinculacion no tiene terminacion registrada (incluye el caso
//     de un reingreso previo que aun no se termino).
//   - FechaSolapaVinculacionAnterior: FechaInicio <= FechaEfectiva de la ultima terminacion -- el
//     mismo dia se rechaza (el dia de la fecha efectiva pertenece a la vinculacion que termina;
//     estrictamente posterior es la unica fecha valida).
// internal: mismo criterio de visibilidad que ResultadoTerminacionVinculacion -- vive en el mismo
// ensamblado que el handler que lo consume (Entities/ y CommandHandler/ en el mismo proyecto
// Function App), publicos son solo Apply(...) y ComputarStreamId.
internal enum ResultadoReingresoColaborador
{
    Exitosa,
    VinculacionAbierta,
    FechaSolapaVinculacionAnterior
}
