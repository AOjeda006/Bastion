using Bastion.BuildingBlocks.Application;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Organizacion.Application.Comun;
using Bastion.Organizacion.Application.Empresas;
using Bastion.Organizacion.Contracts.Almacenes;
using Bastion.Organizacion.Domain.Almacenes;

namespace Bastion.Organizacion.Application.Almacenes;

/// <summary>Da de alta un almacén.</summary>
public interface ICrearAlmacen
{
    /// <summary>Ejecuta el caso de uso.</summary>
    /// <param name="peticion">Datos del almacén que se quiere dar de alta.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<Resultado<AlmacenDto>> EjecutarAsync(CrearAlmacenDto peticion, CancellationToken cancelacion);
}

/// <inheritdoc cref="ICrearAlmacen"/>
internal sealed class CrearAlmacen(
    IRepositorioDeAlmacenes almacenes,
    IRepositorioDeEmpresas empresas,
    IUnidadTrabajo unidadTrabajo) : ICrearAlmacen
{
    public async Task<Resultado<AlmacenDto>> EjecutarAsync(
        CrearAlmacenDto peticion,
        CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(peticion);

        var errores = new ErroresPorCampo();

        if (!await empresas.ExisteAsync(peticion.EmpresaId, cancelacion).ConfigureAwait(false))
        {
            errores.Agregar("empresaId", "No hay ninguna empresa con ese identificador.");
        }

        if (!Enumerados.Intentar(peticion.Tipo, out TipoDeAlmacen tipo))
        {
            errores.Agregar(
                "tipo",
                $"No es un tipo de almacén conocido. Admitidos: {Enumerados.Admitidos<TipoDeAlmacen>()}.");
        }
        else if (tipo == TipoDeAlmacen.Fisico && peticion.Direccion is null)
        {
            // La regla es del dominio y allí LANZA. Aquí se adelanta porque el usuario no ha
            // hecho nada absurdo: ha marcado «físico» y no ha rellenado la dirección, que es un
            // campo del formulario y merece que se le diga cuál.
            errores.Agregar("direccion", "Un almacén físico tiene dirección: es un sitio al que llega mercancía.");
        }

        if (errores.Hay)
        {
            return Resultado.Fallo<AlmacenDto>(errores.AError());
        }

        string codigo = Almacen.NormalizarCodigo(peticion.Codigo);

        if (await almacenes.ExisteElCodigoAsync(peticion.EmpresaId, codigo, cancelacion)
                .ConfigureAwait(false))
        {
            return Resultado.Fallo<AlmacenDto>(ErrorDeOperacion.Conflicto(
                "almacen-duplicado",
                $"La empresa ya tiene un almacén con el código {codigo}."));
        }

        var almacen = Almacen.Crear(
            peticion.EmpresaId,
            peticion.Codigo,
            peticion.Nombre,
            peticion.Direccion?.ADireccion(),
            tipo);

        almacenes.Agregar(almacen);
        await unidadTrabajo.ConfirmarAsync(cancelacion).ConfigureAwait(false);

        return Resultado.Correcto(almacen.ADto());
    }
}
