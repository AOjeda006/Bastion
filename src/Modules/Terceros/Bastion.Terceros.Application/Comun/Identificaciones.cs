using Bastion.BuildingBlocks.Application.Validacion;
using Bastion.BuildingBlocks.Domain.Identificacion;
using Bastion.Terceros.Domain.Terceros;

namespace Bastion.Terceros.Application.Comun;

/// <summary>
/// Lee un identificador fiscal escrito por una persona y lo convierte en el del dominio,
/// anotando por campo lo que no cuadre.
/// </summary>
/// <remarks>
/// <para>
/// En un solo sitio porque lo usan el alta y la búsqueda, y tienen que leerlo <b>igual</b>: si el
/// alta normalizara «B-1234 5678» y la búsqueda no, se daría de alta una ficha que después no se
/// encuentra. Es la clase de fallo que no rompe nada y que nadie sabe explicar.
/// </para>
/// <para>
/// Vive en Application y no en el dominio porque lo que produce no es un identificador: es un
/// identificador <b>o</b> una lista de errores por campo, que es un concepto del borde
/// (ADR-0004). El dominio pone las dos puertas —<c>PaisNormalizado</c> y <c>NumeroNormalizado</c>
/// no lanzan— y aquí solo se decide a qué campo del formulario corresponde cada una.
/// </para>
/// </remarks>
internal static class Identificaciones
{
    /// <summary>
    /// Convierte el par (país, número) en un <see cref="IdentificacionFiscal"/>, o devuelve nulo
    /// dejando en <paramref name="errores"/> el campo que falla.
    /// </summary>
    /// <remarks>
    /// <b>La bifurcación por país es la del criterio del ítem</b>: <c>ES</c> se valida de verdad
    /// —carácter de control del DNI, del NIE y del CIF— y nace verificado; cualquier otro país no
    /// se puede validar y nace marcado como no verificado. Lo que no se puede comprobar se dice
    /// que no se ha comprobado; no se da por bueno.
    /// </remarks>
    /// <param name="pais">País tal como llegó, o nulo.</param>
    /// <param name="numero">Identificador tal como llegó, o nulo.</param>
    /// <param name="prefijo">
    /// Con qué se nombran los campos en el contrato de quien llama: <c>"identificacion."</c> en el
    /// alta, cadena vacía en la búsqueda, donde van sueltos. Los nombres de los errores tienen que
    /// ser los del formulario que los va a pintar.
    /// </param>
    /// <param name="errores">Dónde se apunta lo que no cuadra.</param>
    internal static IdentificacionFiscal? Leer(
        string? pais,
        string? numero,
        string prefijo,
        ErroresPorCampo errores)
    {
        string? codigo = IdentificacionFiscal.PaisNormalizado(pais);

        if (codigo is null)
        {
            errores.Agregar(
                prefijo + "pais",
                "No es un código de país: se escribe con las dos letras de ISO 3166-1 alfa-2, " +
                "como ES, FR o PT.");

            return null;
        }

        if (codigo == IdentificacionFiscal.PaisDeEspana)
        {
            if (Nif.Intentar(numero, out Nif? nif))
            {
                return IdentificacionFiscal.Espanola(nif);
            }

            errores.Agregar(
                prefijo + "numero",
                "No es un NIF, NIE ni CIF español válido: revise el carácter de control.");

            return null;
        }

        string? normalizado = IdentificacionFiscal.NumeroNormalizado(numero);

        if (normalizado is null)
        {
            errores.Agregar(
                prefijo + "numero",
                "El identificador tiene que llevar alguna letra o dígito y no pasar de " +
                $"{IdentificacionFiscal.LongitudMaximaDelNumero} caracteres.");

            return null;
        }

        return IdentificacionFiscal.Extranjera(codigo, normalizado);
    }
}
