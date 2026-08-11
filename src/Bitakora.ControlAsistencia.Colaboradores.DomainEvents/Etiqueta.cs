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
/// Issue #353: doble forma por campo -- la original (display, solo con trim; espacios internos se
/// conservan) y la normalizada (minusculas, sin tildes -- identidad y busqueda). La normalizacion
/// es invariante del VO, no truco de base de datos (MEF-ADR-0012).
/// Igualdad por valor COMPLETA sobre las formas normalizadas (categoria Y valor): dos etiquetas de
/// la misma categoria con valor distinto NO son iguales. <see cref="EsMismaCategoria"/> es la
/// superficie separada para la salvaguarda de unicidad por categoria del aggregate (issue #355,
/// Tell-don't-Ask: el VO decide que significa "misma categoria", no el llamador comparando strings).
/// Este corte no introduce comando, evento ni aggregate -- <see cref="Etiqueta"/> sera payload rico
/// de los eventos persistidos de Colaborador (#355+); por eso ya trae ConfigurarSerializacion.
/// MEF-ADR-0012: sealed class, constructor privado real + constructor vacio para STJ/Marten,
/// factory Crear() con validacion, ConfigurarSerializacion registrando campos privados (patron
/// SubFranja) -- proscrito [JsonConstructor].
/// </remarks>
public sealed partial class Etiqueta : IEquatable<Etiqueta>
{
    private readonly string _categoria;
    private readonly string _categoriaNormalizada;
    private readonly string _valor;
    private readonly string _valorNormalizado;

    // Constructor real: usado por el factory
    private Etiqueta(string categoria, string categoriaNormalizada, string valor, string valorNormalizado)
    {
        _categoria = categoria;
        _categoriaNormalizada = categoriaNormalizada;
        _valor = valor;
        _valorNormalizado = valorNormalizado;
    }

    // Constructor vacio para STJ/Marten
    private Etiqueta()
    {
        _categoria = string.Empty;
        _categoriaNormalizada = string.Empty;
        _valor = string.Empty;
        _valorNormalizado = string.Empty;
    }

    // Formas originales (display, con trim aplicado en los extremos).
    public string Categoria => _categoria;
    public string Valor => _valor;

    // Formas normalizadas: claves del diccionario del aggregate y de la busqueda en reportes
    // (MEF-ADR-0012: identidad observable, no dato intermedio de calculo).
    public string CategoriaNormalizada => _categoriaNormalizada;
    public string ValorNormalizado => _valorNormalizado;

    // CA-1, CA-2, CA-5: valida no vacios/whitespace, trim en extremos (conserva espacios internos),
    // calcula formas normalizadas (Normalize(FormD) + remover NonSpacingMark + ToLowerInvariant).
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

    // CA-4: true si las categorias normalizadas coinciden, sin importar el valor.
    public bool EsMismaCategoria(Etiqueta otra) =>
        _categoriaNormalizada == otra._categoriaNormalizada;

    // Propuesta: "{Categoria}:{Valor}" con las formas originales (display).
    public override string ToString() => $"{_categoria}:{_valor}";

    // Igualdad por valor COMPLETA: categoria normalizada + valor normalizado.
    public bool Equals(Etiqueta? other) =>
        other is not null
        && _categoriaNormalizada == other._categoriaNormalizada
        && _valorNormalizado == other._valorNormalizado;

    public override bool Equals(object? obj) => Equals(obj as Etiqueta);

    public override int GetHashCode() =>
        HashCode.Combine(_categoriaNormalizada, _valorNormalizado);

    // Patron documentado por Microsoft para remover diacriticos: descomponer en FormD, filtrar los
    // marcas de combinacion (NonSpacingMark) y recomponer en FormC, todo en minusculas invariantes.
    // Fuera de alcance (issue #353): errores de transposicion ("aera" vs "area") -- eso es fuzzy
    // matching de UI, no un invariante del dominio.
    private static string Normalizar(string texto) =>
        new string(texto.Normalize(NormalizationForm.FormD)
                .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                .ToArray())
            .Normalize(NormalizationForm.FormC)
            .ToLowerInvariant();

    // Mapping de serializacion -- vive aqui porque cambia con la clase (patron SubFranja /
    // Identificacion). Persiste las 4 formas (doble forma por campo, decision del planner).
    public static void ConfigurarSerializacion(DefaultJsonTypeInfoResolver resolver)
    {
        var tipoClase = typeof(Etiqueta);
        var ctor = tipoClase.GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, Type.EmptyTypes)!;
        var categoriaField = tipoClase.GetField("_categoria", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var categoriaNormalizadaField = tipoClase.GetField(
            "_categoriaNormalizada", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var valorField = tipoClase.GetField("_valor", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var valorNormalizadoField = tipoClase.GetField(
            "_valorNormalizado", BindingFlags.NonPublic | BindingFlags.Instance)!;

        resolver.Modifiers.Add(typeInfo =>
        {
            if (typeInfo.Type != tipoClase) return;
            if (typeInfo.Kind != JsonTypeInfoKind.Object) return;

            typeInfo.CreateObject = () => ctor.Invoke(null);

            var categoriaProp = typeInfo.CreateJsonPropertyInfo(typeof(string), "categoria");
            categoriaProp.Get = obj => ((Etiqueta)obj)._categoria;
            categoriaProp.Set = (obj, val) => categoriaField.SetValue(obj, val);
            typeInfo.Properties.Add(categoriaProp);

            var categoriaNormalizadaProp = typeInfo.CreateJsonPropertyInfo(typeof(string), "categoriaNormalizada");
            categoriaNormalizadaProp.Get = obj => ((Etiqueta)obj)._categoriaNormalizada;
            categoriaNormalizadaProp.Set = (obj, val) => categoriaNormalizadaField.SetValue(obj, val);
            typeInfo.Properties.Add(categoriaNormalizadaProp);

            var valorProp = typeInfo.CreateJsonPropertyInfo(typeof(string), "valor");
            valorProp.Get = obj => ((Etiqueta)obj)._valor;
            valorProp.Set = (obj, val) => valorField.SetValue(obj, val);
            typeInfo.Properties.Add(valorProp);

            var valorNormalizadoProp = typeInfo.CreateJsonPropertyInfo(typeof(string), "valorNormalizado");
            valorNormalizadoProp.Get = obj => ((Etiqueta)obj)._valorNormalizado;
            valorNormalizadoProp.Set = (obj, val) => valorNormalizadoField.SetValue(obj, val);
            typeInfo.Properties.Add(valorNormalizadoProp);
        });
    }
}
