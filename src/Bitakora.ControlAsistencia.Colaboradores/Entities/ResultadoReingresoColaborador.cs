namespace Bitakora.ControlAsistencia.Colaboradores.Entities;

// Issue #350: resultado de ColaboradorAggregateRoot.Reingresar. Gemelo de
// ResultadoTerminacionVinculacion (#349) -- mismo mecanismo "declinar con resultado"
// (CA-ADR-0030): el aggregate nunca lanza y nunca emite un evento de fallo persistido -- responde
// el resultado de la operacion (exito o razon del rechazo) y el handler traduce la razon a
// InvalidOperationException con mensaje .resx en el borde (MEF-ADR-0004 capa 2). Las dos razones de
// rechazo componen la invariante de no-solape y las evalua ColaboradorAggregateRoot.Reingresar solo
// con la historia del stream, sin reloj (ver el detalle de cada una alli).
// internal: mismo criterio de visibilidad que ResultadoTerminacionVinculacion -- vive en el mismo
// ensamblado que el handler que lo consume (Entities/ y CommandHandler/ en el mismo proyecto
// Function App), publicos son solo Apply(...) y ComputarStreamId.
internal enum ResultadoReingresoColaborador
{
    Exitosa,
    VinculacionAbierta,
    FechaSolapaVinculacionAnterior
}
