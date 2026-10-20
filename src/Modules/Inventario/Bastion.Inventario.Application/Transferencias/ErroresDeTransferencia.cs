using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Inventario.Application.Lineas;
using Bastion.Inventario.Application.Trazabilidad;
using Bastion.Inventario.Contracts.Lineas;
using Bastion.Inventario.Contracts.Transferencias;
using Bastion.Inventario.Domain.LotesYSeries;
using Bastion.Inventario.Domain.Movimientos;
using Bastion.Inventario.Domain.Reservas;
using Bastion.Inventario.Domain.Transferencias;
using Bastion.Inventario.Domain.Valoraciones;
using Bastion.Organizacion.Contracts.Unidades;

namespace Bastion.Inventario.Application.Transferencias;

/// <summary>Los desenlaces de negocio de la transferencia que no vienen de un puerto.</summary>
/// <remarks>
/// <b>Los mismos que los del ajuste, con su prefijo, y cuatro propios</b>: el mismo almacén en las
/// dos puntas, la recepción antes del envío, lo que no está enviado y el envío que se llevaría lo
/// reservado (ADR-0059 §9). Cada documento lleva los suyos
/// porque el <c>type</c> dice qué documento falló, y el frontal los traduce por separado (ADR-0053
/// §12).
/// </remarks>
internal static class ErroresDeTransferencia
{
    internal const string CodigoNoEncontrada = "transferencia-no-encontrada";
    internal const string CodigoSinLineas = "transferencia-sin-lineas";
    internal const string CodigoMismoAlmacen = "transferencia-mismo-almacen";
    internal const string CodigoCantidadNoValida = "transferencia-cantidad-no-valida";
    internal const string CodigoNoEstaEnBorrador = "transferencia-no-esta-en-borrador";
    internal const string CodigoNoEstaEnviada = "transferencia-no-esta-enviada";
    internal const string CodigoNoSeAnula = "transferencia-no-se-anula";
    internal const string CodigoMotivoNoValido = "transferencia-motivo-no-valido";
    internal const string CodigoSinEjercicio = "transferencia-sin-ejercicio";
    internal const string CodigoEnEjercicioCerrado = "transferencia-en-ejercicio-cerrado";
    internal const string CodigoConFechaFutura = "transferencia-con-fecha-futura";
    internal const string CodigoRecepcionAntesDelEnvio = "transferencia-recepcion-antes-del-envio";
    internal const string CodigoValoracionEnOtraDivisa = "transferencia-valoracion-en-otra-divisa";
    internal const string CodigoFechaAnteriorAlUltimoMovimiento = "transferencia-fecha-anterior-al-ultimo-movimiento";
    internal const string CodigoLoteNoValido = "transferencia-lote-no-valido";
    internal const string CodigoNumeroDeSerieNoValido = "transferencia-numero-de-serie-no-valido";
    internal const string CodigoSerieNoUnitaria = "transferencia-serie-no-unitaria";
    internal const string CodigoSerieRepetida = "transferencia-serie-repetida";
    internal const string CodigoTrazabilidadNoCasa = "transferencia-trazabilidad-no-casa";
    internal const string CodigoPorEncimaDelDisponible = "transferencia-por-encima-del-disponible";
    internal const string CodigoSinConversion = "transferencia-sin-conversion";
    internal const string CodigoConversionRetirada = "transferencia-conversion-retirada";

    internal static ErrorDeOperacion NoEncontrada(Guid transferenciaId) => ErrorDeOperacion.NoEncontrado(
        CodigoNoEncontrada,
        $"No hay ninguna transferencia con el identificador {transferenciaId}.");

    /// <summary>La vuelta de la R13, dicha antes de que exista el documento, como en el ajuste.</summary>
    /// <returns>El error.</returns>
    internal static ErrorDeOperacion SinLineas() => ErrorDeOperacion.Validacion(
        CodigoSinLineas,
        "La transferencia no tiene ninguna línea: un documento que no mueve el libro no lleva nada " +
        "de un almacén a otro (R13).");

