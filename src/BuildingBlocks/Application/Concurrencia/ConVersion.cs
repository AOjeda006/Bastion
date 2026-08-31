namespace Bastion.BuildingBlocks.Application.Concurrencia;

/// <summary>
/// Lo que devuelve la lectura de un recurso que se puede editar: el recurso y la versión con la
/// que se ha leído.
/// </summary>
/// <remarks>
/// <para>
/// <b>La versión NO va dentro del DTO</b>, y por eso hace falta este envoltorio. El DTO es el
/// contrato del recurso —lo que el negocio dice de él— y la versión es un dato del protocolo:
/// viaja en la cabecera <c>ETag</c> y vuelve en <c>If-Match</c>. Metida en el cuerpo tendría que
/// estar también en las listas, que se leen sin rastreo y no la traen, y quedaría a cero en unas
/// respuestas y con valor en otras: el mismo campo significando dos cosas.
/// </para>
/// <para>
/// Lo emiten <b>solo las lecturas de UN recurso</b>. La lista no lleva <c>ETag</c> por elemento a
/// propósito: quien va a editar algo lo abre primero, y esa lectura es la que trae la versión.
/// </para>
/// </remarks>
/// <typeparam name="T">El DTO del recurso.</typeparam>
/// <param name="Recurso">El recurso, tal como sale por la API.</param>
/// <param name="Version">Su versión, que se publica como <c>ETag</c>.</param>
public sealed record ConVersion<T>(T Recurso, VersionDeRecurso Version);
