namespace Bitakora.ControlAsistencia.ControlHoras.ValueObjects;

// Issue #114: Intervalo temporal con su concepto legal asignado.
// Record con constructor primario - no tiene invariantes propios.
// Issue #184: partial para alojar la clase Mensajes (etiquetas .resx de Concepto) en archivo separado.
// El ToString() humano arma la linea de la memoria de calculo: el intervalo via su propio ToString()
// (primitiva rica) seguido de la etiqueta traducida del concepto. La etiqueta sale del .resx (i18n del
// back); el codigo del concepto (Concepto.ToString()) nunca aparece aqui - ese es la clave estable de
// MinutosPorConcepto, no texto humano.
public sealed partial record IntervaloClasificado(IntervaloTemporal Intervalo, Concepto Concepto)
{
    // Delega al IntervaloTemporal contenido.
    public int DuracionEnMinutos => Intervalo.DuracionEnMinutos;

    // "18:15-21:00 (165min): Ordinaria diurna" - intervalo rico + etiqueta humana traducida.
    public override string ToString() =>
        $"{Intervalo}: {Mensajes.Etiqueta(Concepto)}";
}