    /// <summary>El origen y el destino son el mismo almacén (ADR-0053 §8).</summary>
    /// <returns>El error.</returns>
    internal static ErrorDeOperacion MismoAlmacen() => ErrorDeOperacion.Validacion(
        CodigoMismoAlmacen,
        "El origen y el destino son el mismo almacén: eso no es una transferencia, y mover la " +
        "mercancía de hueco dentro de un almacén es la reubicación (ADR-0053 §8).");

    /// <summary>La ruta de un campo de una línea de la transferencia (ADR-0068 §6).</summary>
    /// <param name="linea">La línea, desde uno, como la dicen los mensajes.</param>
    /// <param name="propiedad">La propiedad de <see cref="LineaDeTransferenciaDto"/>, por su <c>nameof</c>.</param>
    /// <param name="motivo">Lo que se dice del campo.</param>
    /// <returns>Un solo campo, con su motivo.</returns>
    internal static IReadOnlyDictionary<string, IReadOnlyList<string>> ElCampoDeLaLinea(
        int linea, string propiedad, string motivo) =>
        LosCamposDeLasLineas.ElCampo(nameof(AbrirTransferenciaDto.Lineas), linea, propiedad, motivo);

    /// <summary>Una línea que no lleva al destino una cantidad que quepa en el libro.</summary>
    /// <remarks>
    /// <para>
    /// <b>Un <c>400</c> y no el <c>500</c> que daba</b> hasta el ítem 2.19 lo que no cabía en su
    /// columna, <c>numeric(18,6)</c>: el dominio solo miraba el cero (ADR-0068 §4). Se mira dos veces,
    /// como la forma: lo escrito, antes de preguntar a nadie, y lo que mueve en la unidad base, con el
    /// factor puesto.
    /// </para>
    /// <para>
    /// <b>El campo es la cantidad</b> también cuando lo que no cabe es la cantidad base, porque el
    /// factor no lo escribe nadie (ADR-0068 §2): lo que se corrige es la cantidad, o la unidad.
    /// </para>
    /// </remarks>
    /// <param name="linea">La línea, desde uno.</param>
    /// <returns>El error.</returns>
    internal static ErrorDeOperacion CantidadNoValida(int linea) => ErrorDeOperacion.Validacion(
        CodigoCantidadNoValida,
        $"La línea {linea} no lleva al destino una cantidad que quepa en el libro: es positiva, lleva " +
        $"{MovimientoStock.DecimalesDeCantidad} decimales como mucho y doce cifras enteras como mucho, y " +
        "en la unidad base de su artículo tampoco se queda en cero ni pasa de doce cifras enteras. La " +
        "cantidad negativa es la del inverso, que se construye anulando.",
        ElCampoDeLaLinea(
            linea,
            nameof(LineaDeTransferenciaDto.CantidadIntroducida),
            $"Positiva, con {MovimientoStock.DecimalesDeCantidad} decimales y doce cifras enteras como " +
            "mucho, también en la unidad base."));

    internal static ErrorDeOperacion NoEstaEnBorrador(Guid transferenciaId, EstadoDeTransferencia estado) =>
        ErrorDeOperacion.Conflicto(
            CodigoNoEstaEnBorrador,
            $"La transferencia {transferenciaId} está en estado «{estado}»: solo un borrador se envía " +
            "o se descarta, y lo enviado ya tiene su salida en el libro, que se corrige con su " +
            "inverso (R2, R3).");

    internal static ErrorDeOperacion NoEstaEnviada(Guid transferenciaId, EstadoDeTransferencia estado) =>
        ErrorDeOperacion.Conflicto(
            CodigoNoEstaEnviada,
            $"La transferencia {transferenciaId} está en estado «{estado}»: solo se recibe lo que se " +
            "envió y sigue en vuelo. Un borrador no ha salido, y lo recibido o anulado ya no vuela.");

