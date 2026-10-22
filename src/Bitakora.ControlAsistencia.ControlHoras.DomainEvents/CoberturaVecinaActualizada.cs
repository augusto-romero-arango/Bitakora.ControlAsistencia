namespace Bitakora.ControlAsistencia.ControlHoras.DomainEvents;

public sealed class CoberturaVecinaActualizada
{
    public string Id { get; private set; } = null!;
    public DiaVecino Vecino { get; private set; }
    public DateTime? Limite { get; private set; }

    private CoberturaVecinaActualizada() { }

    public static CoberturaVecinaActualizada Crear(string id, DiaVecino vecino, DateTime? limite) =>
        throw new NotImplementedException();
}
