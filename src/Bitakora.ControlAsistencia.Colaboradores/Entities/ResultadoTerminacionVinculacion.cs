namespace Bitakora.ControlAsistencia.Colaboradores.Entities;

// CodigoNoCorresponde es 409, no 404: conflicto con la vinculacion vigente, no un recurso
// inexistente.
internal enum ResultadoTerminacionVinculacion
{
    Exitosa,
    CodigoNoCorresponde,
    YaTerminada,
    FechaAnteriorAInicio
}
