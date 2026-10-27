namespace Bastion.BuildingBlocks.Application.Listados;

/// <summary>
/// Una foto de la base de un módulo: lo que se lea mientras está abierta ve los datos de un mismo
/// instante, y no puede escribir nada.
/// </summary>
/// <remarks>
/// <para>
/// <b>Para qué.</b> La exportación recorre un listado entero por páginas internas, cada una en su
/// orden (ADR-0073). Sin una foto, una fila que entra entre dos páginas empuja a la última de la
/// primera a salir otra vez en la segunda, y una que se borra hace que otra no salga en ninguna.
/// Con la foto, todas salen de un mismo instante, y el total de la primera página es el de todas.
/// </para>
/// <para>
/// <b>Una por módulo</b>, como la unidad de trabajo: cada módulo declara la suya en su capa de
/// aplicación, y su infraestructura la abre sobre su contexto, que es el mismo con el que leen sus
/// listados. Una foto de otro contexto no vería las lecturas del listado.
/// </para>
/// </remarks>
public interface IFotoDeLectura
{
    /// <summary>
    /// Abre la foto. Lo que se lea con el contexto del módulo hasta liberarla sale de ella.
    /// </summary>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    /// <returns>La foto abierta, que se cierra al liberarla.</returns>
    Task<IAsyncDisposable> AbrirAsync(CancellationToken cancelacion);
}