    /// <summary>Lo que no se anula: un borrador, una ya anulada o un inverso (R2).</summary>
    /// <param name="transferenciaId">El documento.</param>
    /// <param name="estado">Su estado.</param>
    /// <param name="esUnInverso">Si es el inverso de otra.</param>
    /// <returns>El error.</returns>
    internal static ErrorDeOperacion NoSeAnula(
        Guid transferenciaId, EstadoDeTransferencia estado, bool esUnInverso) =>
        ErrorDeOperacion.Conflicto(
            CodigoNoSeAnula,
            esUnInverso
                ? $"La transferencia {transferenciaId} es el inverso de otra: deshacerlo sería volver a " +
                  "hacer la transferencia, y eso es otra transferencia (R2)."
                : $"La transferencia {transferenciaId} está en estado «{estado}»: solo se anula lo " +
                  "enviado o lo recibido. Un borrador no ha movido nada, y una anulada ya tiene su " +
                  "inverso.");

    /// <summary>El motivo de la anulación, que es lo único que trae su petición.</summary>
    /// <remarks>
    /// Por lo mismo que en el ajuste: el dominio lanza, y el borde necesita un <c>400</c> con su
    /// código. No se devuelve lo que llegó.
    /// </remarks>
    /// <returns>El error.</returns>
    internal static ErrorDeOperacion MotivoNoValido() => ErrorDeOperacion.Validacion(
        CodigoMotivoNoValido,
        "El motivo de la anulación no puede estar vacío ni pasar de " +
        $"{Transferencia.LargoDelMotivo} caracteres: es lo único que queda para entender la " +
        "corrección dentro de dos años.");

    /// <summary>La fecha no cae en ningún ejercicio de la empresa (R9).</summary>
    /// <param name="fecha">La del envío, la de la recepción o la de hoy, según la acción.</param>
    /// <returns>El error.</returns>
    internal static ErrorDeOperacion SinEjercicio(DateOnly fecha) => ErrorDeOperacion.Conflicto(
        CodigoSinEjercicio,
        $"La fecha {fecha:yyyy-MM-dd} no cae dentro de ningún ejercicio de esta empresa, así que el " +
        "movimiento no podría imputarse a ninguna autoliquidación. Abra el ejercicio que falta o " +
        "corrija la fecha (R9).");

    /// <summary>La fecha cae en un ejercicio ya cerrado (R9).</summary>
    /// <param name="fecha">La del envío, la de la recepción o la de hoy, según la acción.</param>
    /// <returns>El error.</returns>
    internal static ErrorDeOperacion EnEjercicioCerrado(DateOnly fecha) => ErrorDeOperacion.Conflicto(
        CodigoEnEjercicioCerrado,
        $"El ejercicio al que cae la fecha {fecha:yyyy-MM-dd} está cerrado: ese periodo ya es " +
        "definitivo y no admite movimientos nuevos. Reabra el ejercicio, con su motivo, o lleve el " +
        "movimiento a una fecha del ejercicio abierto (R9).");

    /// <summary>La fecha del envío o de la recepción es posterior a hoy (ADR-0053 §3).</summary>
    /// <remarks>
    /// <b>Un <c>422</c>, y no el <c>409</c> del ajuste</b>, a propósito: lo fijó la tabla del §3. La
    /// fecha es un dato de la mercancía, no un estado del documento que otra petición pueda cambiar.
    /// </remarks>
    /// <param name="fecha">La fecha que se quería escribir.</param>
    /// <returns>El error.</returns>
    internal static ErrorDeOperacion ConFechaFutura(DateOnly fecha) => ErrorDeOperacion.ReglaDeNegocio(
        CodigoConFechaFutura,
        $"La fecha {fecha:yyyy-MM-dd} es posterior a hoy: el libro dice lo que ha pasado, y la " +
        "existencia es su suma, así que un movimiento futuro la haría contar algo que todavía no ha " +
        "ocurrido. Hágalo ese día, o corrija la fecha (R3).");

    /// <summary>La recepción lleva una fecha anterior a la del envío (ADR-0053 §3).</summary>
    /// <param name="recepcion">La fecha de la recepción.</param>
    /// <param name="envio">La del envío.</param>
    /// <returns>El error.</returns>
    internal static ErrorDeOperacion RecepcionAntesDelEnvio(DateOnly recepcion, DateOnly envio) =>
        ErrorDeOperacion.ReglaDeNegocio(
            CodigoRecepcionAntesDelEnvio,
            $"La recepción del {recepcion:yyyy-MM-dd} va antes que el envío, del {envio:yyyy-MM-dd}: " +
            "la mercancía no llega antes de salir (ADR-0053 §3).");

