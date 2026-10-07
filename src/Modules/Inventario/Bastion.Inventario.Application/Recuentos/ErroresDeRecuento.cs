using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Inventario.Application.Trazabilidad;
using Bastion.Inventario.Domain.LotesYSeries;
using Bastion.Inventario.Domain.Recuentos;

namespace Bastion.Inventario.Application.Recuentos;

/// <summary>Los desenlaces de negocio del recuento que no vienen de un puerto.</summary>
/// <remarks>
/// <b>Con el prefijo <c>recuento-</c></b> (ADR-0055 §13), como el ajuste y la transferencia llevan
/// el suyo: el mismo almacén bloqueado se dice distinto en cada documento, porque el frontal lo
/// explica distinto.
/// </remarks>
internal static class ErroresDeRecuento
{
    internal const string CodigoNoEncontrado = "recuento-no-encontrado";
    internal const string CodigoLineaNoEncontrada = "recuento-linea-no-encontrada";
    internal const string CodigoMotivoNoValido = "recuento-motivo-no-valido";
    internal const string CodigoYaHayUnoEnCurso = "recuento-ya-hay-uno-en-curso";
    internal const string CodigoNoEstaEnCurso = "recuento-no-esta-en-curso";
    internal const string CodigoContadoNoValido = "recuento-contado-no-valido";
    internal const string CodigoCosteNoValido = "recuento-coste-no-valido";
    internal const string CodigoLoteNoValido = "recuento-lote-no-valido";
    internal const string CodigoNumeroDeSerieNoValido = "recuento-numero-de-serie-no-valido";
    internal const string CodigoTrazabilidadNoCasa = "recuento-trazabilidad-no-casa";
    internal const string CodigoClaveRepetida = "recuento-clave-repetida";
    internal const string CodigoSerieRepetida = "recuento-serie-repetida";
    internal const string CodigoConLineasSinContar = "recuento-con-lineas-sin-contar";
    internal const string CodigoTeoricoCambiado = "recuento-teorico-cambiado";
    internal const string CodigoSubeConTransito = "recuento-sube-con-transito";
    internal const string CodigoSinEjercicio = "recuento-sin-ejercicio";
    internal const string CodigoEnEjercicioCerrado = "recuento-en-ejercicio-cerrado";

    /// <summary>
    /// Cuántas líneas lleva como mucho el estado actual de un conflicto (ADR-0055 §2, §5 y §7). El
    /// total va siempre, y la pantalla pide el resto filtrando sus líneas.
    /// </summary>
    internal const int LineasEnElConflicto = 50;

    /// <summary>
    /// Lo que cabe en el coste de una línea: <c>numeric(18,4)</c> deja catorce cifras enteras.
    /// </summary>
    internal const decimal TopeDelCoste = 100_000_000_000_000m;

    internal static ErrorDeOperacion NoEncontrado(Guid recuentoId) => ErrorDeOperacion.NoEncontrado(
        CodigoNoEncontrado,
        $"No hay ningún recuento con el identificador {recuentoId}.");

    /// <summary>La línea no es de ese recuento, o no existe.</summary>
    /// <remarks>
    /// <b>Las dos cosas contestan lo mismo</b>: una línea de otro recuento, por la ruta de este, no
    /// existe. Distinguirlas diría que la línea está en otro sitio.
    /// </remarks>
    /// <param name="recuentoId">El recuento de la ruta.</param>
    /// <param name="lineaId">La línea que se buscó en él.</param>
    /// <returns>El error.</returns>
    internal static ErrorDeOperacion LineaNoEncontrada(Guid recuentoId, Guid lineaId) =>
        ErrorDeOperacion.NoEncontrado(
            CodigoLineaNoEncontrada,
            $"El recuento {recuentoId} no tiene ninguna línea con el identificador {lineaId}.");

