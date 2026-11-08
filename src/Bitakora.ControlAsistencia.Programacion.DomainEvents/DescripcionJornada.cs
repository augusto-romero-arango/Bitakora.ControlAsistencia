namespace Bitakora.ControlAsistencia.Programacion.DomainEvents;

public static class DescripcionJornada
{
    public static string Componer(LimitesJornada limites, ModalidadJornada? modalidad, bool descansoAcumulado = false) =>
        modalidad is null
            ? limites.ToString()
            : $"{limites}, {ModalidadJornada.Mensajes.LabelModalidad} {modalidad}";
}
