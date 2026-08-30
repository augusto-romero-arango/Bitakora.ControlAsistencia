namespace Bitakora.ControlAsistencia.Colaboradores.AsignarSedeFunction;

// Issue #465: comando para asignar (o reasignar) la sede del colaborador -- reemplazo completo de
// un VO atomico direccionable (MEF-ADR-0043 paso 2, precedente exacto AsignarEtiqueta #376).
// Trigger HTTP PUT, Route: colaboradores/{id}/sede. TipoIdentificacion/NumeroIdentificacion no
// llegan en el body -- el endpoint los deriva de {id} via Identificacion.Parsear (MEF-ADR-0037
// seccion 2); CodigoSede viaja en el body (AsignarSedeBody), sin segmento de ruta adicional.
// Payload primitivo (MEF-ADR-0039 decision 6, payload por rol): NUNCA reusa un tipo de
// Colaboradores.DomainEvents ni de Sedes.DomainEvents (islas, CA-ADR-0029) como campo.
public record AsignarSede(
    string TipoIdentificacion,
    string NumeroIdentificacion,
    string CodigoSede);
