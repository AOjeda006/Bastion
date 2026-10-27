using Bastion.BuildingBlocks.Contracts.Direcciones;

namespace Bastion.BuildingBlocks.Application.Exportacion;

/// <summary>Las seis columnas de una dirección (R17), con el prefijo de quien la lleva.</summary>
/// <remarks>
/// <para>
/// <b>Con los nombres y en el orden de la importación de terceros</b>, la que tiene una dirección:
/// <c>domicilio_calle</c>, <c>domicilio_numero</c>… Así una dirección sale igual en todos los
/// ficheros, y la de un tercero vuelve a entrar por su importación.
/// </para>
/// <para>
/// <b>Una dirección que falta sale en seis campos vacíos</b>, como la de un almacén virtual: la
/// columna está siempre, para que cada fila cuadre con la cabecera.
/// </para>
/// </remarks>
public static class ColumnasDeDireccion
{
    /// <summary>Las seis columnas, en su orden.</summary>
    /// <typeparam name="T">Lo que publica el listado de cada elemento.</typeparam>
    /// <param name="prefijo">Cómo se llama la dirección en la cabecera, como <c>domicilio</c>.</param>
    /// <param name="direccion">La dirección de un elemento, o nula si no tiene.</param>
    public static IReadOnlyList<ColumnaCsv<T>> De<T>(string prefijo, Func<T, DireccionDto?> direccion)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prefijo);
        ArgumentNullException.ThrowIfNull(direccion);

        return
        [
            new($"{prefijo}_calle", elemento => CampoCsv.Texto(direccion(elemento)?.Calle)),
            new($"{prefijo}_numero", elemento => CampoCsv.Texto(direccion(elemento)?.Numero)),
            new($"{prefijo}_codigo_postal", elemento => CampoCsv.Texto(direccion(elemento)?.CodigoPostal)),
            new($"{prefijo}_poblacion", elemento => CampoCsv.Texto(direccion(elemento)?.Poblacion)),
            new($"{prefijo}_subdivision", elemento => CampoCsv.Texto(direccion(elemento)?.Subdivision)),
            new($"{prefijo}_pais", elemento => CampoCsv.Texto(direccion(elemento)?.Pais)),
        ];
    }
}
