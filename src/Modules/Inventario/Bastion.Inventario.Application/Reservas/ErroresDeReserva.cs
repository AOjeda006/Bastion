using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Inventario.Application.Trazabilidad;
using Bastion.Inventario.Domain.LotesYSeries;
using Bastion.Inventario.Domain.Movimientos;
using Bastion.Inventario.Domain.Reservas;
using Bastion.Inventario.Domain.Valoraciones;

namespace Bastion.Inventario.Application.Reservas;

/// <summary>Los desenlaces de negocio de la reserva que no vienen de un puerto.</summary>
/// <remarks>
/// <para>
/// <b>Todos son resultados, ninguno lanza</b> (ADR-0059 §1, precisión 7): quien llama en la fase 4
/// es un manejador de eventos de Ventas, y un rechazo es lo que contesta, no una avería.
/// </para>
/// <para>
/// <b>Los del libro son los del ajuste y la transferencia, con su prefijo</b>: el ejercicio, la
/// fecha futura, la marca, la forma del lote y de la serie, y la valoración. El <c>type</c> dice
/// qué documento falló, y quien lo traduce los separa (ADR-0059 §12).
/// </para>
/// </remarks>
internal static class ErroresDeReserva
{
    internal const string CodigoOrigenNoValido = "reserva-origen-no-valido";
    internal const string CodigoCantidadNoValida = "reserva-cantidad-no-valida";
    internal const string CodigoCaducidadNoValida = "reserva-caducidad-no-valida";
    internal const string CodigoDocumentoNoValido = "reserva-documento-no-valido";
    internal const string CodigoSinLineas = "reserva-sin-lineas";
    internal const string CodigoMotivoNoValido = "reserva-motivo-no-valido";
    internal const string CodigoLoteNoValido = "reserva-lote-no-valido";
    internal const string CodigoNumeroDeSerieNoValido = "reserva-numero-de-serie-no-valido";
    internal const string CodigoSerieNoUnitaria = "reserva-serie-no-unitaria";
    internal const string CodigoSerieRepetida = "reserva-serie-repetida";
    internal const string CodigoNoEncontrada = "reserva-no-encontrada";
    internal const string CodigoOrigenConOtraReserva = "reserva-origen-con-otra-reserva";
    internal const string CodigoCaducada = "reserva-caducada";
    internal const string CodigoNoEstaActiva = "reserva-no-esta-activa";
    internal const string CodigoDocumentoYaLaConsumio = "reserva-documento-ya-la-consumio";
    internal const string CodigoPorEncimaDelDisponible = "reserva-por-encima-del-disponible";
    internal const string CodigoConsumoPorEncimaDeLoPendiente = "reserva-consumo-por-encima-de-lo-pendiente";
    internal const string CodigoConsumoSinStock = "reserva-consumo-sin-stock";
    internal const string CodigoConFechaFutura = "reserva-con-fecha-futura";
    internal const string CodigoSinEjercicio = "reserva-sin-ejercicio";
    internal const string CodigoEnEjercicioCerrado = "reserva-en-ejercicio-cerrado";
    internal const string CodigoTrazabilidadNoCasa = "reserva-trazabilidad-no-casa";
    internal const string CodigoSerieNoEntera = "reserva-serie-no-entera";
    internal const string CodigoValoracionEnOtraDivisa = "reserva-valoracion-en-otra-divisa";
    internal const string CodigoFechaAnteriorAlUltimoMovimiento = "reserva-fecha-anterior-al-ultimo-movimiento";

    /// <summary>El origen no dice qué línea pide: sin él, un reintento no la reconocería.</summary>
    /// <returns>El error.</returns>
    internal static ErrorDeOperacion OrigenNoValido() => ErrorDeOperacion.Validacion(
        CodigoOrigenNoValido,
        "El origen de la reserva no dice qué línea la pide: hace falta un tipo de documento " +
        "conocido, su identificador y una línea contada desde uno. Es lo que reconoce la reserva " +
        "en un reintento (ADR-0059 §5).");

    /// <summary>Una cantidad que no es positiva o que pasa de seis decimales.</summary>
    /// <param name="linea">La línea del consumo, desde uno, o <c>null</c> si es la de la reserva.</param>
    /// <returns>El error.</returns>
    internal static ErrorDeOperacion CantidadNoValida(int? linea) => ErrorDeOperacion.Validacion(
        CodigoCantidadNoValida,
        (linea is { } numero ? $"La línea {numero} no saca" : "La reserva no aparta") +
        $" una cantidad válida: es positiva, en la unidad base, y con {MovimientoStock.DecimalesDeCantidad} " +
        "decimales como mucho, los de la fila del libro.");

