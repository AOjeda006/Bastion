using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Inventario.Contracts.Ajustes;
using Bastion.Inventario.Domain.Ajustes;
using Bastion.Inventario.Domain.Movimientos;
using Bastion.Organizacion.Contracts.Ejercicios;

namespace Bastion.Inventario.Application.Ajustes;

/// <summary>Anula un ajuste confirmado oponiéndole un contra-documento (R2).</summary>
public interface IAnularAjuste
{
    /// <summary>Ejecuta la anulación.</summary>
    /// <param name="ajusteId">El documento que se anula.</param>
    /// <param name="peticion">Por qué se anula.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    /// <returns>El par —original anulado e inverso confirmado—, o el motivo por el que no.</returns>
    Task<Resultado<AnulacionDto>> EjecutarAsync(
        Guid ajusteId,
        AnularAjusteDto peticion,
        CancellationToken cancelacion);
}

/// <summary>
/// La R2 entera, en una transacción: nace el inverso, toma su número, escribe sus filas del libro,
/// y solo entonces el original queda anulado.
/// </summary>
/// <remarks>
/// <para>
/// <b>Anular no borra ni revierte: añade.</b> Las filas que el original escribió siguen ahí —el
/// libro es de solo añadido (R3)— y lo que compensa su efecto es otro documento con el signo
/// contrario. Por eso el inverso es un ajuste de pleno derecho y no una marca: gasta correlativo
/// de la misma serie (R5), escribe sus movimientos (R13) y se lee como cualquier otro.
/// </para>
/// <para>
/// <b>El orden de los cuatro pasos es la regla, no una preferencia.</b> El número se pide después
/// de construir el inverso y antes de confirmarlo, por lo mismo que en la confirmación: dentro de
/// la transacción que abre el filtro de idempotencia, lo más tarde posible —el cerrojo del
/// contador lo suelta el <c>COMMIT</c>— y con el documento todavía sin tocar, para que una serie
/// cerrada no deje nada a medias. Y el original se da por anulado el último: al revés habría un
/// instante con un ajuste anulado y su efecto en el libro sin compensar.
/// </para>
/// <para>
/// <b>La fecha del inverso es la de hoy y no la del original</b>, decidido en <c>docs/PLAN.md</c>
/// con su motivo contable: heredarla asentaría el inverso en el periodo del original, y anular un
/// documento de un ejercicio cerrado sería escribir dentro de él. La consecuencia es que el par
/// cae en dos periodos —y en dos particiones del libro—, que es correcto: el saldo a una fecha
/// anterior a la anulación sigue enseñando lo que el original movió, porque así fue.
/// </para>
/// <para>
/// <b>La serie es la del original y aquí no se pregunta por ella</b>, igual que en la
/// confirmación: quien sostiene la R5 es el <c>WHERE</c> de la sentencia que toma el número, que
/// vuelve a exigir dentro de esta transacción que la serie exista, sea de esta empresa y siga
/// abierta. Si está cerrada, la anulación falla con el <b>mismo</b> error que una confirmación
/// contra una serie cerrada, y eso es la respuesta correcta: la R5 no tiene excepciones por
/// motivo.
/// </para>
/// <para>
/// <b>Y lo que separa dos anulaciones simultáneas no está aquí.</b> Las dos leen
/// <c>Confirmado</c>, las dos construyen su inverso y las dos se creen con derecho; la segunda en
/// llegar se estrella, y <b>medido</b>, contra el índice único de <c>anula_a_id</c>: anular escribe
/// dos filas —el <c>INSERT</c> del inverso y el <c>UPDATE</c> del original—, el <c>INSERT</c> llega
/// antes, y el testigo de concurrencia de la fila del ajuste (R11) habría dicho lo mismo un
/// instante después. El perdedor se lleva un <c>412</c> por los dos caminos —el testigo lo traduce
/// <c>ManejadorDeVersionObsoleta</c>, y el índice, por su nombre,
/// <c>ManejadorDeCarreraPerdidaEnLaBase</c>— y <b>ningún</b> inverso, porque su transacción entera
/// se deshace y con ella el número que había tomado. Queda uno y no dos: lo impide el motor, y lo
/// impediría la R11 si el motor no estuviera.
/// </para>
/// </remarks>
/// <para>
/// <b>El ejercicio que esto pregunta es el de HOY, no el del original</b>, y por eso anular un
/// documento de un periodo cerrado sigue funcionando: el inverso nace con la fecha de hoy, así que
/// la única fila que hace falta que admita escrituras es la del ejercicio abierto. El cerrado ni
/// se lee ni se toca. Preguntar por el del original sería la lectura equivocada con el nombre
/// correcto: dejaría sin anular precisamente los documentos por los que existe la R2 —los viejos,
/// los que ya nadie puede corregir de otra manera—.
/// </para>
/// <para>
/// <b>Pero preguntar hay que preguntar</b>, porque el inverso <b>se confirma</b> aquí dentro y la
/// R9 no admite un documento confirmado sin periodo al que imputarse. El caso que lo hace visible
/// no es el ejercicio cerrado: es el <b>hueco</b>. Si hoy cae fuera de todo ejercicio —nadie abrió
/// el del año en curso—, sin esta lectura la anulación escribiría un inverso que ninguna
/// autoliquidación recogería, y lo haría sin ruido.
/// </para>
/// <param name="ajustes">Dónde viven el documento y el libro.</param>
/// <param name="numerador">Quién entrega el correlativo, en esta misma transacción (R5).</param>
/// <param name="ejercicios">Si <b>hoy</b> admite escrituras, con la fila bloqueada (R9).</param>
/// <param name="unidadTrabajo">La transacción.</param>
/// <param name="reloj">De dónde sale «hoy».</param>
internal sealed class AnularAjuste(
    IRepositorioDeAjustes ajustes,
    INumeradorDeSeriesDeInventario numerador,
    IConsultaDeEjercicios ejercicios,
    IUnidadTrabajoDeInventario unidadTrabajo,
    TimeProvider reloj) : IAnularAjuste
{
    /// <inheritdoc/>
    public async Task<Resultado<AnulacionDto>> EjecutarAsync(
        Guid ajusteId,
        AnularAjusteDto peticion,
        CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(peticion);

        // EL MOTIVO SE VALIDA AQUÍ Y NO SOLO EN EL DOMINIO. `Ajuste.Abrir` lanza con un motivo
        // vacío —es una invariante—, y lo que el borde necesita para un cuerpo mal escrito es un
        // 400 con su código, no un 500 (ADR-0004). El dominio sigue lanzando: esto no lo sustituye.
        string motivo = (peticion.Motivo ?? string.Empty).Trim();

        if (motivo.Length is 0 or > Ajuste.LargoDelMotivo)
        {
            return Resultado.Fallo<AnulacionDto>(ErroresDeAjuste.MotivoNoValido());
        }

        Ajuste? original = await ajustes.ObtenerAsync(ajusteId, cancelacion).ConfigureAwait(false);

        if (original is null)
        {
            return Resultado.Fallo<AnulacionDto>(ErroresDeAjuste.NoEncontrado(ajusteId));
        }

        // El estado se comprueba antes de construir nada, por lo mismo que en la confirmación: el
        // dominio lanza, y un borrador o un ya anulado merecen un 409 con su motivo. La segunda
        // llamada seguida sale por aquí; la segunda llamada SIMULTÁNEA no, y por eso esto no es
        // lo que sostiene «anular dos veces no crea dos inversos».
        if (original.Estado != EstadoDeAjuste.Confirmado)
        {
            return Resultado.Fallo<AnulacionDto>(
                ErroresDeAjuste.NoEstaConfirmado(ajusteId, original.Estado.ToString()));
        }

        DateTimeOffset ahora = reloj.GetUtcNow();
        var hoy = DateOnly.FromDateTime(ahora.UtcDateTime);

        // LA MISMA PREGUNTA QUE AL CONFIRMAR, Y SOBRE LA MISMA FECHA QUE SE VA A ESCRIBIR: `hoy`,
        // la del inverso. Va antes del número por lo mismo que allí —un periodo que no admite el
        // documento no debe gastar correlativo— y antes de `CrearInverso` porque no hace falta
        // construir lo que no se va a escribir.
        EstadoDelEjercicioParaEscribir ejercicio = await ejercicios
            .ParaEscribirEnAsync(hoy, cancelacion)
            .ConfigureAwait(false);

        if (ejercicio is not EstadoDelEjercicioParaEscribir.Abierto)
        {
            return Resultado.Fallo<AnulacionDto>(
                ejercicio is EstadoDelEjercicioParaEscribir.SinEjercicio
                    ? ErroresDeAjuste.SinEjercicio(hoy)
                    : ErroresDeAjuste.EnEjercicioCerrado(hoy));
        }

        Ajuste inverso = original.CrearInverso(hoy, motivo, ahora);

        Resultado<long> numero = await numerador
            .TomarNumeroAsync(inverso.SerieId, cancelacion)
            .ConfigureAwait(false);

        if (!numero.EsCorrecto)
        {
            return Resultado.Fallo<AnulacionDto>(numero.Error!);
        }

        var confirmado = new AjusteConfirmado(
            inverso.Id,
            inverso.EmpresaId,
            inverso.AlmacenId,
            inverso.FechaDeOperacion,
            inverso.Lineas.Count);

        IReadOnlyList<MovimientoStock> movimientos =
            inverso.Confirmar(numero.Valor, confirmado, ahora);

        original.Anular(inverso, new AjusteAnulado(original.Id, original.EmpresaId));

        ajustes.Agregar(inverso);
        ajustes.AgregarMovimientos(movimientos);
        await unidadTrabajo.ConfirmarAsync(cancelacion).ConfigureAwait(false);

        return Resultado.Correcto(new AnulacionDto(original.ADto(), inverso.ADto()));
    }
}
