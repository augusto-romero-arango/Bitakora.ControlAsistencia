namespace Bitakora.ControlAsistencia.Colaboradores.Entities;

// Issue #354: resultado de ColaboradorAggregateRoot.AnularTerminacion. Gemelo de
// ResultadoTerminacionVinculacion/ResultadoReingresoColaborador/ResultadoCorreccionFechaInicioVinculacion
// (#349/#350/#352) -- mismo mecanismo "declinar con resultado" (CA-ADR-0030): el aggregate nunca
// lanza y nunca emite un evento de fallo persistido -- responde el resultado de la operacion
// (exito o razon del rechazo) y el handler traduce la razon a InvalidOperationException con
// mensaje .resx en el borde (MEF-ADR-0004 capa 2).
// Unica razon de rechazo (unica regla del comando, issue #354 -- el mas simple de la cadena): la
// ULTIMA vinculacion no tiene terminacion registrada. Cubre tres casos que el handler no necesita
// distinguir entre si (recien registrada, reingresada, o ya anulada antes -- CA-3/CA-4): tras un
// reingreso la terminacion de la vinculacion ANTERIOR queda congelada (decision aprobada
// explicitamente), porque solo la ULTIMA vinculacion cuenta para esta regla.
// internal: mismo criterio de visibilidad que los resultados hermanos -- vive en el mismo
// ensamblado que el handler que lo consume (Entities/ y CommandHandler/ en el mismo proyecto
// Function App), publicos son solo Apply(...) y ComputarStreamId.
internal enum ResultadoAnulacionTerminacion
{
    Exitosa,
    VinculacionAbierta
}
