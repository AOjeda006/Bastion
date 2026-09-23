namespace Bastion.Organizacion.Contracts.Ejercicios;

/// <summary>
/// Lo que un módulo con documentos tiene que saber contestar sobre un intervalo de fechas.
/// </summary>
/// <remarks>
/// <para>
/// <b>Es la flecha al revés, y por eso vive aquí.</b> Los demás puertos de este ensamblado los
/// implementa Organización y los llaman los otros módulos; éste lo implementa <b>cada módulo con
/// documentos</b> y lo llama Organización. La interfaz sigue estando en el <c>Contracts</c> del
/// módulo que la necesita, que es lo único que otro módulo puede ver (§4), y quien la implementa
/// sigue sin conocer a Organización por dentro: ve una interfaz y nada más.
/// </para>
/// <para>
/// <b>Por qué Organización no consulta las tablas.</b> Cerrar un ejercicio tiene que saber si queda
/// algún borrador dentro, y esa pregunta se contesta en <c>inventario.ajustes</c>, en
/// <c>ventas.facturas</c> y en las tablas de otros cuatro módulos que todavía no existen. Un
/// <c>JOIN</c> entre esquemas es exactamente lo que el §4 prohíbe, y una lista de tablas escrita en
/// Organización sería una lista que hay que acordarse de ampliar: el día que alguien añada un
/// módulo con documentos y no la toque, cerrar diría que no queda nada dentro. Con un puerto, el
/// módulo nuevo se registra o <b>no existe</b> para el cierre — y que no exista ninguno es lo que
/// afirma el caso de uso antes de preguntar (ADR-0020).
/// </para>
/// <para>
/// <b>Los dos intervalos van cerrados por los dos lados</b>, igual que <c>Ejercicio.Comprende</c> y
/// que el <c>daterange(…, '[]')</c> de la restricción de exclusión. Los tres tienen que decir lo
/// mismo del primer y del último día, o habría documentos que un sitio cuenta dentro y otro fuera.
/// </para>
/// </remarks>
public interface IDocumentosDeUnPeriodo
{
    /// <summary>
    /// Cómo se llama el módulo que contesta, para poder decir <b>quién</b> se ha negado.
    /// </summary>
    /// <remarks>
    /// «Quedan borradores dentro del ejercicio» no se puede arreglar: hay que saber dónde están.
    /// Con seis módulos registrados, un error sin este dato obliga a buscarlos uno por uno.
    /// </remarks>
    string Modulo { get; }

    /// <summary>Si queda algún documento <b>en borrador</b> cuya fecha cae dentro del intervalo.</summary>
    /// <remarks>
    /// Es la pregunta del <b>cierre</b>. Sólo mira borradores a propósito: lo confirmado ya está
    /// contado y el cierre es justo lo que lo congela; lo que no puede quedarse a medias es un
    /// documento que todavía puede cambiar de importe dentro de un periodo que se da por cerrado.
    /// </remarks>
    /// <param name="empresaId">Empresa por la que se pregunta (R8).</param>
    /// <param name="desde">Primer día del intervalo, incluido.</param>
    /// <param name="hasta">Último día del intervalo, incluido.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<bool> HayBorradoresEnAsync(
        Guid empresaId,
        DateOnly desde,
        DateOnly hasta,
        CancellationToken cancelacion);

    /// <summary>
    /// Si queda algún documento —<b>en el estado que sea</b>— cuya fecha cae dentro del intervalo.
    /// </summary>
    /// <remarks>
    /// Es la pregunta de <b>mover</b> y de <b>borrar</b> un ejercicio, y ahí no vale mirar sólo
    /// borradores: un documento confirmado que se queda fuera del intervalo es peor, porque ya está
    /// contado en un periodo del que va a dejar de formar parte sin que nadie lo toque.
    /// </remarks>
    /// <param name="empresaId">Empresa por la que se pregunta (R8).</param>
    /// <param name="desde">Primer día del intervalo, incluido.</param>
    /// <param name="hasta">Último día del intervalo, incluido.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<bool> HayDocumentosEnAsync(
        Guid empresaId,
        DateOnly desde,
        DateOnly hasta,
        CancellationToken cancelacion);
}
