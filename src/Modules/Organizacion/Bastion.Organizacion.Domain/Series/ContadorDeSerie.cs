namespace Bastion.Organizacion.Domain.Series;

/// <summary>
/// El último número que ha entregado una serie, en su propia fila (ADR-0039).
/// </summary>
/// <remarks>
/// <para>
/// <b>Está aquí y no en <see cref="Serie"/> por el testigo de concurrencia.</b> El de la serie es
/// <c>xmin</c>, y <c>xmin</c> lo mueve PostgreSQL en <b>cada</b> escritura de la fila: con el
/// contador dentro, cada documento confirmado invalidaría el <c>ETag</c> de quien tuviera esa serie
/// abierta y su pantalla contestaría <c>412</c> sin que nadie la hubiera editado. Una serie se edita
/// una vez al año y se confirman documentos todo el día. El argumento entero, en el ADR-0039.
/// </para>
/// <para>
/// <b>Aquí no hay forma de subir el contador</b>, y esa ausencia es la invariante: ni un <c>set</c>
/// accesible ni un método que lo incremente. Lo único que escribe esta columna es la sentencia del
/// mecanismo de numeración, que incrementa sobre lo que hay y devuelve el resultado, así que no hay
/// un número que un llamante pueda equivocar. Sustituye —con ventaja— a
/// <c>Serie.RegistrarNumeroAsignado</c>, que comprobaba que el número no saltara y que ningún
/// módulo podía llamar.
/// </para>
/// <para>
/// <b>No lleva <c>empresa_id</c></b>: su inquilinato es el de su serie, y la sentencia comprueba la
/// empresa sobre la fila de <c>series</c> que lee para condicionar el incremento — que es
/// exactamente la fila que el filtro global habría protegido.
/// </para>
/// </remarks>
public sealed class ContadorDeSerie
{
    private ContadorDeSerie(Guid serieId) => SerieId = serieId;

    /// <summary>Constructor de materialización para EF Core.</summary>
    private ContadorDeSerie()
    {
    }

    /// <summary>La serie cuyo contador es. Es también la clave primaria de la fila.</summary>
    public Guid SerieId { get; private set; }

    /// <summary>Último número entregado. Cero mientras no haya numerado nada.</summary>
    /// <remarks>
    /// El nombre es el de la columna, y la columna se llama así porque la sentencia de numeración
    /// la escribe <b>a mano</b>: <c>contadores_de_serie.contador</c> se leería dos veces lo mismo, y
    /// <c>valor</c> no diría de qué. En SQL crudo, el nombre es toda la documentación que hay.
    /// </remarks>
    public long UltimoNumero { get; private set; }

    /// <summary>
    /// La fila que nace con la serie, en cero.
    /// </summary>
    /// <remarks>
    /// <b>Nace con la serie y no en la primera numeración</b>, y eso es lo que permite que «ninguna
    /// fila devuelta» sea un fallo sin ambigüedad: si la fila se creara al numerar por primera vez,
    /// el mecanismo no podría distinguir «esta serie está cerrada o es de otra empresa» de «todavía
    /// no tiene contador».
    /// </remarks>
    /// <param name="serieId">La serie a la que pertenece.</param>
    internal static ContadorDeSerie ParaSerieNueva(Guid serieId) => new(serieId);
}
