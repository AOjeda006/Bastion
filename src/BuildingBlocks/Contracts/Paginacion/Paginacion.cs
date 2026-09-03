namespace Bastion.BuildingBlocks.Contracts.Paginacion;

/// <summary>
/// Lo que un cliente pide de una colección: qué página, de qué tamaño y en qué orden.
/// </summary>
/// <remarks>
/// <para>
/// Sin anotaciones de validación, a propósito. Quien comprueba que los números son razonables es
/// el modelo de consulta del borde, que es el que MVC enlaza y por tanto el único que se valida
/// solo; ponerlas también aquí daría dos sitios que decir lo mismo y uno de ellos —este— nunca se
/// ejecutaría, con lo que parecería que protege algo sin protegerlo.
/// </para>
/// <para>
/// Vive en el bloque común desde el ítem 1.3. Estaba duplicada, letra por letra, en Identidad y en
/// Organización —los dos ficheros diferían en la línea del <c>namespace</c> y en nada más—, y la
/// fase 1 traía dos módulos más. El motivo de por qué esto no rompe la frontera del §4 está en el
/// ADR-0029: la regla de capas prohíbe que un <c>Contracts</c> arrastre el interior de SU módulo,
/// no que vea el núcleo común, que todos los módulos ven ya.
/// </para>
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

    /// <summary>
    /// Por qué campo se ordena, o nulo para el orden natural del recurso.
    /// </summary>
    /// <remarks>
    /// Nulo es la respuesta correcta y no un hueco: cada listado tiene un orden por omisión
    /// estable, y sin él PostgreSQL no promete ninguno entre consultas — la página 2 podría
    /// repetir o saltarse filas de la 1 sin que nadie hubiera tocado nada.
    /// </remarks>
    public Orden? Orden { get; init; }

    /// <summary>
    /// Texto por el que se acota el listado, o nulo para no acotar.
    /// </summary>
    /// <remarks>
    /// Es un filtro de texto sobre campos que NO son sensibles, y cuáles son los decide cada
    /// recurso. Viaja en la URL como <c>?q=</c>, así que lo que se escriba aquí queda en el
    /// historial del navegador, en lo que se copia y pega, y en el registro de acceso del
    /// servidor de delante. Buscar por NIF, correo o teléfono va por cuerpo (ADR-0025).
    /// </remarks>
    public string? Filtro { get; init; }

    /// <summary>Cuántos elementos hay que saltarse para llegar a esta página.</summary>
    public int Salto => (Pagina - 1) * Tamanio;
}
