namespace Bastion.Organizacion.Contracts.Comun;

/// <summary>
/// Lo que un cliente pide de una colección: qué página y de qué tamaño.
/// </summary>
/// <remarks>
/// Sin anotaciones de validación, a propósito. Quien comprueba que los números son razonables es
/// el modelo de consulta del borde, que es el que MVC enlaza y por tanto el único que se valida
/// solo; ponerlas también aquí daría dos sitios que decir lo mismo y uno de ellos —este— nunca se
/// ejecutaría, con lo que parecería que protege algo sin protegerlo.
/// </remarks>
public sealed record Paginacion
{
    /// <summary>Tamaño de página cuando el cliente no pide ninguno.</summary>
    public const int TamanioPorDefecto = 20;

    /// <summary>
    /// Tope de tamaño de página. Existe porque sin él <c>?size=100000</c> es una descarga
    /// completa de la tabla disfrazada de página, y la escribe cualquiera desde la barra del
    /// navegador (§9, «paginación con tope»).
    /// </summary>
    public const int TamanioMaximo = 200;

    /// <summary>Número de página, empezando en 1.</summary>
    public int Pagina { get; init; } = 1;

    /// <summary>Cuántos elementos se piden.</summary>
    public int Tamanio { get; init; } = TamanioPorDefecto;

    /// <summary>Cuántos elementos hay que saltarse para llegar a esta página.</summary>
    public int Salto => (Pagina - 1) * Tamanio;
}
