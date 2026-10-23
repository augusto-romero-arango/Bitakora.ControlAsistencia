namespace Bitakora.ControlAsistencia.ControlHoras.Entities;

// Resultado de ControlDiarioAggregateRoot.ValidarReparto (CA-ADR-0030): declina con resultado.
internal enum ResultadoResolucionAmbiguedad
{
    Valido,
    RepartoIncompleto,
    MarcacionNoAmbigua,
    FechaNoValida
}