    /// <summary>La valoración de una de las dos puntas está en otra divisa (ADR-0046 §7).</summary>
    /// <remarks>
    /// <b>Se pregunta también por la clave del destino al enviar</b>, aunque el envío no la valore:
    /// lo que vuela se suma a ella, y sumarle importes de otra divisa daría un valor en tránsito que
    /// no se puede comparar con nada.
    /// </remarks>
    /// <param name="clave">La clave que no está en la divisa del documento.</param>
    /// <param name="divisa">La divisa del documento.</param>
    /// <returns>El error.</returns>
    internal static ErrorDeOperacion ValoracionEnOtraDivisa(ClaveDeValoracion clave, string divisa) =>
        ErrorDeOperacion.ReglaDeNegocio(
            CodigoValoracionEnOtraDivisa,
            $"Las existencias del artículo {clave.ArticuloId} en el almacén {clave.AlmacenId} están " +
            $"valoradas en otra divisa que la del documento, {divisa}, y sumarlas exigiría un tipo de " +
            "cambio (ADR-0046 §7).");

    /// <summary>
    /// El envío saca de una clave del origen más de lo que no está reservado (ADR-0059 §9).
    /// </summary>
    /// <remarks>
    /// <b>Solo cuando no pasa del físico</b>: lo que pasa del físico es el <c>stock-insuficiente</c>
    /// del hueco, que dice que no hay, y no que está apartado.
    /// </remarks>
    /// <param name="pasada">La clave que pasa, con lo que saca y su disponible.</param>
    /// <returns>El error.</returns>
    internal static ErrorDeOperacion PorEncimaDelDisponible(SalidaPorEncimaDelDisponible pasada) =>
        ErrorDeOperacion.ReglaDeNegocio(
            CodigoPorEncimaDelDisponible,
            $"La transferencia saca {pasada.Sale} del artículo {pasada.Clave.ArticuloId} del almacén " +
            $"{pasada.Clave.AlmacenId}, y el disponible es {pasada.Disponible}: el resto está reservado " +
            "para otras líneas, y una transferencia no se lleva lo apartado (ADR-0059 §9).");

    /// <summary>Lo que impide valorar una de las patas, con su código (ADR-0046 §10).</summary>
    /// <remarks>
    /// <b>Una entrada sin coste no puede pasar aquí</b>: toda pata que entra en una transferencia
    /// lleva el valor que compensa, el que viajó, así que no necesita el precio medio de nadie.
    /// Si llega, es que una línea perdió su valor por el camino, y eso es un error del código.
    /// </remarks>
    /// <param name="impedimento">Lo que contestó <c>LoQueImpide</c>.</param>
    /// <param name="divisa">La divisa del documento.</param>
    /// <returns>El error.</returns>
    internal static ErrorDeOperacion NoSeValora(ImpedimentoDeValoracion impedimento, string divisa) =>
        impedimento.Motivo switch
        {
            MotivoDelImpedimento.ValoracionEnOtraDivisa => ValoracionEnOtraDivisa(impedimento.Clave, divisa),
            MotivoDelImpedimento.FechaAnteriorAlUltimoMovimiento => ErrorDeOperacion.ReglaDeNegocio(
                CodigoFechaAnteriorAlUltimoMovimiento,
                $"El artículo {impedimento.Clave.ArticuloId} se movió por última vez en el almacén " +
                $"{impedimento.Clave.AlmacenId} el {impedimento.UltimaFecha:yyyy-MM-dd}, y la " +
                "transferencia lleva una fecha anterior: sumar el libro hasta los días de en medio " +
                "daría un estado que no existió. Póngale esa fecha o una posterior (ADR-0047)."),
            _ => throw new ArgumentOutOfRangeException(
                nameof(impedimento),
                impedimento.Motivo,
                "Una pata de la transferencia que entra lleva siempre el valor que viajó, así que no " +
                "puede faltarle coste ni precio medio: esto es un error del código, no del almacén."),
        };