    /// <summary>Una caducidad que no es posterior a ahora: la reserva nacería liberada.</summary>
    /// <param name="caducaEl">La caducidad pedida.</param>
    /// <returns>El error.</returns>
    internal static ErrorDeOperacion CaducidadNoValida(DateTimeOffset caducaEl) => ErrorDeOperacion.Validacion(
        CodigoCaducidadNoValida,
        $"La caducidad pedida, {caducaEl:O}, no es posterior a ahora, contada al microsegundo: una " +
        "reserva que caduca antes de nacer nacería liberada.");

    /// <summary>El documento que sale no es un albarán, o no lleva identificador.</summary>
    /// <param name="tipo">La clase de documento que llegó.</param>
    /// <returns>El error.</returns>
    internal static ErrorDeOperacion DocumentoNoValido(TipoDeDocumentoOrigen tipo) => ErrorDeOperacion.Validacion(
        CodigoDocumentoNoValido,
        $"Una reserva la consume un albarán con su identificador, y llegó un documento de tipo «{tipo}»" +
        " o sin identificador: el ajuste y la transferencia no salen contra una reserva, y una " +
        "salida sin documento no se ata a sus filas del libro (ADR-0059 §7 y §8).");

    /// <summary>Un consumo que no saca nada.</summary>
    /// <returns>El error.</returns>
    internal static ErrorDeOperacion SinLineas() => ErrorDeOperacion.Validacion(
        CodigoSinLineas,
        "El consumo no tiene ninguna línea: una salida que no mueve el libro dejaría un consumo sin " +
        "su fila (R13).");

    /// <summary>El motivo de una liberación a mano, vacío o demasiado largo.</summary>
    /// <returns>El error.</returns>
    internal static ErrorDeOperacion MotivoNoValido() => ErrorDeOperacion.Validacion(
        CodigoMotivoNoValido,
        "El motivo de la liberación no puede estar vacío ni pasar de " +
        $"{Reserva.LargoDelMotivo} caracteres: es lo único que explica por qué volvió la mercancía.");

    /// <summary>La línea que no tiene forma, con su código (ADR-0048 §2 y §3).</summary>
    /// <param name="sinForma">Lo que contestó <c>LoQueNoTieneForma</c>.</param>
    /// <returns>El error.</returns>
    internal static ErrorDeOperacion DeLaForma(LineaSinForma sinForma) => sinForma.Falta switch
    {
        FaltaDeForma.LoteNoValido => ErrorDeOperacion.Validacion(
            CodigoLoteNoValido,
            $"El lote de la línea {sinForma.Linea} no es un código GS1: de 1 a {CodigoGs1.LargoMaximo} " +
            "caracteres del conjunto 82, sin espacios dentro (ADR-0048 §2)."),
        FaltaDeForma.NumeroDeSerieNoValido => ErrorDeOperacion.Validacion(
            CodigoNumeroDeSerieNoValido,
            $"El número de serie de la línea {sinForma.Linea} no es un código GS1: de 1 a " +
            $"{CodigoGs1.LargoMaximo} caracteres del conjunto 82, sin espacios dentro (ADR-0048 §2)."),
        FaltaDeForma.SerieNoUnitaria => ErrorDeOperacion.Validacion(
            CodigoSerieNoUnitaria,
            $"La línea {sinForma.Linea} lleva número de serie y no saca una unidad base: un número " +
            "de serie es una unidad (ADR-0048 §3)."),
        FaltaDeForma.SerieRepetida => ErrorDeOperacion.Validacion(
            CodigoSerieRepetida,
            $"La línea {sinForma.Linea} repite el número de serie «{sinForma.Serie}» de otra línea: " +
            "una serie sale una sola vez por documento (ADR-0048 §3)."),
        _ => throw new ArgumentOutOfRangeException(nameof(sinForma), sinForma.Falta, null),
    };

    /// <summary>El origen no tiene reserva, o la tiene en otra empresa, que desde aquí es lo mismo.</summary>
    /// <param name="origen">El origen por el que se preguntó.</param>
    /// <returns>El error.</returns>
    internal static ErrorDeOperacion NoEncontrada(OrigenDeLaReserva origen) => ErrorDeOperacion.NoEncontrado(
        CodigoNoEncontrada,
        $"La línea {origen.Linea} del documento {origen.Id} ({origen.Tipo}) no tiene ninguna reserva.");

    /// <summary>
    /// El origen ya tiene una reserva y la petición pide otra cosa: otro artículo, otro almacén, otra
    /// cantidad u otra caducidad (ADR-0059 §5, precisión 6).
    /// </summary>
    /// <param name="origen">El origen.</param>
    /// <param name="reservaId">La reserva que ya tiene.</param>
    /// <returns>El error.</returns>
    internal static ErrorDeOperacion OrigenConOtraReserva(OrigenDeLaReserva origen, Guid reservaId) =>
        ErrorDeOperacion.Conflicto(
            CodigoOrigenConOtraReserva,
            $"La línea {origen.Linea} del documento {origen.Id} ya tiene la reserva {reservaId}, y " +
            "pedía otra cosa: una línea tiene una reserva como mucho, en cualquier estado. Volver a " +
            "reservarla o cambiar su cantidad es de la fase 4 (ADR-0059 §11).");

