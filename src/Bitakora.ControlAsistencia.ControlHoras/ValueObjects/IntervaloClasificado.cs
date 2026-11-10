namespace Bitakora.ControlAsistencia.ControlHoras.ValueObjects;

// El codigo del concepto (Concepto.ToString()) nunca aparece en el ToString(): es la clave estable de
// MinutosPorConcepto, no texto humano.
public sealed partial record IntervaloClasificado(IntervaloTemporal Intervalo, Concepto Concepto)
{
    public int DuracionEnMinutos => Intervalo.DuracionEnMinutos;

    // "18:15-21:00 (165min): Ordinaria diurna"
    public override string ToString() =>
        $"{Intervalo}: {Mensajes.Etiqueta(Concepto)}";
}
