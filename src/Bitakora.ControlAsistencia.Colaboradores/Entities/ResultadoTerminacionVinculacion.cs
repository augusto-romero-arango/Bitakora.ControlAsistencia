namespace Bitakora.ControlAsistencia.Colaboradores.Entities;

// Issue #349: resultado de ColaboradorAggregateRoot.TerminarVinculacion. Mecanismo "declinar con
// resultado" (CA-ADR-0030): el aggregate nunca lanza y nunca emite un evento de fallo persistido
// -- responde el resultado de la operacion (exito o razon del rechazo) y el handler traduce la
// razon a InvalidOperationException con mensaje .resx en el borde (MEF-ADR-0004 capa 2).
// Compatible con Tell-don't-Ask (MEF-ADR-0012): el handler consulta el RESULTADO, nunca interroga
// el estado interno del aggregate para decidir por si mismo.
// Issue #379 (MEF-ADR-0043 paso 4): CodigoNoCorresponde -- el {codigo} de la ruta no coincide con
// el codigo de la vinculacion vigente (_codigoVinculacionVigente). Se evalua PRIMERO, antes de
// YaTerminada/FechaAnteriorAInicio: un comando dirigido a la vinculacion equivocada no debe
// filtrar informacion sobre el estado de la vigente. 409, no 404 (es conflicto con el estado
// vigente, no un recurso inexistente).
// Vive en el mismo ensamblado que el handler que lo consume (Entities/ y CommandHandler/ estan en
// el mismo proyecto Function App), asi que es internal: nadie fuera del ensamblado decide sobre
// este resultado. Misma visibilidad que los metodos de comando de los otros aggregates del repo
// (ControlDiarioAggregateRoot.AdicionarMarcacion, CatalogoTurnos.ObtenerDetalle) -- publicos son
// solo Apply(...) (los necesita el TestStore via GetMethods()) y ComputarStreamId.
internal enum ResultadoTerminacionVinculacion
{
    Exitosa,
    CodigoNoCorresponde,
    YaTerminada,
    FechaAnteriorAInicio
}