    /// <summary>El motivo del alta, que es el que llevará su ajuste.</summary>
    /// <remarks>
    /// <b>Se comprueba aquí aunque el dominio también lo compruebe</b>, como en el ajuste: ahí es una
    /// invariante y se lanza, y aquí es un cuerpo mal escrito, que es un <c>400</c> con su código.
    /// </remarks>
    /// <returns>El error.</returns>
    internal static ErrorDeOperacion MotivoNoValido() => ErrorDeOperacion.Validacion(
        CodigoMotivoNoValido,
        $"El motivo no puede estar vacío ni pasar de {Recuento.LargoDelMotivo} caracteres: es el que " +
        "llevará el ajuste de la diferencia, y lo único que queda para entenderla dentro de dos años.");

    /// <summary>El almacén ya tiene un recuento en curso (ADR-0055 §1.7).</summary>
    /// <remarks>
    /// <para>
    /// <b>Lo dan dos caminos, y tienen que dar el mismo cuerpo</b>, como el GTIN repetido del 2.10. La
    /// comprobación previa del alta lo devuelve sin llegar al motor, y el borde lo contesta cuando el
    /// índice único parcial para a quien pierde la carrera de dos altas a la vez. El índice solo trae
    /// su nombre, así que el error no lleva el almacén: si lo llevara, la respuesta diría por qué
    /// camino se llegó.
    /// </para>
    /// <para>
    /// <b>Por eso Infrastructure ve esta clase</b>: la declaración de la restricción vive en
    /// <c>ModuloDeInventario</c>, y una sola fábrica es un solo texto.
    /// </para>
    /// </remarks>
    /// <returns>El error.</returns>
    internal static ErrorDeOperacion YaHayUnoEnCurso() => ErrorDeOperacion.Conflicto(
        CodigoYaHayUnoEnCurso,
        "Ese almacén ya tiene un recuento en curso, y dos a la vez se pisarían: cada uno movería la " +
        "diferencia contra el teórico que deja el otro. Confírmelo o descártelo antes de abrir otro.");

    /// <summary>
    /// Se escribe en las líneas de un recuento que ya no está en curso (ADR-0055 §4), o se confirma
    /// (§3).
    /// </summary>
    /// <remarks>
    /// <b>Un <c>409</c> con el estado en el mensaje</b>: la petición está bien escrita, y lo que falla
    /// es que el recuento ya se confirmó, se anuló o se descartó. Lo que se contó entonces es lo que
    /// quedó, y no se toca.
    /// </remarks>
    /// <param name="recuentoId">El recuento.</param>
    /// <param name="estado">En el que está.</param>
    /// <returns>El error.</returns>
    internal static ErrorDeOperacion NoEstaEnCurso(Guid recuentoId, string estado) =>
        ErrorDeOperacion.Conflicto(
            CodigoNoEstaEnCurso,
            $"El recuento {recuentoId} está en estado «{estado}»: solo se cuenta, se añade o se quita " +
            "una línea, y se confirma, mientras está en curso. Lo que se contó en uno cerrado es lo que " +
            "quedó.");

    /// <summary>Se confirma con líneas sin contar (ADR-0055 §5).</summary>
    /// <remarks>
    /// <b>Un <c>422</c> y no un <c>409</c></b>: no ha cambiado nada que quien confirma no supiera, y
    /// repetir la petición no lo arregla. Falta trabajo: contar esas líneas, o quitarlas a propósito.
    /// Las primeras van en <c>actual</c>, con el total.
    /// </remarks>
    /// <param name="sinContar">Cuántas faltan.</param>
    /// <returns>El error.</returns>
    internal static ErrorDeOperacion ConLineasSinContar(int sinContar) => ErrorDeOperacion.ReglaDeNegocio(
        CodigoConLineasSinContar,
        $"Al recuento le faltan {sinContar} líneas por contar, y una línea sin contar no es un cero. " +
        "Cuéntelas, o quite las que no se vayan a contar: su clave quedará como está (ADR-0055 §5).");

