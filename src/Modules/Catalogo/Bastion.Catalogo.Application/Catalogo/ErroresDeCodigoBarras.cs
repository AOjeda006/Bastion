using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Catalogo.Domain.Catalogo;

namespace Bastion.Catalogo.Application.Catalogo;

/// <summary>Los desenlaces fallidos de los códigos de barras del artículo.</summary>
internal static class ErroresDeCodigoBarras
{
    internal static ErrorDeOperacion NoEncontrado(Guid id) => ErrorDeOperacion.NoEncontrado(
        "codigo-barras-no-encontrado",
        $"No hay ningún código de barras con el identificador {id}.");

    /// <summary>Ese GTIN ya lo lleva un artículo de esta empresa (ADR-0051 §5).</summary>
    /// <remarks>
    /// <para>
    /// <b>Lo dan dos caminos, y tienen que dar el mismo cuerpo.</b> La comprobación previa del alta
    /// lo devuelve sin llegar al motor, y el borde lo contesta cuando el índice único
    /// <c>(empresa_id, gtin)</c> para a quien pierde la carrera. El índice solo trae su nombre, así que
    /// el error no lleva parámetros: si los llevara, la respuesta diría por qué camino se llegó.
    /// </para>
    /// <para>
    /// <b>Por eso Infrastructure ve esta clase</b>: la declaración de la restricción vive en
    /// <c>ModuloDeCatalogo</c>, y una sola fábrica es un solo texto. Copiarla allí, como hace
    /// Inventario con los errores que ningún caso de uso devuelve, dejaría dos textos para un
    /// <c>type</c>.
    /// </para>
    /// </remarks>
    /// <returns>El error.</returns>
    internal static ErrorDeOperacion Duplicado() => ErrorDeOperacion.Conflicto(
        "codigo-barras-duplicado",
        "Ese GTIN ya lo lleva un artículo de esta empresa, y un GTIN identifica una sola cosa. Si " +
        "ahora es de otro artículo, quítelo antes del que lo lleva.");

    internal static ErrorDeOperacion NivelNoValido(string valores) => ErrorDeOperacion.Validacion(
        "codigo-barras-nivel-no-valido",
        $"El nivel tiene que ser uno de estos: {valores}.");

    /// <summary>Las unidades no cuadran con el nivel (ADR-0051 §3).</summary>
    /// <remarks>
    /// Antes de construir el agregado, porque <c>CodigoBarras.Nuevo</c> lanza con unas unidades que no
    /// cuadran: si se llega ahí, es un defecto, y lo que la petición merece es un <c>400</c>.
    /// </remarks>
    /// <returns>El error.</returns>
    internal static ErrorDeOperacion UnidadesNoValidas() => ErrorDeOperacion.Validacion(
        "codigo-barras-unidades-no-validas",
        "La base lleva una unidad. Una caja o un palé llevan las unidades base que contienen, y son " +
        "dos o más.");
}

/// <summary>Por qué un texto no es el GTIN de un artículo: un <c>type</c> por motivo (ADR-0051 §4).</summary>
/// <remarks>
/// <b>Una rama por motivo, cada una con su literal</b>, y no un código compuesto a partir del nombre
/// del motivo: el catálogo de errores se genera leyendo el código fuente, y solo reconoce un
/// literal. Si el enumerado gana un motivo, la rama que falta lanza, y <c>ElGtinTests</c> fija los
/// nombres del enumerado para que eso se vea antes.
/// </remarks>
internal static class ErroresDeGtin
{
    internal static ErrorDeOperacion DelMotivo(MotivoDeRechazoDelGtin motivo) => motivo switch
    {
        MotivoDeRechazoDelGtin.NoSonDigitos => ErrorDeOperacion.Validacion(
            "gtin-no-son-digitos",
            "Un GTIN son solo cifras del 0 al 9: sin letras, ni guiones, ni espacios por dentro."),
        MotivoDeRechazoDelGtin.LargoNoAdmitido => ErrorDeOperacion.Validacion(
            "gtin-largo-no-admitido",
            "Un GTIN tiene 8, 12, 13 o 14 cifras. Si le falta una, no se le pone: hay que volver a " +
            "mirar el código."),
        MotivoDeRechazoDelGtin.DigitoDeControl => ErrorDeOperacion.Validacion(
            "gtin-digito-de-control",
            "El dígito de control no cuadra con las demás cifras: el número está mal leído o mal " +
            "escrito."),
        MotivoDeRechazoDelGtin.CirculacionRestringida => ErrorDeOperacion.Validacion(
            "gtin-circulacion-restringida",
            "Es un número de circulación restringida: lo pone una tienda o un país para su uso " +
            "interno, y fuera de ahí no identifica un artículo (GS1, §2.1.11)."),
        MotivoDeRechazoDelGtin.MedidaVariable => ErrorDeOperacion.Validacion(
            "gtin-medida-variable",
            "Es un GTIN de medida variable: le falta la medida para estar entero, y por sí solo no " +
            "identifica un artículo (GS1, §2.1.10)."),
        MotivoDeRechazoDelGtin.Cupon => ErrorDeOperacion.Validacion(
            "gtin-cupon",
            "Es un cupón o un vale de devolución, no un artículo (GS1, §2.6.3 y §2.6.4)."),
        MotivoDeRechazoDelGtin.SinAsignar => ErrorDeOperacion.Validacion(
            "gtin-sin-asignar",
            "Ese prefijo está reservado, o se dio para algo que no es un GTIN, así que no es el de " +
            "ningún artículo."),
        _ => throw new ArgumentOutOfRangeException(nameof(motivo), motivo, "Un motivo sin su error."),
    };
}
