namespace Bitakora.ControlAsistencia.Programacion.DomainEvents;

public static class DescripcionJornada
{
    public static string Componer(LimitesJornada limites, ModalidadJornada? modalidad, bool descansoAcumulado = false) =>
        string.Concat(
            limites.ToString(),
            modalidad is null ? string.Empty : $", {ModalidadJornada.Mensajes.LabelModalidad} {modalidad}",
            descansoAcumulado ? $", {ModalidadJornada.Mensajes.LabelDescansoAcumulado}" : string.Empty);
}
