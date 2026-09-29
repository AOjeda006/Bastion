namespace Bastion.Catalogo.Contracts.Catalogo;

/// <summary>Cómo se lleva el stock de un artículo: sin más, por lote o por número de serie.</summary>
/// <remarks>
/// <para>
/// <b>Es la marca del artículo vista desde fuera de Catálogo</b>, con los mismos tres nombres que la
/// de su dominio, que desde aquí no se ve. Los nombres son el contrato: la columna los guarda como
/// texto, y quien la lee los traduce uno a uno.
/// </para>
/// <para>
/// <b>No tiene valor cero, y es a propósito.</b> En <see cref="AptitudParaMoverExistencias"/> el cero
/// es la respuesta que no autoriza nada. Aquí no hay ninguna así: <see cref="Ninguna"/> autoriza las
/// líneas sin código. Un <c>default</c> que llegara por un camino que no pasó por el puerto no es
/// ninguna marca, y quien lo reciba no sabe traducirlo.
/// </para>
/// </remarks>
public enum MarcaDeTrazabilidad
{
    /// <summary>Sin lote ni número de serie.</summary>
    Ninguna = 1,

    /// <summary>Cada línea lleva el lote que mueve.</summary>
    PorLote = 2,

    /// <summary>Cada línea mueve una unidad con su número de serie.</summary>
    PorNumeroSerie = 3,
}

/// <summary>
/// La marca de trazabilidad de los artículos que mueve un documento (ADR-0048 §4).
/// </summary>
/// <remarks>
/// <para>
/// <b>Lo publica Catálogo y lo implementa Inventario</b>, como el puerto de ejercicios lo publica
/// Organización. La lectura que decide tiene que caer en la transacción del documento que se
/// confirma, con su cerrojo, y esa transacción es de Inventario.
/// </para>
/// <para>
/// <b>Dos lecturas, la cortesía y la guarda.</b> Al abrir el documento se pregunta sin cerrojo, para
/// avisar pronto. Al confirmar se pregunta con la fila del artículo bloqueada en compartido hasta el
/// <c>COMMIT</c>. Así, un cambio de marca, que la bloquea en exclusiva, espera a la confirmación, o
/// la confirmación a él, y ninguno decide sobre una marca que el otro está cambiando.
/// </para>
/// <para>
/// La empresa no viaja en la firma: sale de donde la toma el filtro global (R8).
/// </para>
/// </remarks>
public interface IConsultaDeTrazabilidad
{
    /// <summary>La marca de cada artículo, <b>sin cerrojo</b>: la cortesía de abrir.</summary>
    /// <param name="articulos">Los artículos del documento.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    /// <returns>La marca de cada artículo que existe en la empresa.</returns>
    Task<IReadOnlyDictionary<Guid, MarcaDeTrazabilidad>> MarcasDeAsync(
        IReadOnlyCollection<Guid> articulos, CancellationToken cancelacion);

    /// <summary>
    /// La marca de cada artículo, <b>con la fila bloqueada en compartido hasta el <c>COMMIT</c></b>:
    /// la guarda de confirmar.
    /// </summary>
    /// <remarks>
    /// Quien la implementa <b>revienta</b> si no hay transacción abierta: sin ella, el cerrojo se
    /// soltaría al acabar la lectura y no guardaría nada.
    /// </remarks>
    /// <param name="articulos">Los artículos del documento.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    /// <returns>La marca de cada artículo que existe en la empresa.</returns>
    Task<IReadOnlyDictionary<Guid, MarcaDeTrazabilidad>> MarcasParaMoverAsync(
        IReadOnlyCollection<Guid> articulos, CancellationToken cancelacion);
}
