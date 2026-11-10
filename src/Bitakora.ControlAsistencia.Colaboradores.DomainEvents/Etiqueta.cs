using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Json.Serialization.Metadata;

namespace Bitakora.ControlAsistencia.Colaboradores.DomainEvents;

/// <summary>
/// Etiqueta dinamica (par categoria:valor libre, definido por el cliente) para segmentar
/// colaboradores en reportes.
/// </summary>
/// <remarks>
/// Doble forma por campo: la original (display, solo con trim en los extremos) y la normalizada
/// (minusculas, sin tildes: identidad y busqueda).
/// Igualdad sobre las formas normalizadas de categoria Y valor: dos etiquetas de la misma categoria
/// con valor distinto NO son iguales; para "misma categoria" esta <see cref="EsMismaCategoria"/>.
/// </remarks>
public sealed partial class Etiqueta : IEquatable<Etiqueta>
{
    private readonly string _categoria;
    private readonly string _categoriaNormalizada;
    private readonly string _valor;
    private readonly string _valorNormalizado;

    private Etiqueta(string categoria, string categoriaNormalizada, string valor, string valorNormalizado)
    {
        _categoria = categoria;
        _categoriaNormalizada = categoriaNormalizada;
        _valor = valor;
        _valorNormalizado = valorNormalizado;
    }

    // Para STJ/Marten
    private Etiqueta()
    {
        _categoria = string.Empty;
        _categoriaNormalizada = string.Empty;
        _valor = string.Empty;
        _valorNormalizado = string.Empty;
    }

    public string Categoria => _categoria;
    public string Valor => _valor;

    public string CategoriaNormalizada => _categoriaNormalizada;
    public string ValorNormalizado => _valorNormalizado;

    public static Etiqueta Crear(string categoria, string valor)
    {
        if (string.IsNullOrWhiteSpace(categoria))
            throw new ArgumentException(Mensajes.CategoriaVacia, nameof(categoria));
        if (string.IsNullOrWhiteSpace(valor))
            throw new ArgumentException(Mensajes.ValorVacio, nameof(valor));

        var categoriaOriginal = categoria.Trim();
        var valorOriginal = valor.Trim();

        return new Etiqueta(categoriaOriginal, Normalizar(categoriaOriginal),
            valorOriginal, Normalizar(valorOriginal));
    }

    public bool EsMismaCategoria(Etiqueta otra) =>
        _categoriaNormalizada == otra._categoriaNormalizada;

    // Para quien solo tiene la categoria (RetirarEtiqueta no trae valor y Crear lo exige): mismas
    // reglas de rechazo y misma normalizacion que Crear, que deben seguir alineadas.
    public static string NormalizarCategoria(string categoria)
    {
        if (string.IsNullOrWhiteSpace(categoria))
            throw new ArgumentException(Mensajes.CategoriaVacia, nameof(categoria));

        return Normalizar(categoria.Trim());
    }

    // Con las formas originales, no las normalizadas.
    public override string ToString() => $"{_categoria}:{_valor}";

    public bool Equals(Etiqueta? other) =>
        other is not null
        && _categoriaNormalizada == other._categoriaNormalizada
        && _valorNormalizado == other._valorNormalizado;

    public override bool Equals(object? obj) => Equals(obj as Etiqueta);

    public override int GetHashCode() =>
        HashCode.Combine(_categoriaNormalizada, _valorNormalizado);

    // Deliberado: la enie tambien se descompone, asi que "Diseño" y "Diseno" son la MISMA etiqueta
    // (evita fragmentar el reporte con etiquetas tecleadas a mano). La forma original la conserva.
    private static string Normalizar(string texto) =>
        new string(texto.Normalize(NormalizationForm.FormD)
                .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                .ToArray())
            .Normalize(NormalizationForm.FormC)
            .ToLowerInvariant();

    // Persiste las 4 formas: el evento es autocontenido y no recalcula la normalizacion al
    // rehidratar.
    public static void ConfigurarSerializacion(DefaultJsonTypeInfoResolver resolver)
    {
        var tipoClase = typeof(Etiqueta);
        var ctor = tipoClase.GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, Type.EmptyTypes)!;
        var categoriaField = Campo("_categoria");
        var categoriaNormalizadaField = Campo("_categoriaNormalizada");
        var valorField = Campo("_valor");
        var valorNormalizadoField = Campo("_valorNormalizado");

        resolver.Modifiers.Add(typeInfo =>
        {
            if (typeInfo.Type != tipoClase) return;
            if (typeInfo.Kind != JsonTypeInfoKind.Object) return;

            typeInfo.CreateObject = () => ctor.Invoke(null);

            RegistrarCampo(typeInfo, categoriaField, "categoria");
            RegistrarCampo(typeInfo, categoriaNormalizadaField, "categoriaNormalizada");
            RegistrarCampo(typeInfo, valorField, "valor");
            RegistrarCampo(typeInfo, valorNormalizadoField, "valorNormalizado");
        });
    }

    private static FieldInfo Campo(string nombre) =>
        typeof(Etiqueta).GetField(nombre, BindingFlags.NonPublic | BindingFlags.Instance)!;

    private static void RegistrarCampo(JsonTypeInfo typeInfo, FieldInfo campo, string nombreJson)
    {
        var prop = typeInfo.CreateJsonPropertyInfo(typeof(string), nombreJson);
        prop.Get = obj => campo.GetValue(obj);
        prop.Set = (obj, val) => campo.SetValue(obj, val);
        typeInfo.Properties.Add(prop);
    }
}