    /// <summary>La reserva caducó: lo que apartaba ya no es suyo.</summary>
    /// <param name="reservaId">La reserva.</param>
    /// <param name="caducaEl">Cuándo caducó.</param>
    /// <returns>El error.</returns>
    internal static ErrorDeOperacion Caducada(Guid reservaId, DateTimeOffset? caducaEl) => ErrorDeOperacion.Conflicto(
        CodigoCaducada,
        $"La reserva {reservaId} caducó el {caducaEl:O}: lo que apartaba volvió al disponible, y no " +
        "se consume ni se libera.");

    /// <summary>La reserva se consumió entera o se liberó a mano.</summary>
    /// <param name="reservaId">La reserva.</param>
    /// <param name="estado">Su estado ahora.</param>
    /// <returns>El error.</returns>
    internal static ErrorDeOperacion NoEstaActiva(Guid reservaId, EstadoDeReserva estado) => ErrorDeOperacion.Conflicto(
        CodigoNoEstaActiva,
        $"La reserva {reservaId} está «{estado}»: solo se consume o se libera una activa, y a esta " +
        "no le queda nada apartado.");

    /// <summary>Ese documento ya consumió esta reserva, y un documento la consume una vez.</summary>
    /// <param name="reservaId">La reserva.</param>
    /// <param name="documentoId">El documento.</param>
    /// <returns>El error.</returns>
    internal static ErrorDeOperacion DocumentoYaLaConsumio(Guid reservaId, Guid documentoId) =>
        ErrorDeOperacion.Conflicto(
            CodigoDocumentoYaLaConsumio,
            $"El documento {documentoId} ya consumió la reserva {reservaId}: un documento la consume " +
            "una vez, y lo que sacó ya está en el libro.");

    /// <summary>Se pide apartar más de lo que hay disponible en la clave (ADR-0059 §5).</summary>
    /// <param name="clave">La clave.</param>
    /// <param name="pedida">Lo pedido.</param>
    /// <param name="disponible">El disponible, leído con la valoración bloqueada.</param>
    /// <returns>El error.</returns>
    internal static ErrorDeOperacion PorEncimaDelDisponible(ClaveDeValoracion clave, decimal pedida, decimal disponible) =>
        ErrorDeOperacion.ReglaDeNegocio(
            CodigoPorEncimaDelDisponible,
            $"Se piden {pedida} del artículo {clave.ArticuloId} en el almacén {clave.AlmacenId}, y el " +
            $"disponible es {disponible}: el físico menos lo que ya está apartado. Reservar más de lo " +
            "disponible es el backorder, de la fase 4.");

    /// <summary>Las líneas sacan más de lo que le queda a la reserva.</summary>
    /// <param name="reservaId">La reserva.</param>
    /// <param name="sale">Lo que suman las líneas.</param>
    /// <param name="pendiente">Lo que le queda.</param>
    /// <returns>El error.</returns>
    internal static ErrorDeOperacion ConsumoPorEncimaDeLoPendiente(Guid reservaId, decimal sale, decimal pendiente) =>
        ErrorDeOperacion.ReglaDeNegocio(
            CodigoConsumoPorEncimaDeLoPendiente,
            $"Las líneas sacan {sale} y a la reserva {reservaId} le quedan {pendiente}: no se consume " +
            "más de lo que aparta.");

    /// <summary>Una línea saca de un hueco más de lo que hay en él (ADR-0059 §6).</summary>
    /// <param name="linea">La línea, desde uno.</param>
    /// <param name="ubicacionId">El hueco.</param>
    /// <param name="fisico">Lo que hay en él, con ese lote o esa serie.</param>
    /// <returns>El error.</returns>
    internal static ErrorDeOperacion ConsumoSinStock(int linea, Guid ubicacionId, decimal fisico) =>
        ErrorDeOperacion.ReglaDeNegocio(
            CodigoConsumoSinStock,
            $"La línea {linea} saca del hueco {ubicacionId} más de lo que hay en él, {fisico}, con ese " +
            "lote o esa serie: lo reservado es del almacén y no de un hueco, y la salida es de uno.");

    /// <summary>La fecha de la salida es posterior a hoy.</summary>
    /// <param name="fecha">La fecha.</param>
    /// <returns>El error.</returns>
    internal static ErrorDeOperacion ConFechaFutura(DateOnly fecha) => ErrorDeOperacion.ReglaDeNegocio(
        CodigoConFechaFutura,
        $"La fecha {fecha:yyyy-MM-dd} es posterior a hoy: el libro dice lo que ha pasado, y una " +
        "salida futura haría contar al físico algo que todavía no ha ocurrido (R3).");

