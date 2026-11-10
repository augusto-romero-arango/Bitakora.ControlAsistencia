namespace Bitakora.ControlAsistencia.Colaboradores.Entities;

// SinCambios es EXITO sin evento (la fecha ya es la de inicio actual), no un rechazo.
internal enum ResultadoCorreccionFechaInicioVinculacion
{
    Exitosa,
    CodigoNoCorresponde,
    SinCambios,
    FechaPosteriorATerminacionPropia,
    FechaSolapaVinculacionAnterior
}
