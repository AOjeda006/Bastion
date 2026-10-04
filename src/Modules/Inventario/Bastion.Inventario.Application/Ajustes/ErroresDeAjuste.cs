using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Inventario.Application.Trazabilidad;
using Bastion.Inventario.Domain.LotesYSeries;
using Bastion.Inventario.Domain.Valoraciones;

namespace Bastion.Inventario.Application.Ajustes;

/// <summary>Los desenlaces de negocio del ajuste que no vienen de un puerto.</summary>
internal static class ErroresDeAjuste
{
    internal const string CodigoNoEncontrado = "ajuste-no-encontrado";
    internal const string CodigoSinLineas = "ajuste-sin-lineas";
    internal const string CodigoNoEstaEnBorrador = "ajuste-no-esta-en-borrador";
    internal const string CodigoNoEstaConfirmado = "ajuste-no-esta-confirmado";
    internal const string CodigoMotivoNoValido = "ajuste-motivo-no-valido";
    internal const string CodigoSinEjercicio = "ajuste-sin-ejercicio";
    internal const string CodigoEnEjercicioCerrado = "ajuste-en-ejercicio-cerrado";
    internal const string CodigoConFechaFutura = "ajuste-con-fecha-futura";
    internal const string CodigoCosteNoValido = "ajuste-coste-no-valido";
    internal const string CodigoEntradaSinCosteNiPrecioMedio = "ajuste-entrada-sin-coste-ni-precio-medio";
    internal const string CodigoValoracionEnOtraDivisa = "ajuste-valoracion-en-otra-divisa";
    internal const string CodigoFechaAnteriorAlUltimoMovimiento = "ajuste-fecha-anterior-al-ultimo-movimiento";
    internal const string CodigoLoteNoValido = "ajuste-lote-no-valido";
    internal const string CodigoNumeroDeSerieNoValido = "ajuste-numero-de-serie-no-valido";
    internal const string CodigoSerieNoUnitaria = "ajuste-serie-no-unitaria";
    internal const string CodigoSerieRepetida = "ajuste-serie-repetida";
    internal const string CodigoTrazabilidadNoCasa = "ajuste-trazabilidad-no-casa";

    internal static ErrorDeOperacion NoEncontrado(Guid ajusteId) => ErrorDeOperacion.NoEncontrado(
        CodigoNoEncontrado,
        $"No hay ningún ajuste con el identificador {ajusteId}.");

    /// <summary>La vuelta de la R13, dicha antes de que exista el documento.</summary>
    /// <remarks>
    /// Un ajuste sin líneas no llega ni a abrirse. Podría abrirse vacío y rechazarse al confirmar,
    /// y sería peor: dejaría en la base documentos en borrador que nunca podrán confirmarse, y
    /// alguien tendría que decidir qué se hace con ellos.
    /// </remarks>
    /// <param name="ajusteId">El documento, si ya existía.</param>
    /// <returns>El error.</returns>
    internal static ErrorDeOperacion SinLineas(Guid ajusteId) => ErrorDeOperacion.Validacion(
        CodigoSinLineas,
        $"El ajuste {ajusteId} no tiene ninguna línea: un documento que no mueve el libro no " +
        "ajusta nada (R13).");

    /// <summary>Una línea que baja con coste, o un coste negativo (ADR-0046 §7).</summary>
    /// <remarks>
    /// <b>Se rechaza y no se ignora.</b> Una salida se valora al precio medio, así que un coste
    /// escrito ahí no se usaría, y quien lo escribió creería que sí. Un coste negativo no existe:
    /// una muestra o un regalo entran a cero.
    /// </remarks>
    /// <returns>El error.</returns>
    internal static ErrorDeOperacion CosteNoValido() => ErrorDeOperacion.Validacion(
        CodigoCosteNoValido,
        "Una línea que baja existencias no lleva coste, porque se valora al precio medio, y " +
        "ninguna lleva un coste negativo: una muestra o un regalo entran a cero (ADR-0046).");