    /// <summary>La fecha no cae en ningún ejercicio de la empresa (R9).</summary>
    /// <param name="fecha">La de la salida.</param>
    /// <returns>El error.</returns>
    internal static ErrorDeOperacion SinEjercicio(DateOnly fecha) => ErrorDeOperacion.Conflicto(
        CodigoSinEjercicio,
        $"La fecha {fecha:yyyy-MM-dd} no cae dentro de ningún ejercicio de esta empresa, así que la " +
        "salida no podría imputarse a ninguna autoliquidación (R9).");

    /// <summary>La fecha cae en un ejercicio ya cerrado (R9).</summary>
    /// <param name="fecha">La de la salida.</param>
    /// <returns>El error.</returns>
    internal static ErrorDeOperacion EnEjercicioCerrado(DateOnly fecha) => ErrorDeOperacion.Conflicto(
        CodigoEnEjercicioCerrado,
        $"El ejercicio al que cae la fecha {fecha:yyyy-MM-dd} está cerrado: ese periodo ya es " +
        "definitivo y no admite movimientos nuevos (R9).");

    /// <summary>Una línea que no casa con la marca del artículo de la reserva (ADR-0048 §4).</summary>
    /// <param name="noCasa">La línea, su artículo, su marca y lo que le falta o le sobra.</param>
    /// <returns>El error.</returns>
    internal static ErrorDeOperacion TrazabilidadNoCasa(LineaQueNoCasa noCasa) => ErrorDeOperacion.Conflicto(
        CodigoTrazabilidadNoCasa,
        $"La línea {noCasa.Linea} no casa con la marca del artículo {noCasa.ArticuloId}, que es " +
        $"«{noCasa.Marca}»: {LaTrazabilidadDeLasLineas.EnPalabras(noCasa.Discrepancia)} (ADR-0048 §4).");

    /// <summary>
    /// Una cantidad con decimales de un artículo por número de serie: no se podría consumir entera
    /// (ADR-0059 §5).
    /// </summary>
    /// <param name="articuloId">El artículo.</param>
    /// <param name="cantidad">Lo que se pidió apartar, en la unidad base.</param>
    /// <returns>El error.</returns>
    internal static ErrorDeOperacion SerieNoEntera(Guid articuloId, decimal cantidad) => ErrorDeOperacion.Conflicto(
        CodigoSerieNoEntera,
        $"El artículo {articuloId} va por número de serie, y se piden {cantidad} unidades base: cada " +
        "número es una pieza y el consumo saca una por línea, así que lo que sobrara de la última no " +
        "saldría nunca. Se aparta un número entero de piezas (ADR-0059 §5).");

    /// <summary>Lo que impide valorar la salida, con su código (ADR-0046 §10).</summary>
    /// <remarks>
    /// <b>Una entrada sin coste no puede pasar aquí</b>: un consumo solo saca, y una salida se valora
    /// al precio medio de lo que hay. Si llega, es un error del código.
    /// </remarks>
    /// <param name="impedimento">Lo que contestó <c>LoQueImpide</c>.</param>
    /// <param name="divisa">La divisa base de la empresa.</param>
    /// <returns>El error.</returns>
    internal static ErrorDeOperacion NoSeValora(ImpedimentoDeValoracion impedimento, string divisa) =>
        impedimento.Motivo switch
        {
            MotivoDelImpedimento.ValoracionEnOtraDivisa => ErrorDeOperacion.ReglaDeNegocio(
                CodigoValoracionEnOtraDivisa,
                $"Las existencias del artículo {impedimento.Clave.ArticuloId} en el almacén " +
                $"{impedimento.Clave.AlmacenId} están valoradas en otra divisa que la de la empresa, " +
                $"{divisa}, y sacarlas exigiría un tipo de cambio (ADR-0046 §7)."),
            MotivoDelImpedimento.FechaAnteriorAlUltimoMovimiento => ErrorDeOperacion.ReglaDeNegocio(
                CodigoFechaAnteriorAlUltimoMovimiento,
                $"El artículo {impedimento.Clave.ArticuloId} se movió por última vez en el almacén " +
                $"{impedimento.Clave.AlmacenId} el {impedimento.UltimaFecha:yyyy-MM-dd}, y la salida " +
                "lleva una fecha anterior: póngale esa fecha o una posterior (ADR-0047)."),
            _ => throw new ArgumentOutOfRangeException(
                nameof(impedimento),
                impedimento.Motivo,
                "Un consumo solo saca, y una salida se valora al precio medio: no puede faltarle " +
                "coste. Esto es un error del código, no del almacén."),
        };
}
