namespace Bitakora.ControlAsistencia.Colaboradores.Entities;

// Issue #349: resultado de ColaboradorAggregateRoot.TerminarVinculacion. Mecanismo "declinar con
// resultado" (CA-ADR-0030): el aggregate nunca lanza y nunca emite un evento de fallo persistido
// -- responde el resultado de la operacion (exito o razon del rechazo) y el handler traduce la
// razon a InvalidOperationException con mensaje .resx en el borde (MEF-ADR-0004 capa 2).
// Compatible con Tell-don't-Ask (MEF-ADR-0012): el handler consulta el RESULTADO, nunca interroga
// el estado interno del aggregate para decidir por si mismo.
// Vive en el mismo ensamblado que el handler que lo consume (Entities/ y CommandHandler/ estan en
// el mismo proyecto Function App), asi que es internal: nadie fuera del ensamblado decide sobre
// este resultado. Misma visibilidad que los metodos de comando de los otros aggregates del repo
// (ControlDiarioAggregateRoot.AdicionarMarcacion, CatalogoTurnos.ObtenerDetalle) -- publicos son
// solo Apply(...) (los necesita el TestStore via GetMethods()) y ComputarStreamId.
internal enum ResultadoTerminacionVinculacion
{
    Exitosa,
    YaTerminada,
    FechaAnteriorAInicio
}
