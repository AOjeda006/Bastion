using Bastion.BuildingBlocks.Application.Autorizacion;
using Bastion.BuildingBlocks.Application.Validacion;
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
    IUsuarioActual usuarioActual,
    IRepositorioDeSeries series,
    IRepositorioDeEjercicios ejercicios,
    IRepositorioDeEmpresas empresas,
    IUnidadTrabajoDeOrganizacion unidadTrabajo,
    TimeProvider reloj) : ICrearSerie
{
    public async Task<Resultado<SerieDto>> EjecutarAsync(
        CrearSerieDto peticion,
        CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(peticion);

        // La empresa sale del CLAIM y no de la petición (R8). `CrearSerieDto` no tiene el campo.
        Guid empresaId = usuarioActual.EmpresaId;

        if (!await empresas.EstaActivaAsync(empresaId, cancelacion).ConfigureAwait(false))
        {
            return Resultado.Fallo<SerieDto>(ErroresDeEmpresa.NoOperativa());
        }

        var errores = new ErroresPorCampo();

        Ejercicio? ejercicio = await ejercicios.ObtenerAsync(peticion.EjercicioId, cancelacion)
            .ConfigureAwait(false);

        if (ejercicio is null)
        {
            errores.Agregar("ejercicioId", "No hay ningún ejercicio con ese identificador.");
        }
        else if (ejercicio.EmpresaId != empresaId)
        {
            // Sigue haciendo falta con la empresa saliendo del claim, y aquí se ve por qué: el
            // ejercicio SÍ viene en la petición, así que un cliente puede nombrar el de otra
            // empresa. Sin esta comprobación, la serie quedaría colgando de dos contabilidades a
            // la vez. Y el mensaje no dice de cuál es: eso confirmaría que ese identificador
            // existe en otra empresa, que es justo lo que no se le cuenta a quien no pertenece.
            errores.Agregar("ejercicioId", "No hay ningún ejercicio con ese identificador.");
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

        if (await series.ExisteElCodigoAsync(empresaId, peticion.EjercicioId, codigo, cancelacion)
                .ConfigureAwait(false))
        {
            return Resultado.Fallo<SerieDto>(ErrorDeOperacion.Conflicto(
                "serie-duplicada",
                $"Ese ejercicio ya tiene una serie con el código {codigo}."));
        }

        var serie = Serie.Crear(
            empresaId, peticion.EjercicioId, tipo, peticion.Codigo, peticion.Formato,
            reloj.GetUtcNow());

        series.Agregar(serie);
        await unidadTrabajo.ConfirmarAsync(cancelacion).ConfigureAwait(false);

        return Resultado.Correcto(serie.ADto());
    }
}