    /// <summary>El teórico no es el que vio quien confirma (ADR-0055 §2).</summary>
    /// <remarks>
    /// <b>Un <c>409</c> con el estado de ahora en <c>actual</c></b>: la huella de ahora y las líneas cuyo
    /// teórico ya no es el de cuando se contaron. Algo ha movido el almacén desde que se leyó la ficha,
    /// y quien confirma tiene que verlo antes de dar lo contado por bueno.
    /// </remarks>
    /// <returns>El error.</returns>
    internal static ErrorDeOperacion TeoricoCambiado() => ErrorDeOperacion.Conflicto(
        CodigoTeoricoCambiado,
        "El teórico del recuento ya no es el que se vio al leerlo: algo ha movido el almacén desde " +
        "entonces. Revise las líneas marcadas, vuelva a contarlas si hace falta y confirme con la " +
        "huella de ahora (ADR-0055 §2).");

    /// <summary>Una línea sube en una clave con tránsito hacia ella (ADR-0055 §7).</summary>
    /// <remarks>
    /// <b>Un <c>409</c> con esas líneas en <c>actual</c></b>: si la mercancía en vuelo ya llegó y no se
    /// ha recibido, contarla y después recibirla la sumaría dos veces. Se confirma después de recibir.
    /// </remarks>
    /// <returns>El error.</returns>
    internal static ErrorDeOperacion SubeConTransito() => ErrorDeOperacion.Conflicto(
        CodigoSubeConTransito,
        "Hay líneas contadas por encima del teórico en claves con mercancía en tránsito hacia ellas: " +
        "si ya llegó y no se ha recibido, se sumaría dos veces. Reciba las transferencias y vuelva a " +
        "confirmar (ADR-0055 §7).");

    /// <summary>Hoy no cae en ningún ejercicio, y el recuento se confirma con la fecha de hoy.</summary>
    /// <param name="fecha">La fecha de la confirmación.</param>
    /// <returns>El error.</returns>
    internal static ErrorDeOperacion SinEjercicio(DateOnly fecha) => ErrorDeOperacion.Conflicto(
        CodigoSinEjercicio,
        $"El {fecha:yyyy-MM-dd} no cae en ningún ejercicio de esta empresa, y el recuento y su ajuste " +
        "llevan la fecha de la confirmación (ADR-0055 §1.5). Abra el ejercicio que falta antes de " +
        "confirmar.");

    /// <summary>El ejercicio de hoy está cerrado (R9).</summary>
    /// <param name="fecha">La fecha de la confirmación.</param>
    /// <returns>El error.</returns>
    internal static ErrorDeOperacion EnEjercicioCerrado(DateOnly fecha) => ErrorDeOperacion.Conflicto(
        CodigoEnEjercicioCerrado,
        $"El ejercicio del {fecha:yyyy-MM-dd} está cerrado, y el recuento y su ajuste llevan la fecha " +
        "de la confirmación (ADR-0055 §1.5): ese periodo ya es definitivo y no admite documentos " +
        "nuevos (R9).");

    /// <summary>Lo contado no cabe en la línea (ADR-0055 §6).</summary>
    /// <remarks>
    /// <b>Es la regla del dominio dicha antes</b>, con su <c>type</c>: ahí es una invariante y se
    /// lanza, y aquí es un cuerpo mal escrito. No se devuelve el valor recibido.
    /// </remarks>
    /// <returns>El error.</returns>
    internal static ErrorDeOperacion ContadoNoValido() => ErrorDeOperacion.Validacion(
        CodigoContadoNoValido,
        "Lo contado va en la unidad base del artículo: no es negativo, lleva seis decimales como " +
        "mucho, tiene menos de trece cifras enteras y, en una línea con número de serie, es cero o " +
        "uno, porque un número de serie es una unidad (ADR-0055 §6).");

    /// <summary>El coste de una clave añadida no es un coste (ADR-0055 §6).</summary>
    /// <remarks>
    /// <b>Negativo no existe</b>: una muestra o un regalo entran a cero, como en el ajuste. Y uno que
    /// no cabe en la columna daría un <c>500</c> al guardar en vez de este <c>400</c>.
    /// </remarks>
    /// <returns>El error.</returns>
    internal static ErrorDeOperacion CosteNoValido() => ErrorDeOperacion.Validacion(
        CodigoCosteNoValido,
        "El coste de una unidad base no es negativo, porque una muestra o un regalo entran a cero, " +
        "y tiene menos de quince cifras enteras (ADR-0055 §6).");

