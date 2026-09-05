using Bastion.BuildingBlocks.Domain.Resultados;

namespace Bastion.Terceros.Application.Terceros;

/// <summary>Los desenlaces fallidos que comparten varios casos de uso de tercero.</summary>
internal static class ErroresDeTercero
{
    internal static ErrorDeOperacion NoEncontrado(Guid id) => ErrorDeOperacion.NoEncontrado(
        "tercero-no-encontrado",
        $"No hay ningún tercero con el identificador {id}.");

    /// <summary>
    /// El identificador fiscal ya lo tiene alguien en esta empresa. <b>Uno solo para los dos
    /// casos</b>, y ahí está el criterio del ítem.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Se devuelve igual si el que estorba está activo que si está bloqueado, y no por descuido:
    /// si las dos respuestas se distinguieran, cualquiera con el formulario de alta podría
    /// recorrer identificadores y sacar la lista de quiénes están dados de baja — que es
    /// exactamente lo que el artículo 32 de la LOPDGDD reserva. El formulario de alta se
    /// convertiría en el censo de bajas.
    /// </para>
    /// <para>
    /// <b>Es una propiedad, no una redacción.</b> Lo que se comprueba no es que el mensaje evite
    /// la palabra «bloqueado», sino que las dos respuestas sean indistinguibles: mismo código de
    /// estado, mismo <c>type</c> y mismo cuerpo salvo el identificador de traza. Por eso este
    /// método <b>no recibe parámetros</b>: no hay nada que interpolar, así que no hay manera de
    /// que un caso ponga en el texto algo que el otro no ponga.
    /// </para>
    /// <para>
    /// <b>Y por eso tampoco lleva el identificador que chocó</b>, aunque el de un almacén
    /// duplicado sí lo lleve. Con el número dentro, las dos respuestas dejarían de ser comparables
    /// enteras —el número es distinto en cada escenario, porque un mismo número no puede estar
    /// activo y bloqueado a la vez— y la comparación tendría que normalizar el cuerpo antes de
    /// mirarlo. Un test que decide qué trozos no cuenta es un test que decide qué se puede filtrar.
    /// </para>
    /// </remarks>
    /// <param name="bloqueada">Si la ficha que estorba está bloqueada.</param>
    internal static ErrorDeOperacion IdentificacionDuplicada(bool bloqueada) =>
        ErrorDeOperacion.Conflicto(
            "tercero-duplicado",
            bloqueada
                ? "Ese identificador fiscal pertenece a una ficha dada de baja."
                : "Esta empresa ya tiene un tercero con ese identificador fiscal.");
}