    /// <summary>La línea que no tiene forma, con su código (ADR-0048 §2 y §3).</summary>
    /// <param name="sinForma">Lo que contestó <c>LoQueNoTieneForma</c>.</param>
    /// <returns>El error.</returns>
    internal static ErrorDeOperacion DeLaForma(LineaSinForma sinForma) => sinForma.Falta switch
    {
        FaltaDeForma.LoteNoValido => LoteNoValido(sinForma.Linea),
        FaltaDeForma.NumeroDeSerieNoValido => NumeroDeSerieNoValido(sinForma.Linea),
        FaltaDeForma.SerieNoUnitaria => SerieNoUnitaria(sinForma.Linea),
        FaltaDeForma.SerieRepetida => SerieRepetida(sinForma.Linea, sinForma.Serie!),
        _ => throw new ArgumentOutOfRangeException(nameof(sinForma), sinForma.Falta, null),
    };

    /// <summary>El lote de una línea no es un código GS1 (ADR-0048 §2).</summary>
    /// <param name="linea">La línea, desde uno.</param>
    /// <returns>El error.</returns>
    internal static ErrorDeOperacion LoteNoValido(int linea) => ErrorDeOperacion.Validacion(
        CodigoLoteNoValido,
        $"El lote de la línea {linea} no es un código GS1: de 1 a {CodigoGs1.LargoMaximo} caracteres " +
        "del conjunto 82, sin espacios dentro, que es lo que cabe en la etiqueta (ADR-0048 §2).",
        ElCampoDeLaLinea(
            linea,
            nameof(LineaDeTransferenciaDto.CodigoDeLote),
            $"No es un código GS1: de 1 a {CodigoGs1.LargoMaximo} caracteres del conjunto 82."));

    /// <summary>El número de serie de una línea no es un código GS1 (ADR-0048 §2).</summary>
    /// <param name="linea">La línea, desde uno.</param>
    /// <returns>El error.</returns>
    internal static ErrorDeOperacion NumeroDeSerieNoValido(int linea) => ErrorDeOperacion.Validacion(
        CodigoNumeroDeSerieNoValido,
        $"El número de serie de la línea {linea} no es un código GS1: de 1 a " +
        $"{CodigoGs1.LargoMaximo} caracteres del conjunto 82, sin espacios dentro, que es lo que cabe " +
        "en la etiqueta (ADR-0048 §2).",
        ElCampoDeLaLinea(
            linea,
            nameof(LineaDeTransferenciaDto.NumeroDeSerie),
            $"No es un código GS1: de 1 a {CodigoGs1.LargoMaximo} caracteres del conjunto 82."));

    /// <summary>Una línea con número de serie que no mueve una unidad base (ADR-0048 §3).</summary>
    /// <remarks>
    /// <b>El campo es la cantidad</b>, que es lo que se corrige. Se mira con el factor ya resuelto, así
    /// que una caja de dos con número de serie también sale aquí (ADR-0068 §2).
    /// </remarks>
    /// <param name="linea">La línea, desde uno.</param>
    /// <returns>El error.</returns>
    internal static ErrorDeOperacion SerieNoUnitaria(int linea) => ErrorDeOperacion.Validacion(
        CodigoSerieNoUnitaria,
        $"La línea {linea} lleva número de serie y no mueve una unidad base: un número de serie es una " +
        "unidad (ADR-0048 §3).",
        ElCampoDeLaLinea(
            linea,
            nameof(LineaDeTransferenciaDto.CantidadIntroducida),
            "Con número de serie, la línea mueve una unidad base."));

    /// <summary>El mismo número de serie en dos líneas del documento (ADR-0048 §3).</summary>
    /// <param name="linea">La segunda línea que la nombra, desde uno.</param>
    /// <param name="serie">El número, ya normalizado.</param>
    /// <returns>El error.</returns>
    internal static ErrorDeOperacion SerieRepetida(int linea, string serie) => ErrorDeOperacion.Validacion(
        CodigoSerieRepetida,
        $"La línea {linea} repite el número de serie «{serie}» de otra línea del mismo artículo: una " +
        "serie sale una sola vez por documento (ADR-0048 §3).",
        ElCampoDeLaLinea(
            linea,
            nameof(LineaDeTransferenciaDto.NumeroDeSerie),
            "Otra línea del mismo artículo ya lleva este número de serie."));

