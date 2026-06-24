namespace Bitakora.ControlAsistencia.Contracts.ControlHoras.ValueObjects;

// Issue #114: Intervalo temporal con su concepto legal asignado.
// Record con constructor primario - no tiene invariantes propios.
// Issue #184: partial para alojar la clase Mensajes (etiquetas .resx de Concepto) en archivo separado.
// El ToString() humano para la trazabilidad ("{intervalo}: {etiqueta traducida}") lo provee el implementer
// (lo cubre IntervaloClasificadoTests en fase roja); aqui no se override para no escribir implementacion.
public sealed partial record IntervaloClasificado(IntervaloTemporal Intervalo, Concepto Concepto)
{
    // Delega al IntervaloTemporal contenido.
    public int DuracionEnMinutos => Intervalo.DuracionEnMinutos;
}