    /// <summary>El lote de la clave añadida no es un código GS1 (ADR-0048 §2).</summary>
    /// <returns>El error.</returns>
    internal static ErrorDeOperacion LoteNoValido() => ErrorDeOperacion.Validacion(
        CodigoLoteNoValido,
        $"El lote no es un código GS1: de 1 a {CodigoGs1.LargoMaximo} caracteres del conjunto 82, " +
        "sin espacios dentro, que es lo que cabe en la etiqueta (ADR-0048 §2).");

    /// <summary>El número de serie de la clave añadida no es un código GS1 (ADR-0048 §2).</summary>
    /// <returns>El error.</returns>
    internal static ErrorDeOperacion NumeroDeSerieNoValido() => ErrorDeOperacion.Validacion(
        CodigoNumeroDeSerieNoValido,
        $"El número de serie no es un código GS1: de 1 a {CodigoGs1.LargoMaximo} caracteres del " +
        "conjunto 82, sin espacios dentro, que es lo que cabe en la etiqueta (ADR-0048 §2).");

    /// <summary>La clave añadida no casa con la marca de su artículo (ADR-0055 §6, ADR-0048 §4).</summary>
    /// <remarks>
    /// <b>Un <c>409</c> y no un <c>400</c></b>, como en el alta del ajuste: el cuerpo puede estar bien
    /// escrito, y lo que falla es la ficha del artículo. Es la cortesía: la guarda es la de confirmar,
    /// que lee la marca con la fila bloqueada.
    /// </remarks>
    /// <param name="noCasa">El artículo, su marca y lo que le falta o le sobra a la clave.</param>
    /// <returns>El error.</returns>
    internal static ErrorDeOperacion TrazabilidadNoCasa(LineaQueNoCasa noCasa) =>
        ErrorDeOperacion.Conflicto(
            CodigoTrazabilidadNoCasa,
            $"La clave no casa con la marca del artículo {noCasa.ArticuloId}, que es «{noCasa.Marca}»: " +
            $"{LaTrazabilidadDeLasLineas.EnPalabras(noCasa.Discrepancia)} Corrija la clave (ADR-0048 §4).");

    /// <summary>El recuento ya lleva esa clave (ADR-0055 §13).</summary>
    /// <remarks>
    /// <b>Lo dan dos caminos, y tienen que dar el mismo cuerpo</b>, como el de uno en curso por
    /// almacén. El caso de uso lo mira con la cabecera bloqueada, y el índice
    /// <c>ix_lineas_recuento_una_por_clave</c> es la red de debajo: el borde lo traduce si alguna
    /// escritura llegara a cruzarse sin el cerrojo. El índice solo trae su nombre, así que el error
    /// no lleva la clave.
    /// </remarks>
    /// <returns>El error.</returns>
    internal static ErrorDeOperacion ClaveRepetida() => ErrorDeOperacion.Conflicto(
        CodigoClaveRepetida,
        "El recuento ya lleva esa clave: la misma ubicación, el mismo artículo y el mismo lote o " +
        "número de serie. Se contaría dos veces; cuéntela en la línea que ya tiene.");

    /// <summary>El recuento ya lleva ese número de serie, en otra ubicación (ADR-0048 §3).</summary>
    /// <remarks>
    /// <b>Por los mismos dos caminos que la clave repetida</b>, con el índice
    /// <c>ix_lineas_recuento_una_por_serie</c> debajo. Una serie va una sola vez por recuento porque va
    /// una sola vez por ajuste: en dos ubicaciones, el ajuste la sacaría de una y la metería en otra, y
    /// eso es la reubicación.
    /// </remarks>
    /// <returns>El error.</returns>
    internal static ErrorDeOperacion SerieRepetida() => ErrorDeOperacion.Conflicto(
        CodigoSerieRepetida,
        "El recuento ya lleva ese número de serie de ese artículo, en otra ubicación: una unidad está " +
        "en un solo sitio. Cuéntela en la línea que ya la tiene, o quite esa línea si no está ahí " +
        "(ADR-0048 §3).");
}
