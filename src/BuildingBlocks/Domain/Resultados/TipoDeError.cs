namespace Bastion.BuildingBlocks.Domain.Resultados;

/// <summary>
/// Clase de desenlace de negocio fallido. Es lo que el borde necesita para elegir la respuesta
/// sin saber nada del caso de uso concreto.
/// </summary>
/// <remarks>
/// Son categorías de NEGOCIO, no códigos de estado HTTP: el dominio no sabe que existe HTTP.
/// La traducción a códigos vive en el borde, en la política central de errores de
/// <c>BuildingBlocks.Infrastructure</c>. Ver ADR-0004.
/// </remarks>
public enum TipoDeError
{
    /// <summary>Los datos recibidos no cumplen el contrato de entrada.</summary>
    Validacion,

    /// <summary>
    /// Quien pide la operación no ha demostrado quién es, o ya no puede demostrarlo.
    /// </summary>
    /// <remarks>
    /// Es distinto de <see cref="PermisoDenegado"/> y la diferencia importa para el cliente:
    /// <b>no autenticado</b> significa «identifícate y vuelve a intentarlo», y el frontal responde
    /// mandando al login; <b>permiso denegado</b> significa «ya sé quién eres y esto no es para
    /// ti», y volver a identificarse no cambia nada. Mandar al login a quien solo le falta un
    /// permiso es un bucle: entra, vuelve, y otra vez.
    /// </remarks>
    NoAutenticado,

    /// <summary>Quien pide la operación está identificado pero no tiene permiso para hacerla.</summary>
    PermisoDenegado,

    /// <summary>Lo que la operación necesita no existe, o no es visible para quien la pide.</summary>
    NoEncontrado,

    /// <summary>El recurso está en un estado que no admite la operación.</summary>
    /// <remarks>
    /// <b>No es el conflicto de versión.</b> Aquí el documento ya no admite lo que se le pide
    /// —cerrar un ejercicio que ya está cerrado, facturar un albarán ya facturado— y repetir la
    /// operación con datos frescos tampoco funcionaría. Cuando lo que pasa es que otro escribió
    /// antes, el desenlace es <see cref="VersionObsoleta"/> y el cliente reacciona distinto:
    /// vuelve a leer y decide. Los dos salen con códigos distintos porque son cosas distintas.
    /// </remarks>
    Conflicto,

    /// <summary>
    /// La versión sobre la que el cliente dice estar escribiendo ya no es la actual: alguien
    /// guardó en medio.
    /// </summary>
    /// <remarks>
    /// Es la actualización perdida, y el protocolo ya trae su respuesta: <c>412</c>. La versión
    /// no la comprueba ningún caso de uso a mano —eso sería leer, comparar y guardar, con hueco
    /// entre los tres pasos—, sino el <c>WHERE</c> del propio <c>UPDATE</c>.
    /// </remarks>
    VersionObsoleta,

    /// <summary>
    /// La operación exige decir sobre qué versión se escribe, y la petición no lo dice.
    /// </summary>
    /// <remarks>
    /// <c>428</c> y no <c>400</c>: la petición está bien formada, lo que falta es una
    /// precondición. La diferencia importa porque el cliente puede arreglarlo solo —leer el
    /// recurso, quedarse con su <c>ETag</c> y repetir—, y un <c>400</c> le diría que revise el
    /// cuerpo, que está impecable.
    /// </remarks>
    FaltaLaVersion,

    /// <summary>Los datos son válidos y el estado es coherente, pero una regla lo impide.</summary>
    ReglaDeNegocio,
}
