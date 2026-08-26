namespace Bastion.Organizacion.Contracts.Empresas;

/// <summary>
/// Lo que otros módulos pueden preguntar sobre las empresas.
/// </summary>
/// <remarks>
/// <para>
/// Es la lectura entre módulos que describe el §4: <b>interfaz del <c>Contracts</c> del módulo
/// dueño, resuelta en proceso</b>. Una llamada a método, no una petición HTTP, y desde luego no
/// un <c>JOIN</c> contra <c>organizacion.empresas</c> —eso sería la consulta que cruza esquemas
/// que la regla 3 prohíbe—.
/// </para>
/// <para>
/// <b>Es la otra mitad de «sin claves foráneas entre esquemas» (regla 4).</b> Identidad guarda el
/// identificador de empresa en su tabla de pertenencias sin clave ajena; lo que impide que ahí
/// acabe un identificador inventado no es el motor, es esta pregunta. Sin ella, la ausencia de
/// clave ajena sería un agujero en vez de una frontera.
/// </para>
/// </remarks>
public interface IConsultaDeEmpresas
{
    /// <summary>Si existe una empresa con ese identificador y no está dada de baja.</summary>
    /// <remarks>
    /// Activa, no solo existente: dar de alta a alguien en una empresa bloqueada sería concederle
    /// acceso a una sociedad que se dio de baja a propósito.
    /// </remarks>
    /// <param name="empresaId">Identificador de la empresa.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<bool> EstaActivaAsync(Guid empresaId, CancellationToken cancelacion);

    /// <summary>La primera empresa activa que haya, o nulo si no hay ninguna.</summary>
    /// <remarks>
    /// Existe para la semilla de arranque, que necesita saber si el sistema está virgen para
    /// decidir si crea la empresa inicial o se apunta a la que ya está. No devuelve la ficha, solo
    /// el identificador: quien pregunta no tiene por qué ver los datos fiscales de nadie.
    /// </remarks>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<Guid?> PrimeraActivaAsync(CancellationToken cancelacion);
}
