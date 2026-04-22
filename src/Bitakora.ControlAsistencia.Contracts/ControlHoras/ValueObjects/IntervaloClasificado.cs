namespace Bitakora.ControlAsistencia.Contracts.ControlHoras.ValueObjects;

// Issue #114: Intervalo temporal con su concepto legal asignado.
// Record con constructor primario - no tiene invariantes propios.
public record IntervaloClasificado(IntervaloTemporal Intervalo, Concepto Concepto)
{
    // Delega al IntervaloTemporal contenido.
    public int DuracionEnMinutos => throw new NotImplementedException();
}
