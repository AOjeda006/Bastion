using Bastion.BuildingBlocks.Application.Validacion;
using Bastion.BuildingBlocks.Domain.Dinero;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Organizacion.Application.Comun;
using Bastion.Organizacion.Contracts.Divisas;
using Bastion.Organizacion.Domain.Divisas;

namespace Bastion.Organizacion.Application.Divisas;

/// <summary>Da de alta una divisa con la que operar.</summary>
public interface ICrearDivisa
{
    /// <summary>Ejecuta el caso de uso.</summary>
    /// <param name="peticion">Datos de la divisa.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<Resultado<DivisaDto>> EjecutarAsync(CrearDivisaDto peticion, CancellationToken cancelacion);
}

/// <inheritdoc cref="ICrearDivisa"/>
internal sealed class CrearDivisa(
    IRepositorioDeDivisas divisas,
    IUnidadTrabajoDeOrganizacion unidadTrabajo,
    TimeProvider reloj) : ICrearDivisa
{
    public async Task<Resultado<DivisaDto>> EjecutarAsync(
        CrearDivisaDto peticion,
        CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(peticion);

        string codigo = Divisa.NormalizarCodigo(peticion.Codigo);

        // La puerta que PREGUNTA, no la que exige (ADR-0004). El dominio también lo comprueba y
        // allí lanza, pero aquí quien escribió «KWD» no ha cometido un fallo de programación: ha
        // pedido una divisa cuyo redondeo esta instalación no sabe, y eso se contesta señalando
        // el campo. Que la tabla no pueda tener una divisa que el catálogo no sepa redondear es
        // lo que impide que las dos acaben diciendo cosas distintas.
        if (!CatalogoDeDivisas.EsConocida(codigo))
        {
            var errores = new ErroresPorCampo();
            errores.Agregar(
                "codigo",
                $"No se conoce el redondeo fiscal de {codigo}. Hay que añadirla antes al catálogo " +
                "de divisas, con su caso dorado: guardarla sin saber con cuántos decimales " +
                "redondea es una factura mal calculada esperando.");

            return Resultado.Fallo<DivisaDto>(errores.AError());
        }

        if (await divisas.ExisteElCodigoAsync(codigo, cancelacion).ConfigureAwait(false))
        {
            return Resultado.Fallo<DivisaDto>(ErrorDeOperacion.Conflicto(
                "divisa-duplicada",
                $"Ya hay una divisa con el código {codigo}."));
        }

        var divisa = Divisa.Crear(peticion.Codigo, peticion.Nombre, reloj.GetUtcNow());

        divisas.Agregar(divisa);
        await unidadTrabajo.ConfirmarAsync(cancelacion).ConfigureAwait(false);

        return Resultado.Correcto(divisa.ADto());
    }
}