    /// <summary>Lo que impide valorar el documento, con su código (ADR-0046 §10).</summary>
    /// <remarks>
    /// <para>
    /// <b>Los tres son un <c>422</c></b>: el cuerpo está bien escrito, y lo que falla es lo que hay
    /// en el almacén cuando se confirma. El mismo documento se confirmaría con otro saldo.
    /// </para>
    /// <para>
    /// <b>Una entrada sin coste</b> se valora al precio medio de su artículo en su almacén, y sin
    /// existencias no hay precio medio. <b>Una valoración en otra divisa</b> es la de una empresa que
    /// cambió de divisa base con existencias: sumarlas exigiría un tipo de cambio con fecha, y eso es
    /// de la fase 6.
    /// </para>
    /// </remarks>
    /// <param name="impedimento">Lo que contestó <c>LoQueImpide</c>.</param>
    /// <param name="divisa">La divisa del documento.</param>
    /// <returns>El error.</returns>
    internal static ErrorDeOperacion NoSeValora(ImpedimentoDeValoracion impedimento, string divisa) =>
        impedimento.Motivo switch
        {
            MotivoDelImpedimento.EntradaSinCosteNiPrecioMedio => ErrorDeOperacion.ReglaDeNegocio(
                CodigoEntradaSinCosteNiPrecioMedio,
                $"El artículo {impedimento.Clave.ArticuloId} no tiene existencias en el almacén " +
                $"{impedimento.Clave.AlmacenId}, así que no tiene precio medio: su entrada necesita " +
                "un coste (ADR-0046 §5)."),
            MotivoDelImpedimento.ValoracionEnOtraDivisa => ErrorDeOperacion.ReglaDeNegocio(
                CodigoValoracionEnOtraDivisa,
                $"Las existencias del artículo {impedimento.Clave.ArticuloId} en el almacén " +
                $"{impedimento.Clave.AlmacenId} están valoradas en otra divisa que la del " +
                $"documento, {divisa}, y sumarlas exigiría un tipo de cambio (ADR-0046 §7)."),
            MotivoDelImpedimento.FechaAnteriorAlUltimoMovimiento => ErrorDeOperacion.ReglaDeNegocio(
                CodigoFechaAnteriorAlUltimoMovimiento,
                $"El artículo {impedimento.Clave.ArticuloId} se movió por última vez en el almacén " +
                $"{impedimento.Clave.AlmacenId} el {impedimento.UltimaFecha:yyyy-MM-dd}, y el " +
                "documento lleva una fecha anterior: sumar el libro hasta los días de en medio daría " +
                "un estado que no existió. Póngale esa fecha o una posterior (ADR-0047)."),
            _ => throw new ArgumentOutOfRangeException(
                nameof(impedimento), impedimento.Motivo, "un impedimento que el borde no sabe decir"),
        };

    internal static ErrorDeOperacion NoEstaEnBorrador(Guid ajusteId, string estado) =>
        ErrorDeOperacion.Conflicto(
            CodigoNoEstaEnBorrador,
            $"El ajuste {ajusteId} está en estado «{estado}»: solo se confirma un borrador, y un " +
            "ajuste confirmado no se vuelve a confirmar porque sus filas del libro ya están " +
            "escritas y el libro es de solo añadido (R2, R3).");

    /// <summary>El motivo de la anulación, que es lo único que trae su petición.</summary>
    /// <remarks>
    /// <b>Se comprueba en el caso de uso aunque el dominio también lo compruebe.</b> Ahí es una
    /// invariante y se lanza; aquí es un cuerpo mal escrito, y lo que el borde debe devolver por
    /// eso es un 400 con su código, no un 500 (ADR-0004). No se dice qué tenía de malo más allá
    /// del largo: el valor recibido es del llamante y no se le devuelve dentro de un error.
    /// </remarks>
    /// <returns>El error.</returns>
    internal static ErrorDeOperacion MotivoNoValido() => ErrorDeOperacion.Validacion(
        CodigoMotivoNoValido,
        "El motivo de la anulación no puede estar vacío ni pasar de " +
        $"{Domain.Ajustes.Ajuste.LargoDelMotivo} caracteres: es lo único que queda para entender " +
        "la corrección dentro de dos años.");

