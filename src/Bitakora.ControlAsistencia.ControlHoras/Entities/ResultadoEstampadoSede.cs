namespace Bitakora.ControlAsistencia.ControlHoras.Entities;

// - MarcacionNoEncontrada: la marcacion aun no fue adicionada al dia; el handler la traduce a
//   InvalidOperationException para que el retry del Service Bus la resuelva.
// - SedeYaEstampada: exito silencioso, el estampado es identico al vigente; sin evento nuevo ni
//   re-publicacion de DiaDepurado.
internal enum ResultadoEstampadoSede
{
    Estampada,
    SedeYaEstampada,
    MarcacionNoEncontrada
}
