using System.Text.Json.Serialization;
using Bastion.Organizacion.Domain.Impuestos;

namespace Bastion.Organizacion.Infrastructure.Semillas;

/// <summary>Una fila de <c>impuestos.json</c>.</summary>
/// <remarks>
/// <para>
/// <b>Todo es <c>required</c>, incluso lo anulable</b>, y esa es la decisión. Un
/// <c>vigenteHasta</c> que se puede omitir hace indistinguibles «este tramo sigue abierto» de «se
/// me olvidó escribirlo» y de «lo escribí con una errata en la clave». Escrito siempre —con
/// <c>null</c> cuando toca—, las tres se separan: la errata sale como miembro no mapeado y la
/// omisión como <c>required</c> sin rellenar, las dos con el nombre delante.
/// </para>
/// <para>
/// <see cref="JsonUnmappedMemberHandlingAttribute"/> en <c>Disallow</c> es la otra mitad: por
/// omisión, una clave que no case con ninguna propiedad se ignora en silencio. Un
/// <c>"porcentage": 21</c> se cargaría como un impuesto sin porcentaje... si el porcentaje no
/// fuera obligatorio. Con las dos cosas puestas, ninguna errata pasa.
/// </para>
/// <para>
/// Es un tipo de la infraestructura y no un DTO del contrato: describe la forma de un fichero del
/// repositorio, no la de una petición. Si algún día la API acepta cargar semillas, ese será otro
/// tipo, en <c>Contracts</c>, y con su propia validación de entrada.
/// </para>
/// </remarks>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record FilaDeImpuesto
{
    /// <summary>Código del tramo. Se repite entre tramos del mismo impuesto.</summary>
    public required string Codigo { get; init; }

    /// <summary>Nombre con el que se muestra.</summary>
    public required string Nombre { get; init; }

    /// <summary>Naturaleza del impuesto, por su nombre: <c>Iva</c>, <c>Igic</c>, <c>Retencion</c>.</summary>
    public required TipoDeImpuesto Tipo { get; init; }

    /// <summary>Porcentaje en tanto por ciento: el 21 % es <c>21</c>.</summary>
    public required decimal Porcentaje { get; init; }

    /// <summary>Primer día en que se aplica.</summary>
    public required DateOnly VigenteDesde { get; init; }

    /// <summary>Último día en que se aplica, o <c>null</c> mientras siga vigente.</summary>
    public required DateOnly? VigenteHasta { get; init; }

    /// <summary>Cuenta del PGC en la que se repercute, o <c>null</c>.</summary>
    public required string? CuentaRepercutido { get; init; }

    /// <summary>Cuenta del PGC en la que se soporta, o <c>null</c>.</summary>
    public required string? CuentaSoportado { get; init; }
}

/// <summary>Una fila de <c>unidades-de-medida.json</c>.</summary>
/// <remarks>Mismas dos garantías que <see cref="FilaDeImpuesto"/>, y por lo mismo.</remarks>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record FilaDeUnidad
{
    /// <summary>Código en mayúsculas: <c>UD</c>, <c>KG</c>, <c>M</c>.</summary>
    public required string Codigo { get; init; }

    /// <summary>Nombre con el que se muestra.</summary>
    public required string Nombre { get; init; }

    /// <summary>Decimales que admite una cantidad medida en esta unidad.</summary>
    public required int Decimales { get; init; }
}