    /// <summary>La fecha del documento no cae en ningún ejercicio de la empresa (R9).</summary>
    /// <remarks>
    /// <b>Es un desenlace distinto del ejercicio cerrado, y por eso lleva su propio código.</b> Se
    /// arreglan de maneras distintas: éste, abriendo el ejercicio que falta —o corrigiendo la
    /// fecha, si estaba mal escrita—; el otro, reabriendo o poniendo el documento donde le toca.
    /// Un solo código obligaría a leer la prosa para saber cuál de las dos cosas hacer, y la prosa
    /// es lo único del error que no es contrato.
    /// </remarks>
    /// <param name="fecha">La fecha de operación que no cae en ningún ejercicio.</param>
    /// <returns>El error.</returns>
    internal static ErrorDeOperacion SinEjercicio(DateOnly fecha) => ErrorDeOperacion.Conflicto(
        CodigoSinEjercicio,
        $"La fecha de operación {fecha:yyyy-MM-dd} no cae dentro de ningún ejercicio de esta " +
        "empresa, así que el documento no podría imputarse a ninguna autoliquidación. Abra el " +
        "ejercicio que falta o corrija la fecha (R9).");

    /// <summary>La fecha del documento cae en un ejercicio ya cerrado (R9).</summary>
    /// <returns>El error.</returns>
    internal static ErrorDeOperacion EnEjercicioCerrado(DateOnly fecha) =>
        ErrorDeOperacion.Conflicto(
            CodigoEnEjercicioCerrado,
            $"El ejercicio al que cae la fecha de operación {fecha:yyyy-MM-dd} está cerrado: ese " +
            "periodo ya es definitivo y no admite documentos nuevos. Reabra el ejercicio, con su " +
            "motivo, o lleve el documento a una fecha del ejercicio abierto (R9).");

    /// <summary>La fecha del documento es posterior a hoy (ADR-0044).</summary>
    /// <remarks>
    /// <b>Un código propio y no el del ejercicio</b>: la fecha puede caer en un ejercicio abierto y
    /// seguir sin poder confirmarse. Lo que se arregla aquí es esperar al día, o corregir la fecha
    /// si estaba mal escrita.
    /// </remarks>
    /// <param name="fecha">La fecha de operación del documento.</param>
    /// <returns>El error.</returns>
    internal static ErrorDeOperacion ConFechaFutura(DateOnly fecha) => ErrorDeOperacion.Conflicto(
        CodigoConFechaFutura,
        $"La fecha de operación {fecha:yyyy-MM-dd} es posterior a hoy: el libro dice lo que ha " +
        "pasado, y la existencia es su suma, así que un movimiento futuro la haría contar algo " +
        "que todavía no ha ocurrido. Confírmelo ese día, o corrija la fecha (R3).");

    internal static ErrorDeOperacion NoEstaConfirmado(Guid ajusteId, string estado) =>
        ErrorDeOperacion.Conflicto(
            CodigoNoEstaConfirmado,
            $"El ajuste {ajusteId} está en estado «{estado}»: solo se anula lo que está " +
            "confirmado. Un borrador no ha movido nada, así que no hay nada que compensar.");

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
    /// <remarks>
    /// <b>No se devuelve lo que llegó</b>, como en el motivo: el valor es del llamante. Se dice la
    /// línea y la forma que se espera.
    /// </remarks>
    /// <param name="linea">La línea, desde uno.</param>
    /// <returns>El error.</returns>
    internal static ErrorDeOperacion LoteNoValido(int linea) => ErrorDeOperacion.Validacion(
        CodigoLoteNoValido,
        $"El lote de la línea {linea} no es un código GS1: de 1 a {CodigoGs1.LargoMaximo} caracteres " +
        "del conjunto 82, sin espacios dentro, que es lo que cabe en la etiqueta (ADR-0048 §2).");

