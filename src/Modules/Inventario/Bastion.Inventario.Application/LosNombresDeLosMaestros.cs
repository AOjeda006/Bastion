using Bastion.BuildingBlocks.Application.Autorizacion;
using Bastion.BuildingBlocks.Application.Exportacion;
using Bastion.BuildingBlocks.Contracts.Maestros;
using Bastion.BuildingBlocks.Domain.Autorizacion;
using Bastion.Catalogo.Contracts;
using Bastion.Catalogo.Contracts.Catalogo;
using Bastion.Organizacion.Contracts;
using Bastion.Organizacion.Contracts.Almacenes;
using Bastion.Organizacion.Contracts.Ubicaciones;
using Bastion.Organizacion.Contracts.Unidades;

namespace Bastion.Inventario.Application;

/// <summary>
/// Los nombres de los cuatro maestros a los que apuntan las exportaciones de Inventario: el
/// almacén, la ubicación, el artículo y la unidad, cada uno solo con el permiso de verlo.
/// </summary>
/// <remarks>
/// <para>
/// <b>Los cuatro son de otro módulo</b>, así que ninguno se nombra sin mirar (ADR-0074 §3): sin el
/// permiso va su identificador, que es lo que enseña la pantalla, y no se pregunta a su dueño.
/// </para>
/// <para>
/// <b>Está aquí, y no repetido en cada exportación</b>, porque seis exportaciones citan los mismos
/// cuatro maestros, y cada permiso, escrito seis veces, es un sitio más donde cambiarlo por el de
/// otro maestro copiando una línea. Aquí cada maestro va con el suyo una sola vez.
/// </para>
/// </remarks>
internal sealed class LosNombresDeLosMaestros(
    IConsultaDeAlmacenes almacenes,
    IConsultaDeUbicaciones ubicaciones,
    IConsultaDeArticulos articulos,
    IConsultaDeUnidadesDeMedida unidades,
    IUsuarioActual usuarioActual)
{
    private static readonly Permiso s_verAlmacenes = Permiso.De(PermisosDeOrganizacion.AlmacenVer);

    private static readonly Permiso s_verUbicaciones = Permiso.De(PermisosDeOrganizacion.UbicacionVer);

    private static readonly Permiso s_verArticulos = Permiso.De(PermisosDeCatalogo.ArticuloVer);

    private static readonly Permiso s_verUnidades = Permiso.De(PermisosDeOrganizacion.UnidadMedidaVer);

    /// <summary>Los almacenes de una página, si quien exporta puede verlos.</summary>
    /// <param name="ids">Los identificadores de la página, con repetidos.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    public Task<IReadOnlyDictionary<Guid, NombreDeMaestro>> DeAlmacenesAsync(
        IEnumerable<Guid?> ids,
        CancellationToken cancelacion) =>
        NombresDeMaestros.PedirAsync(usuarioActual, s_verAlmacenes, ids, almacenes.NombresDeAsync, cancelacion);

    /// <summary>Las ubicaciones de una página, si quien exporta puede verlas.</summary>
    /// <param name="ids">Los identificadores de la página, con repetidos.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    public Task<IReadOnlyDictionary<Guid, NombreDeMaestro>> DeUbicacionesAsync(
        IEnumerable<Guid?> ids,
        CancellationToken cancelacion) =>
        NombresDeMaestros.PedirAsync(usuarioActual, s_verUbicaciones, ids, ubicaciones.NombresDeAsync, cancelacion);

    /// <summary>Los artículos de una página, si quien exporta puede verlos.</summary>
    /// <param name="ids">Los identificadores de la página, con repetidos.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    public Task<IReadOnlyDictionary<Guid, NombreDeMaestro>> DeArticulosAsync(
        IEnumerable<Guid?> ids,
        CancellationToken cancelacion) =>
        NombresDeMaestros.PedirAsync(usuarioActual, s_verArticulos, ids, articulos.NombresDeAsync, cancelacion);

    /// <summary>Las unidades de una página, si quien exporta puede verlas.</summary>
    /// <param name="ids">Los identificadores de la página, con repetidos.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    public Task<IReadOnlyDictionary<Guid, NombreDeMaestro>> DeUnidadesAsync(
        IEnumerable<Guid?> ids,
        CancellationToken cancelacion) =>
        NombresDeMaestros.PedirAsync(usuarioActual, s_verUnidades, ids, unidades.NombresDeAsync, cancelacion);
}
