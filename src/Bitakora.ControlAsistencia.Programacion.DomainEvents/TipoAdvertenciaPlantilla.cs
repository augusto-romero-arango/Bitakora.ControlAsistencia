namespace Bitakora.ControlAsistencia.Programacion.DomainEvents;

public enum TipoAdvertenciaPlantilla
{
    PlantillaSinJornada,
    DiaSinTurno,
    SuperaTopeDiario,
    PorDebajoDelMinimoDiario,
    SuperaHorasSemanales,
    PorDebajoDeHorasSemanales,
    FaltanDiasDeDescanso,
    SobranDiasDeDescanso
}