    /// <summary>El número de serie de una línea no es un código GS1 (ADR-0048 §2).</summary>
    /// <param name="linea">La línea, desde uno.</param>
    /// <returns>El error.</returns>
    internal static ErrorDeOperacion NumeroDeSerieNoValido(int linea) => ErrorDeOperacion.Validacion(
        CodigoNumeroDeSerieNoValido,
        $"El número de serie de la línea {linea} no es un código GS1: de 1 a " +
        $"{CodigoGs1.LargoMaximo} caracteres del conjunto 82, sin espacios dentro, que es lo que cabe " +
        "en la etiqueta (ADR-0048 §2).");

    /// <summary>Una línea con número de serie que no mueve una unidad base (ADR-0048 §3).</summary>
    /// <param name="linea">La línea, desde uno.</param>
    /// <returns>El error.</returns>
    internal static ErrorDeOperacion SerieNoUnitaria(int linea) => ErrorDeOperacion.Validacion(
        CodigoSerieNoUnitaria,
        $"La línea {linea} lleva número de serie y no mueve una unidad base, ni arriba ni abajo: " +
        "un número de serie es una unidad, y dos unidades con el mismo número no existen " +
        "(ADR-0048 §3).");

    /// <summary>El mismo número de serie en dos líneas del documento (ADR-0048 §3).</summary>
    /// <remarks>
    /// <b>Aunque sea en otra ubicación y con el signo contrario.</b> Sacarla de una estantería y
    /// meterla en otra es la reubicación, que es otro documento: dentro de uno, el índice que la
    /// mantiene en un sitio chocaría o no según el orden en que el motor recorriera las filas.
    /// </remarks>
    /// <param name="linea">La segunda línea que la nombra, desde uno.</param>
    /// <param name="serie">El número, ya normalizado.</param>
    /// <returns>El error.</returns>
    internal static ErrorDeOperacion SerieRepetida(int linea, string serie) => ErrorDeOperacion.Validacion(
        CodigoSerieRepetida,
        $"La línea {linea} repite el número de serie «{serie}» de otra línea del mismo artículo: una " +
        "serie sale una sola vez por documento, y moverla de sitio es la reubicación (ADR-0048 §3).");

    /// <summary>Una línea que no casa con la marca de su artículo (ADR-0048 §4).</summary>
    /// <remarks>
    /// <para>
    /// <b>Un <c>409</c> y no un <c>400</c></b>: el cuerpo puede estar bien escrito, y lo que falla es
    /// la ficha del artículo. Al abrir es la cortesía, y al confirmar, la guarda: la marca se lee
    /// con la fila bloqueada, y un borrador escrito con la marca de antes se para aquí.
    /// </para>
    /// <para>
    /// <b>Dice qué línea y qué le falta o le sobra</b>, que es lo que hay que corregir.
    /// </para>
    /// </remarks>
    /// <param name="noCasa">La línea, su artículo, su marca y lo que le falta o le sobra.</param>
    /// <returns>El error.</returns>
    internal static ErrorDeOperacion TrazabilidadNoCasa(LineaQueNoCasa noCasa) =>
        ErrorDeOperacion.Conflicto(
            CodigoTrazabilidadNoCasa,
            $"La línea {noCasa.Linea} no casa con la marca del artículo {noCasa.ArticuloId}, que es " +
            $"«{noCasa.Marca}»: {LaTrazabilidadDeLasLineas.EnPalabras(noCasa.Discrepancia)} Corrija la " +
            "línea, o la marca si el artículo todavía no se ha movido (ADR-0048 §4).");
}