    /// <summary>Una línea que no casa con la marca de su artículo (ADR-0048 §4).</summary>
    /// <remarks>
    /// <b>Un <c>409</c></b>, como en el ajuste: al abrir es la cortesía, y al enviar, la guarda, con
    /// la marca leída bajo cerrojo. Al recibir y al anular no se pregunta: el artículo ya se movió
    /// al enviar, así que su marca no puede cambiar. La línea va también en <c>actual</c>, para que
    /// la pantalla ponga el error en su artículo (ADR-0068 §6).
    /// </remarks>
    /// <param name="noCasa">La línea, su artículo, su marca y lo que le falta o le sobra.</param>
    /// <returns>El error.</returns>
    internal static ErrorDeOperacion TrazabilidadNoCasa(LineaQueNoCasa noCasa) =>
        ErrorDeOperacion.Conflicto(
            CodigoTrazabilidadNoCasa,
            $"La línea {noCasa.Linea} no casa con la marca del artículo {noCasa.ArticuloId}, que es " +
            $"«{noCasa.Marca}»: {LaTrazabilidadDeLasLineas.EnPalabras(noCasa.Discrepancia)} Corrija la " +
            "línea, o la marca si el artículo todavía no se ha movido (ADR-0048 §4).")
        .ConElEstadoActual(new LineaQueFallaDto(noCasa.Linea));

    /// <summary>
    /// La unidad de una línea no tiene conversión a la base de su artículo (ADR-0068 §2).
    /// </summary>
    /// <remarks>
    /// Un <c>422</c>, como en el ajuste: el cuerpo está bien escrito, y lo que falta es un maestro que
    /// se puede dar de alta. No se invierte ni se encadena otra conversión (ADR-0023).
    /// </remarks>
    /// <param name="linea">La primera línea que la necesita, desde uno.</param>
    /// <param name="par">De la unidad introducida a la base.</param>
    /// <returns>El error, con las dos unidades en <c>actual</c>.</returns>
    internal static ErrorDeOperacion SinConversion(int linea, ParDeUnidades par) =>
        ErrorDeOperacion.ReglaDeNegocio(
            CodigoSinConversion,
            $"La línea {linea} está escrita en la unidad {par.OrigenId}, y no hay ninguna conversión " +
            $"de esa unidad a {par.DestinoId}, que es la base de su artículo: sin ella no se sabe " +
            "cuántas unidades base lleva. Dé de alta la conversión, o escriba la línea en la unidad " +
            "base (ADR-0068 §2).")
        .ConElEstadoActual(new ConversionDeLaLineaDto(linea, par.OrigenId, par.DestinoId));

    /// <summary>
    /// La conversión de una línea a la base de su artículo está retirada (ADR-0068 §2 y §3).
    /// </summary>
    /// <remarks>
    /// Un <c>409</c>, como la unidad retirada. Solo lo pregunta el alta: enviar usa el factor que la
    /// línea guardó, y el inverso lo copia.
    /// </remarks>
    /// <param name="linea">La primera línea que la usa, desde uno.</param>
    /// <param name="par">De la unidad introducida a la base.</param>
    /// <returns>El error, con las dos unidades en <c>actual</c>.</returns>
    internal static ErrorDeOperacion ConversionRetirada(int linea, ParDeUnidades par) =>
        ErrorDeOperacion.Conflicto(
            CodigoConversionRetirada,
            $"La línea {linea} está escrita en la unidad {par.OrigenId}, y su conversión a " +
            $"{par.DestinoId}, la base de su artículo, está retirada: explica lo que ya está escrito " +
            "con ella, pero no se escribe nada nuevo (ADR-0068 §3).")
        .ConElEstadoActual(new ConversionDeLaLineaDto(linea, par.OrigenId, par.DestinoId));
}
