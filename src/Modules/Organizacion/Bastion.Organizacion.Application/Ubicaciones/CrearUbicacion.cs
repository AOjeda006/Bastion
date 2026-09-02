using Bastion.BuildingBlocks.Application.Autorizacion;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Organizacion.Application.Almacenes;
using Bastion.Organizacion.Application.Comun;
using Bastion.Organizacion.Application.Empresas;
using Bastion.Organizacion.Contracts.Ubicaciones;
using Bastion.Organizacion.Domain.Almacenes;
using Bastion.Organizacion.Domain.Ubicaciones;

namespace Bastion.Organizacion.Application.Ubicaciones;

/// <summary>Da de alta una ubicación dentro de un almacén.</summary>
public interface ICrearUbicacion
{
    /// <summary>Ejecuta el caso de uso.</summary>
    /// <param name="peticion">Datos de la ubicación.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<Resultado<UbicacionDto>> EjecutarAsync(
        CrearUbicacionDto peticion,
        CancellationToken cancelacion);
}

/// <inheritdoc cref="ICrearUbicacion"/>
internal sealed class CrearUbicacion(
    IUsuarioActual usuarioActual,
    IRepositorioDeUbicaciones ubicaciones,
    IRepositorioDeAlmacenes almacenes,
    IRepositorioDeEmpresas empresas,
    IUnidadTrabajoDeOrganizacion unidadTrabajo,
    TimeProvider reloj) : ICrearUbicacion
{
    public async Task<Resultado<UbicacionDto>> EjecutarAsync(
        CrearUbicacionDto peticion,
        CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(peticion);

        // La empresa sale del CLAIM y no de la petición (R8): `CrearUbicacionDto` no tiene el
        // campo, así que no hay ningún camino por el que llegue de fuera.
        Guid empresaId = usuarioActual.EmpresaId;

        if (!await empresas.EstaActivaAsync(empresaId, cancelacion).ConfigureAwait(false))
        {
            return Resultado.Fallo<UbicacionDto>(ErroresDeEmpresa.NoOperativa());
        }

        // El almacén se busca por el repositorio ORDINARIO, que ya filtra por empresa y deja fuera
        // lo bloqueado. Así, un almacén de otra sociedad no existe para esta petición: no hace
        // falta comparar la empresa a mano —y por tanto no se puede olvidar—, y uno bloqueado
        // tampoco admite ubicaciones nuevas.
        Almacen? almacen = await almacenes.ObtenerAsync(peticion.AlmacenId, cancelacion)
            .ConfigureAwait(false);

        if (almacen is null)
        {
            return Resultado.Fallo<UbicacionDto>(ErrorDeOperacion.NoEncontrado(
                "almacen-no-encontrado",
                $"No hay ningún almacén con el identificador {peticion.AlmacenId}."));
        }

        string codigo = Ubicacion.NormalizarCodigo(peticion.Codigo);

        if (await ubicaciones.ExisteElCodigoAsync(almacen.Id, codigo, cancelacion)
                .ConfigureAwait(false))
        {
            return Resultado.Fallo<UbicacionDto>(ErrorDeOperacion.Conflicto(
                "ubicacion-duplicada",
                $"El almacén ya tiene una ubicación con el código {codigo}."));
        }

        var ubicacion = Ubicacion.Crear(
            empresaId,
            almacen.Id,
            peticion.Codigo,
            peticion.Pasillo,
            peticion.Estante,
            peticion.Hueco,
            peticion.Descripcion,
            reloj.GetUtcNow());

        ubicaciones.Agregar(ubicacion);
        await unidadTrabajo.ConfirmarAsync(cancelacion).ConfigureAwait(false);

        return Resultado.Correcto(ubicacion.ADto());
    }
}
