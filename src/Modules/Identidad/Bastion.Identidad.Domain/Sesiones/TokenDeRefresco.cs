namespace Bastion.Identidad.Domain.Sesiones;

/// <summary>
/// Un token de refresco emitido a un usuario: el que le permite renovar el acceso sin volver a
/// escribir la contraseña.
/// </summary>
/// <remarks>
/// <para>
/// <b>Aquí se guarda el resumen, no el token.</b> La fila es exactamente igual de sensible que
/// una contraseña —quien la lee puede renovar sesiones ajenas—, así que una lectura de la base de
/// datos no puede bastar para suplantar a nadie. Se guarda un SHA-256 del token; no hace falta un
/// hasher de contraseñas porque el token no lo elige una persona: son 256 bits de aleatoriedad
/// criptográfica, y contra eso un diccionario no sirve de nada.
/// </para>
/// <para>
/// <b>Rotación, y qué significa exactamente.</b> Cada renovación canjea el token presentado y
/// emite uno nuevo que apunta al anterior (<see cref="SustituidoPorId"/>). Un token canjeado no
/// vuelve a valer nunca. Con eso, un token robado tiene una vida útil que acaba en cuanto el
/// dueño legítimo renueva.
/// </para>
/// <para>
/// <b>Detectar la reutilización es lo que convierte la rotación en una defensa.</b> Si alguien
/// presenta un token ya canjeado, solo hay dos explicaciones: o el atacante está usando lo que
/// robó, o el legítimo está usando lo que le robaron y el atacante ya renovó. En los dos casos
/// hay una copia por ahí y no se sabe cuál es cuál, así que la única respuesta segura es
/// <b>revocar la familia entera</b> y obligar a iniciar sesión otra vez. Sin esta detección, la
/// rotación solo acorta la ventana; con ella, la cierra.
/// </para>
/// <para>
/// <b>El token lleva dentro la empresa activa.</b> Renovar no es ocasión de cambiar de empresa:
/// el token nuevo hereda la misma. Cambiar de empresa es otra operación, que comprueba la
/// pertenencia y reemite (§9).
/// </para>
/// </remarks>
public sealed class TokenDeRefresco
{
    private TokenDeRefresco()
    {
        Hash = null!;
    }

    private TokenDeRefresco(
        Guid id,
        Guid usuarioId,
        Guid familiaId,
        Guid empresaActivaId,
        string hash,
        DateTimeOffset creadoEn,
        DateTimeOffset expiraEn)
    {
        Id = id;
        UsuarioId = usuarioId;
        FamiliaId = familiaId;
        EmpresaActivaId = empresaActivaId;
        Hash = hash;
        CreadoEn = creadoEn;
        ExpiraEn = expiraEn;
    }

    /// <summary>Identificador de la emisión.</summary>
    public Guid Id { get; private set; }

    /// <summary>Usuario al que se emitió.</summary>
    public Guid UsuarioId { get; private set; }

    /// <summary>
    /// Cadena de rotaciones a la que pertenece: todas las emisiones que descienden de un mismo
    /// inicio de sesión comparten familia. Es lo que se revoca entero al detectar reutilización.
    /// </summary>
    public Guid FamiliaId { get; private set; }

    /// <summary>Empresa activa que llevaba la sesión al emitirlo (R8).</summary>
    public Guid EmpresaActivaId { get; private set; }

    /// <summary>SHA-256 del token, en hexadecimal. Nunca el token.</summary>
    public string Hash { get; private set; }

    /// <summary>Cuándo se emitió.</summary>
    public DateTimeOffset CreadoEn { get; private set; }

    /// <summary>Cuándo deja de valer por antigüedad.</summary>
    public DateTimeOffset ExpiraEn { get; private set; }

    /// <summary>Cuándo se canjeó por otro, si se canjeó.</summary>
    public DateTimeOffset? CanjeadoEn { get; private set; }

    /// <summary>Emisión que lo sustituyó, si la hubo.</summary>
    public Guid? SustituidoPorId { get; private set; }

    /// <summary>Cuándo se revocó, si se revocó.</summary>
    public DateTimeOffset? RevocadoEn { get; private set; }

    /// <summary>Por qué se revocó.</summary>
    public MotivoDeRevocacion? Motivo { get; private set; }

    /// <summary>Emite un token de refresco.</summary>
    /// <param name="usuarioId">Usuario al que se emite.</param>
    /// <param name="familiaId">Cadena de rotaciones. En un inicio de sesión, una nueva.</param>
    /// <param name="empresaActivaId">Empresa activa de la sesión.</param>
    /// <param name="hash">SHA-256 del token entregado.</param>
    /// <param name="momento">Ahora.</param>
    /// <param name="duracion">Cuánto vale.</param>
    public static TokenDeRefresco Emitir(
        Guid usuarioId,
        Guid familiaId,
        Guid empresaActivaId,
        string hash,
        DateTimeOffset momento,
        TimeSpan duracion)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hash);

        return new TokenDeRefresco(
            Guid.CreateVersion7(),
            usuarioId,
            familiaId,
            empresaActivaId,
            hash,
            momento,
            momento + duracion);
    }

    /// <summary>Si todavía se puede canjear.</summary>
    /// <param name="momento">Ahora.</param>
    public bool EstaVigente(DateTimeOffset momento) =>
        CanjeadoEn is null && RevocadoEn is null && ExpiraEn > momento;

    /// <summary>Si ya se canjeó una vez. Presentarlo otra vez es la señal de robo.</summary>
    public bool EstaCanjeado => CanjeadoEn is not null;

    /// <summary>Marca este token como canjeado por otro.</summary>
    /// <param name="sustitutoId">Emisión que lo sustituye.</param>
    /// <param name="momento">Ahora.</param>
    public void Canjear(Guid sustitutoId, DateTimeOffset momento)
    {
        if (CanjeadoEn is not null)
        {
            throw new InvalidOperationException(
                "Ese token de refresco ya se canjeó. Canjearlo dos veces es justo lo que hay que " +
                "detectar, no lo que hay que permitir.");
        }

        CanjeadoEn = momento;
        SustituidoPorId = sustitutoId;
    }

    /// <summary>Revoca el token. Revocarlo dos veces conserva el primer motivo.</summary>
    /// <param name="motivo">Por qué.</param>
    /// <param name="momento">Ahora.</param>
    public void Revocar(MotivoDeRevocacion motivo, DateTimeOffset momento)
    {
        if (RevocadoEn is not null)
        {
            return;
        }

        RevocadoEn = momento;
        Motivo = motivo;
    }
}
