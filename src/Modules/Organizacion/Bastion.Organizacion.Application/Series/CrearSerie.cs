using Bastion.BuildingBlocks.Application;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Organizacion.Application.Comun;
using Bastion.Organizacion.Application.Ejercicios;
using Bastion.Organizacion.Application.Empresas;
using Bastion.Organizacion.Contracts.Series;
using Bastion.Organizacion.Domain.Ejercicios;
using Bastion.Organizacion.Domain.Series;

namespace Bastion.Organizacion.Application.Series;

/// <summary>Crea una serie de numeración.</summary>
public interface ICrearSerie
{
    /// <summary>Ejecuta el caso de uso.</summary>
    /// <param name="peticion">Datos de la serie que se quiere crear.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<Resultado<SerieDto>> EjecutarAsync(CrearSerieDto peticion, CancellationToken cancelacion);
}

/// <inheritdoc cref="ICrearSerie"/>
internal sealed class CrearSerie(
    IRepositorioDeSeries series,
    IRepositorioDeEjercicios ejercicios,
    IRepositorioDeEmpresas empresas,
    IUnidadTrabajo unidadTrabajo) : ICrearSerie
{
    public async Task<Resultado<SerieDto>> EjecutarAsync(
        CrearSerieDto peticion,
        CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(peticion);

        var errores = new ErroresPorCampo();

        if (!await empresas.ExisteAsync(peticion.EmpresaId, cancelacion).ConfigureAwait(false))
        {
            errores.Agregar("empresaId", "No hay ninguna empresa con ese identificador.");
        }

        Ejercicio? ejercicio = await ejercicios.ObtenerAsync(peticion.EjercicioId, cancelacion)
            .ConfigureAwait(false);

        if (ejercicio is null)
        {
            errores.Agregar("ejercicioId", "No hay ningún ejercicio con ese identificador.");
        }
        else if (ejercicio.EmpresaId != peticion.EmpresaId)
        {
            // Sin esta comprobación se podría colgar una serie de la empresa A del ejercicio de
            // la empresa B: las dos claves ajenas serían válidas por separado y la fila quedaría
            // apuntando a dos contabilidades distintas a la vez.
            errores.Agregar("ejercicioId", "Ese ejercicio es de otra empresa.");
        }

        if (!Enumerados.Intentar(peticion.TipoDeDocumento, out TipoDeDocumento tipo))
        {
            errores.Agregar(
                "tipoDeDocumento",
                $"No es un tipo de documento conocido. Admitidos: {Enumerados.Admitidos<TipoDeDocumento>()}.");
        }

        if (errores.Hay)
        {
            return Resultado.Fallo<SerieDto>(errores.AError());
        }

        // El código se normaliza a mayúsculas en el dominio, así que la comprobación de duplicado
        // se hace sobre el valor normalizado: si no, «fac» y «FAC» pasarían el filtro y chocarían
        // luego contra el índice único, que es un 500 en lugar de un 409.
        string codigo = Serie.NormalizarCodigo(peticion.Codigo);

        if (await series.ExisteElCodigoAsync(peticion.EmpresaId, peticion.EjercicioId, codigo, cancelacion)
                .ConfigureAwait(false))
        {
            return Resultado.Fallo<SerieDto>(ErrorDeOperacion.Conflicto(
                "serie-duplicada",
                $"Ese ejercicio ya tiene una serie con el código {codigo}."));
        }

        var serie = Serie.Crear(
            peticion.EmpresaId, peticion.EjercicioId, tipo, peticion.Codigo, peticion.Formato);

        series.Agregar(serie);
        await unidadTrabajo.ConfirmarAsync(cancelacion).ConfigureAwait(false);

        return Resultado.Correcto(serie.ADto());
    }
}
