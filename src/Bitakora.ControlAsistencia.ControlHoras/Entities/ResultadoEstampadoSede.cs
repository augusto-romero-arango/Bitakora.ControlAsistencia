namespace Bitakora.ControlAsistencia.ControlHoras.Entities;

// Resultado de ControlDiarioAggregateRoot.EstamparSede. Mecanismo "declinar con resultado"
// (CA-ADR-0030) para que el handler no interrogue Marcaciones antes de decidir (Tell-don't-Ask,
// MEF-ADR-0012):
//   - MarcacionNoEncontrada: la marcacion aun no fue adicionada al dia -- el handler la traduce a
//     InvalidOperationException para que el retry del Service Bus la resuelva (CA-3).
//   - SedeYaEstampada: variante de exito silenciosa -- el estampado que llego es identico al que ya
//     tiene la marcacion, no hay evento nuevo ni re-publicacion de DiaDepurado (CA-4).
// internal: mismo criterio de visibilidad que los resultados hermanos -- vive en el mismo ensamblado
// que el handler que lo consume.
internal enum ResultadoEstampadoSede
{
    Estampada,
    SedeYaEstampada,
    MarcacionNoEncontrada
}
