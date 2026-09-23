using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Inventario.Contracts.Ajustes;
using Bastion.Inventario.Domain.Ajustes;
using Bastion.Inventario.Domain.Movimientos;
using Bastion.Organizacion.Contracts.Ejercicios;

namespace Bastion.Inventario.Application.Ajustes;

/// <summary>Confirma un ajuste y mueve el libro.</summary>
public interface IConfirmarAjuste
{
    /// <summary>Ejecuta la confirmación.</summary>
    /// <param name="ajusteId">El documento.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    /// <returns>El ajuste confirmado, o el motivo por el que no se confirma.</returns>
    Task<Resultado<AjusteDto>> EjecutarAsync(Guid ajusteId, CancellationToken cancelacion);
}

/// <summary>
/// La transición que hace verdad el documento: cabecera y filas del libro, en el mismo
/// <c>COMMIT</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Esta es la transacción que dobla la R12 a sabiendas</b>, y está decidido y escrito en
/// <c>docs/PLAN.md</c>: se modifican dos agregados —el <c>Ajuste</c> y las filas del libro— en una
/// sola transacción. La alternativa era publicar un evento y que un suscriptor escribiera los
/// movimientos, y eso deja una ventana con un ajuste confirmado y el stock sin mover: rompería la
/// R3, que dice que el libro <b>es</b> la verdad. De las dos, es peor romper la R3.
/// </para>
/// <para>
/// <b>Los movimientos no se construyen aquí.</b> Los devuelve <c>Ajuste.Confirmar</c>, que es lo
/// que hace imposible confirmar sin generarlos: si este caso de uso se olvidara de guardarlos,
/// tendría delante una lista sin usar en vez de un documento confirmado y un libro quieto.
/// </para>
/// <para>
/// <b>Y el evento va por documento, no por movimiento.</b> Uno por fila convertiría la bandeja en
/// una segunda copia de la tabla que más crece del sistema.
/// </para>
/// <para>
/// <b>El número se toma aquí y no antes</b>, y «aquí» quiere decir dentro de la transacción que
/// abre el filtro de idempotencia, la misma en la que caen la cabecera y las filas del libro. Si se
/// tomara al abrir, cada borrador tirado dejaría un hueco; si se tomara fuera de la transacción,
/// quedaría gastado cuando el <c>COMMIT</c> no llegara. Las dos cosas son lo que la R5 llama
/// «sin huecos», y el mecanismo <b>revienta</b> si no hay transacción abierta en vez de fiarse.
/// </para>
/// <para>
/// <b>Y se toma antes de transitar, que es el único orden que deja el número dentro del recibo de
/// idempotencia.</b> Lo que se guarda como recibo es el cuerpo de la respuesta, y el cuerpo sale
/// del DTO del ajuste: numerar después de componerlo devolvería un <c>Numero</c> nulo al primer
/// llamante y otro distinto al reintento con la misma clave.
/// </para>
/// </remarks>
/// <param name="ajustes">Dónde viven el documento y el libro.</param>
/// <param name="numerador">Quién entrega el correlativo, en esta misma transacción (R5).</param>
/// <param name="ejercicios">Si la fecha del documento se puede escribir, con la fila bloqueada.</param>
/// <param name="unidadTrabajo">La transacción.</param>
/// <param name="reloj">De dónde sale «ahora».</param>
internal sealed class ConfirmarAjuste(
    IRepositorioDeAjustes ajustes,
    INumeradorDeSeriesDeInventario numerador,
    IConsultaDeEjercicios ejercicios,
    IUnidadTrabajoDeInventario unidadTrabajo,
    TimeProvider reloj) : IConfirmarAjuste
{
    /// <inheritdoc/>
    public async Task<Resultado<AjusteDto>> EjecutarAsync(
        Guid ajusteId,
        CancellationToken cancelacion)
    {
        Ajuste? ajuste = await ajustes.ObtenerAsync(ajusteId, cancelacion).ConfigureAwait(false);

        if (ajuste is null)
        {
            return Resultado.Fallo<AjusteDto>(ErroresDeAjuste.NoEncontrado(ajusteId));
        }

        // El estado se comprueba ANTES de transitar, aunque la transición también lo compruebe.
        // No es una comprobación repetida por costumbre: el dominio lanza —una transición
        // imposible es una invariante rota— y lo que el borde necesita para un ajuste ya
        // confirmado es un 409 con su motivo, no un 500 (ADR-0004).
        if (ajuste.Estado != EstadoDeAjuste.Borrador)
        {
            return Resultado.Fallo<AjusteDto>(
                ErroresDeAjuste.NoEstaEnBorrador(ajusteId, ajuste.Estado.ToString()));
        }

        // EL EJERCICIO, ANTES QUE EL NÚMERO. El orden es el que importa: esta lectura toma un
        // cerrojo COMPARTIDO sobre la fila del ejercicio y no lo suelta hasta el `COMMIT`, así que
        // un cierre que llegue a partir de aquí espera a que este documento acabe —y uno que ya
        // hubiera terminado deja esta lectura contestando «Cerrado»—. Preguntar después de
        // numerar gastaría un número para nada cada vez que el periodo no admita el documento.
        //
        // Y ES LA GUARDA, no la cortesía. Lo que el cierre le pregunta a cada módulo —si le quedan
        // borradores dentro— avisa a quien cierra de que va a dejar papeles colgando; esto es lo
        // que impide que un documento se haga definitivo en un periodo que ya lo era. Sin la
        // cortesía alguien se lleva un susto; sin esto, el libro deja de cuadrar con lo presentado.
        EstadoDelEjercicioParaEscribir ejercicio = await ejercicios
            .ParaEscribirEnAsync(ajuste.FechaDeOperacion, cancelacion)
            .ConfigureAwait(false);

        if (ejercicio is not EstadoDelEjercicioParaEscribir.Abierto)
        {
            return Resultado.Fallo<AjusteDto>(
                ejercicio is EstadoDelEjercicioParaEscribir.SinEjercicio
                    ? ErroresDeAjuste.SinEjercicio(ajuste.FechaDeOperacion)
                    : ErroresDeAjuste.EnEjercicioCerrado(ajuste.FechaDeOperacion));
        }

        // EL NÚMERO, ANTES DE TOCAR EL DOCUMENTO. El orden no es estético: la sentencia toma el
        // cerrojo sobre la fila del contador y lo suelta el `COMMIT`, así que cuanto más tarde se
        // pida, más corto es el tramo en el que otra confirmación de la misma serie espera —pero
        // tiene que caer dentro de la transacción, y deshacerla es lo único que devuelve el
        // número—. Y si la serie no numera, aquí no se ha cambiado nada todavía.
        Resultado<long> numero = await numerador
            .TomarNumeroAsync(ajuste.SerieId, cancelacion)
            .ConfigureAwait(false);

        if (!numero.EsCorrecto)
        {
            return Resultado.Fallo<AjusteDto>(numero.Error!);
        }

        var evento = new AjusteConfirmado(
            ajuste.Id,
            ajuste.EmpresaId,
            ajuste.AlmacenId,
            ajuste.FechaDeOperacion,
            ajuste.Lineas.Count);

        IReadOnlyList<MovimientoStock> movimientos =
            ajuste.Confirmar(numero.Valor, evento, reloj.GetUtcNow());

        ajustes.AgregarMovimientos(movimientos);
        await unidadTrabajo.ConfirmarAsync(cancelacion).ConfigureAwait(false);

        return Resultado.Correcto(ajuste.ADto());
    }
}
