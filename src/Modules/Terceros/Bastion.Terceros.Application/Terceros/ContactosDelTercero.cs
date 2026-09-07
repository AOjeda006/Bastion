using Bastion.BuildingBlocks.Application.Concurrencia;
using Bastion.BuildingBlocks.Application.Validacion;
using Bastion.BuildingBlocks.Domain.Identificacion;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Terceros.Application.Comun;
using Bastion.Terceros.Contracts.Terceros;
using Bastion.Terceros.Domain.Terceros;

namespace Bastion.Terceros.Application.Terceros;

/// <summary>Los contactos de un tercero.</summary>
public interface IListarContactos
{
    /// <summary>Ejecuta el caso de uso.</summary>
    /// <param name="terceroId">La ficha de la que cuelgan.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<Resultado<IReadOnlyList<ContactoDto>>> EjecutarAsync(
        Guid terceroId,
        CancellationToken cancelacion);
}

/// <summary>Cuelga un contacto de un tercero.</summary>
public interface IAgregarContacto
{
    /// <summary>Ejecuta el caso de uso.</summary>
    /// <param name="terceroId">La ficha de la que cuelga.</param>
    /// <param name="version">La versión de la FICHA sobre la que se escribe.</param>
    /// <param name="peticion">Datos del contacto.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<Resultado<ContactoDto>> EjecutarAsync(
        Guid terceroId,
        VersionDeRecurso version,
        ContactoDeAltaDto peticion,
        CancellationToken cancelacion);
}

/// <summary>Quita un contacto de un tercero.</summary>
public interface IQuitarContacto
{
    /// <summary>Ejecuta el caso de uso.</summary>
    /// <param name="terceroId">La ficha de la que cuelga.</param>
    /// <param name="contactoId">Cuál.</param>
    /// <param name="version">La versión de la FICHA sobre la que se escribe.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<Resultado> EjecutarAsync(
        Guid terceroId,
        Guid contactoId,
        VersionDeRecurso version,
        CancellationToken cancelacion);
}

/// <inheritdoc cref="IListarContactos"/>
internal sealed class ListarContactos(IRepositorioDeTerceros terceros) : IListarContactos
{
    public async Task<Resultado<IReadOnlyList<ContactoDto>>> EjecutarAsync(
        Guid terceroId,
        CancellationToken cancelacion)
    {
        Tercero? tercero = await terceros
            .ObtenerConLoQueCuelgaAsync(terceroId, cancelacion)
            .ConfigureAwait(false);

        // Sin paginación, y es una decisión: los contactos de una ficha son unos pocos, y una
        // página sobre un puñado de filas obliga a quien pinta la pantalla a montar un paginador
        // que no se va a usar nunca. El día que alguien cargue mil contactos en un tercero, esto
        // se hace un listado; hasta entonces sería andamiaje.
        return tercero is null
            ? Resultado.Fallo<IReadOnlyList<ContactoDto>>(ErroresDeTercero.NoEncontrado(terceroId))
            : Resultado.Correcto<IReadOnlyList<ContactoDto>>(
                [.. tercero.Contactos.Select(contacto => contacto.ADto())]);
    }
}

/// <inheritdoc cref="IAgregarContacto"/>
internal sealed class AgregarContacto(
    IRepositorioDeTerceros terceros,
    IUnidadTrabajoDeTerceros unidadTrabajo,
    IVersionesDeTerceros versiones,
    TimeProvider reloj) : IAgregarContacto
{
    public async Task<Resultado<ContactoDto>> EjecutarAsync(
        Guid terceroId,
        VersionDeRecurso version,
        ContactoDeAltaDto peticion,
        CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(peticion);

        Tercero? tercero = await terceros
            .ObtenerConLoQueCuelgaAsync(terceroId, cancelacion)
            .ConfigureAwait(false);

        if (tercero is null)
        {
            return Resultado.Fallo<ContactoDto>(ErroresDeTercero.NoEncontrado(terceroId));
        }

        // LA VERSIÓN QUE SE EXIGE ES LA DE LA FICHA, no la del contacto, porque el contacto no
        // tiene versión propia ni debe tenerla: lo que se está modificando es el agregado. Así,
        // colgar un contacto mientras otro cambia la razón social de la misma ficha se detecta;
        // con un testigo por hijo, las dos escrituras pasarían y la segunda pisaría a la primera
        // sin que ninguna versión cambiara de manera visible.
        versiones.Exigir(tercero, version);

        var errores = new ErroresPorCampo();
        Correo? correo = null;

        if (!string.IsNullOrWhiteSpace(peticion.Correo) && !Correo.Intentar(peticion.Correo, out correo))
        {
            errores.Agregar("correo", "No es una dirección de correo válida.");
        }

        if (errores.Hay)
        {
            return Resultado.Fallo<ContactoDto>(errores.AError());
        }

        Contacto contacto = tercero.AgregarContacto(
            peticion.Nombre, peticion.Cargo, correo, peticion.Telefono, reloj.GetUtcNow());

        await unidadTrabajo.ConfirmarAsync(cancelacion).ConfigureAwait(false);

        return Resultado.Correcto(contacto.ADto());
    }
}

/// <inheritdoc cref="IQuitarContacto"/>
internal sealed class QuitarContacto(
    IRepositorioDeTerceros terceros,
    IUnidadTrabajoDeTerceros unidadTrabajo,
    IVersionesDeTerceros versiones) : IQuitarContacto
{
    public async Task<Resultado> EjecutarAsync(
        Guid terceroId,
        Guid contactoId,
        VersionDeRecurso version,
        CancellationToken cancelacion)
    {
        Tercero? tercero = await terceros
            .ObtenerConLoQueCuelgaAsync(terceroId, cancelacion)
            .ConfigureAwait(false);

        if (tercero is null)
        {
            return Resultado.Fallo(ErroresDeTercero.NoEncontrado(terceroId));
        }

        versiones.Exigir(tercero, version);

        // AQUÍ SÍ SE BORRA LA FILA, y conviene decir por qué no choca con el art. 32. Un contacto
        // no es el interesado de la ficha: es una persona de contacto que ya no trabaja ahí, o que
        // se apuntó por error. Nada de lo emitido cuelga de él —las facturas van al tercero—, así
        // que conservarlo no protege ninguna cuenta y sí mantiene el nombre y el teléfono de
        // alguien que dejó de tener relación con el negocio. Lo que la ley pide para ese dato es
        // justo lo contrario de reservarlo: quitarlo. El rastro de que existió está en la traza.
        tercero.QuitarContacto(contactoId);

        await unidadTrabajo.ConfirmarAsync(cancelacion).ConfigureAwait(false);

        return Resultado.Correcto();
    }
}
