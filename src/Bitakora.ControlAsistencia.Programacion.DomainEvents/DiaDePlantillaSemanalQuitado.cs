using System.Text.Json.Serialization.Metadata;

namespace Bitakora.ControlAsistencia.Programacion.DomainEvents;

// Issue #622: vacia el slot (semana, dia) de la plantilla. Su resultado es una ausencia
// (CA-ADR-0033 decision 4): sin TurnoId -- la clave (Semana, Dia) ya localiza el slot que Apply
// debe vaciar, sin necesitar el turno que se fue. Mismo patron que DiaDePlantillaSemanalAsignado:
// sealed class con ctor privado + ctor vacio para Marten/JSON.
public sealed partial class DiaDePlantillaSemanalQuitado
{
    public Guid PlantillaId { get; private set; }
    public int Semana { get; private set; }
    public DiaSemana Dia { get; private set; }

    private DiaDePlantillaSemanalQuitado(Guid plantillaId, int semana, DiaSemana dia)
    {
        PlantillaId = plantillaId;
        Semana = semana;
        Dia = dia;
    }

    // Constructor vacio privado para Marten/JSON (mismo patron que DiaDePlantillaSemanalAsignado).
    private DiaDePlantillaSemanalQuitado() => Dia = DiaSemana.Lunes;

    public static DiaDePlantillaSemanalQuitado Crear(Guid plantillaId, int semana, DiaSemana dia) =>
        throw new NotImplementedException();

    // Dia persiste como su numero ISO (entero), mismo mecanismo que DiaDePlantillaSemanalAsignado.
    public static void ConfigurarSerializacion(DefaultJsonTypeInfoResolver resolver) =>
        throw new NotImplementedException();
}
