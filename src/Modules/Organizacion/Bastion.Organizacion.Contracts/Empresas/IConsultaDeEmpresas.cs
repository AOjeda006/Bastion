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

    /// <summary>La razón social de cada una de esas empresas, para las que sigan activas.</summary>
    /// <remarks>
    /// <para>
    /// Existe para el <b>selector de empresa</b> del 0.11: la sesión devuelve a qué empresas
    /// pertenece quien acaba de entrar, y un desplegable de identificadores no es un selector. El
    /// nombre tiene que salir de aquí y no de <c>GET /organizacion/empresas</c>, porque ese
    /// endpoint exige el permiso <c>organizacion.empresa.ver</c> y <b>pertenecer a varias empresas
    /// no implica poder ver la ficha de ninguna</b>.
    /// </para>
    /// <para>
    /// Devuelve la razón social y nada más. No es la ficha: ni NIF, ni domicilio fiscal, ni
    /// ejercicios — quien pregunta solo necesita poder escribir el nombre en una lista.
    /// </para>
    /// <para>
    /// <b>Las que estén bloqueadas no salen</b>, y no por omisión: es el filtro de R16 haciendo su
    /// trabajo una vez más. Una empresa suprimida al amparo del art. 32 desaparece del selector
    /// igual que desapareció del listado, y quien la tuviera en sus pertenencias simplemente ve una
    /// opción menos. Por eso el resultado se indexa por identificador en vez de devolver una lista
    /// paralela a la de entrada: <b>puede traer menos elementos de los que se pidieron</b>, y quien
    /// llama tiene que poder notarlo.
    /// </para>
    /// </remarks>
    /// <param name="empresaIds">Identificadores por los que se pregunta.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<IReadOnlyDictionary<Guid, string>> RazonesSocialesDeAsync(
        IReadOnlyCollection<Guid> empresaIds,
        CancellationToken cancelacion);
}
