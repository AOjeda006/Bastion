using Bastion.BuildingBlocks.Application.Validacion;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Organizacion.Application.Comun;
using Bastion.Organizacion.Contracts.Impuestos;
using Bastion.Organizacion.Domain.Impuestos;

namespace Bastion.Organizacion.Application.Impuestos;

/// <summary>Abre un tramo de un tipo impositivo.</summary>
public interface ICrearImpuesto
{
    /// <summary>Ejecuta el caso de uso.</summary>
    /// <param name="peticion">Datos del tramo que se quiere abrir.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<Resultado<ImpuestoDto>> EjecutarAsync(
        CrearImpuestoDto peticion,
        CancellationToken cancelacion);
}

/// <inheritdoc cref="ICrearImpuesto"/>
internal sealed class CrearImpuesto(
    IRepositorioDeImpuestos impuestos,
    IUnidadTrabajoDeOrganizacion unidadTrabajo,
    TimeProvider reloj) : ICrearImpuesto
{
    public async Task<Resultado<ImpuestoDto>> EjecutarAsync(
        CrearImpuestoDto peticion,
        CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(peticion);

        // Aquí NO se lee la empresa del claim, y esa ausencia es la decisión: un tipo impositivo
        // lo fija el BOE para todas las sociedades que operan en España. Es uno de los cinco
        // maestros compartidos que la R8 manda marcar explícitamente, y la marca está en el
        // barrido de inquilinato con su motivo escrito.
        var errores = new ErroresPorCampo();

        if (!Enumerados.Intentar(peticion.Tipo, out TipoDeImpuesto tipo))
        {
            errores.Agregar(
                "tipo",
                $"No es una clase de impuesto conocida. Admitidas: {Enumerados.Admitidos<TipoDeImpuesto>()}.");
        }

        if (peticion.VigenteHasta is { } hasta && hasta < peticion.VigenteDesde)
        {
            // La regla es del dominio y allí lanza. Se adelanta porque quien rellenó el formulario
            // no ha hecho nada absurdo —ha puesto dos fechas y las ha cruzado— y merece que se le
            // diga cuál de las dos mirar.
            errores.Agregar(
                "vigenteHasta",
                "Un impuesto no puede dejar de regir antes de empezar a regir.");
        }

        if (errores.Hay)
        {
            return Resultado.Fallo<ImpuestoDto>(errores.AError());
        }

        string codigo = Impuesto.NormalizarCodigo(peticion.Codigo);

        if (await impuestos
                .HaySolapeAsync(codigo, peticion.VigenteDesde, peticion.VigenteHasta, null, cancelacion)
                .ConfigureAwait(false))
        {
            return Resultado.Fallo<ImpuestoDto>(ErroresDeImpuesto.Solapado(codigo));
        }

        var impuesto = Impuesto.Crear(
            peticion.Codigo,
            peticion.Nombre,
            tipo,
            peticion.Porcentaje,
            peticion.VigenteDesde,
            peticion.VigenteHasta,
            peticion.CuentaRepercutido,
            peticion.CuentaSoportado,
            reloj.GetUtcNow());

        impuestos.Agregar(impuesto);
        await unidadTrabajo.ConfirmarAsync(cancelacion).ConfigureAwait(false);

        return Resultado.Correcto(impuesto.ADto());
    }
}
