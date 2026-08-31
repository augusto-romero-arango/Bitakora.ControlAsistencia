namespace Bitakora.ControlAsistencia.Colaboradores.AsignarSedeFunction;

// Forma de transporte del borde HTTP, no el comando interno: TipoIdentificacion/NumeroIdentificacion
// se derivan de la ruta (colaboradores/{id}/sede), nunca del body.
public record AsignarSedeBody(string CodigoSede);
