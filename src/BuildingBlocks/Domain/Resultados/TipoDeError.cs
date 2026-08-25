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

    /// <summary>Quien pide la operación está identificado pero no tiene permiso para hacerla.</summary>
    PermisoDenegado,

    /// <summary>Lo que la operación necesita no existe, o no es visible para quien la pide.</summary>
    NoEncontrado,

    /// <summary>El recurso está en un estado que no admite la operación, o hay concurrencia.</summary>
    Conflicto,

    /// <summary>Los datos son válidos y el estado es coherente, pero una regla lo impide.</summary>
    ReglaDeNegocio,
}
