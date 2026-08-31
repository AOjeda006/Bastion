using Bastion.BuildingBlocks.Domain.Bloqueos;
using Bastion.BuildingBlocks.Domain.Entidades;
using Bastion.BuildingBlocks.Domain.Identificacion;

namespace Bastion.Identidad.Domain.Usuarios;

/// <summary>
/// Una cuenta de usuario: quién es, cómo se comprueba su contraseña, en qué empresas está y con
/// qué roles.
/// </summary>
/// <remarks>
/// <para>
/// <b>Aquí no hay ni una contraseña, solo su resumen.</b> El agregado recibe el hash ya
/// calculado y nunca ve el texto: quién sabe hashear es Infrastructure, que es donde vive la
/// decisión de con qué algoritmo (ADR-0008). Si el dominio recibiera la contraseña en claro,
/// pasaría por la pila de llamadas, por los mensajes de excepción y por cualquier registro que
/// serializara un objeto de dominio.
/// </para>
/// <para>
/// <b>Dos bloqueos distintos que no se pueden confundir.</b>
/// <see cref="Bloqueo"/> es el bloqueo de R16: baja lógica, la decide una persona con permiso,
/// lleva fecha y motivo y no caduca sola. <see cref="RechazadoHasta"/> es el rechazo por intentos
/// fallidos: automático, temporal y se levanta solo. Mezclarlos en un campo haría que un ataque de
/// fuerza bruta diera de baja la cuenta —que es exactamente el favor que el atacante quería— o que
/// dar de baja una cuenta caducara al cabo de un rato. Siguen siendo dos cosas separadas después
/// del 0.10, y hay una prueba que se pone roja si alguien las junta.
/// </para>
/// </remarks>
public sealed class Usuario : EntidadBase, IBloqueable
{
    /// <summary>Intentos fallidos seguidos que se toleran antes de rechazar.</summary>
    /// <remarks>
    /// Cinco, con quince minutos de espera. Es el equilibrio habitual: frena la fuerza bruta
    /// —cinco pruebas por cuarto de hora no llegan a ninguna parte— sin dejar fuera a quien
    /// simplemente no acierta con cuál de sus contraseñas era.
    /// </remarks>
    public const int IntentosTolerados = 5;

    /// <summary>Cuánto dura el rechazo por intentos fallidos.</summary>
    public static readonly TimeSpan EsperaTrasIntentosFallidos = TimeSpan.FromMinutes(15);

    private readonly List<Membresia> _membresias = [];

    private Usuario()
    {
        Correo = null!;
        Nombre = null!;
        HashDeContrasena = null!;
        Bloqueo = null!;
    }

    private Usuario(Guid id, Correo correo, string nombre, string hashDeContrasena, DateTimeOffset momento)
        : base(momento)
    {
        Id = id;
        Correo = correo;
        Nombre = nombre;
        HashDeContrasena = hashDeContrasena;
        Bloqueo = Bloqueo.Ninguno();
    }

    /// <summary>Identificador del usuario. Es lo que viaja en el <i>claim</i> del sujeto.</summary>
    public Guid Id { get; private set; }

    /// <summary>Correo, que es con lo que inicia sesión.</summary>
    public Correo Correo { get; private set; }

    /// <summary>Nombre con el que se le llama en la interfaz.</summary>
    public string Nombre { get; private set; }

    /// <summary>Resumen de la contraseña. Nunca la contraseña.</summary>
    public string HashDeContrasena { get; private set; }

    /// <inheritdoc/>
    public Bloqueo Bloqueo { get; private set; }

    /// <summary>Último inicio de sesión correcto, si ha habido alguno.</summary>
    public DateTimeOffset? UltimoAccesoEn { get; private set; }

    /// <summary>Intentos fallidos seguidos desde el último acierto.</summary>
    public int IntentosFallidos { get; private set; }

    /// <summary>Hasta cuándo se rechazan sus intentos, si está rechazado.</summary>
    public DateTimeOffset? RechazadoHasta { get; private set; }

    /// <summary>Empresas a las que pertenece, con sus roles.</summary>
    public IReadOnlyCollection<Membresia> Membresias => _membresias;

    /// <summary>Da de alta una cuenta.</summary>
    /// <param name="correo">Correo con el que iniciará sesión.</param>
    /// <param name="nombre">Nombre para la interfaz.</param>
    /// <param name="hashDeContrasena">Resumen de la contraseña, ya calculado.</param>
    /// <param name="momento">Ahora.</param>
    public static Usuario Crear(Correo correo, string nombre, string hashDeContrasena, DateTimeOffset momento)
    {
        ArgumentNullException.ThrowIfNull(correo);
        ArgumentException.ThrowIfNullOrWhiteSpace(nombre);
        ArgumentException.ThrowIfNullOrWhiteSpace(hashDeContrasena);

        return new Usuario(Guid.CreateVersion7(), correo, nombre.Trim(), hashDeContrasena, momento);
    }

    /// <summary>Cambia el nombre para la interfaz.</summary>
    /// <param name="nombre">Nombre nuevo.</param>
    public void Renombrar(string nombre)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nombre);

        Nombre = nombre.Trim();
    }

    /// <summary>
    /// Cambia la contraseña y borra el rastro de los intentos fallidos.
    /// </summary>
    /// <remarks>
    /// El contador se reinicia porque quien acaba de demostrar que puede cambiar la contraseña ya
    /// no es el desconocido que estaba probando; dejarlo rechazado sería castigar al dueño de la
    /// cuenta por el ataque que ha sufrido.
    /// </remarks>
    /// <param name="hashDeContrasena">Resumen de la contraseña nueva.</param>
    public void CambiarContrasena(string hashDeContrasena)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hashDeContrasena);

        HashDeContrasena = hashDeContrasena;
        IntentosFallidos = 0;
        RechazadoHasta = null;
    }

    /// <inheritdoc/>
    /// <remarks>Da de baja la cuenta (R16). No la borra: un usuario es una persona física.</remarks>
    public void Bloquear(MotivoDeBloqueo motivo, DateTimeOffset momento) =>
        Bloqueo = Bloqueo.Bloquear(motivo, momento);

    /// <inheritdoc/>
    public void Desbloquear() => Bloqueo = Bloqueo.Desbloquear();

    /// <summary>Si la cuenta está rechazando intentos ahora mismo.</summary>
    /// <param name="momento">Ahora.</param>
    public bool EstaRechazado(DateTimeOffset momento) =>
        RechazadoHasta is { } hasta && hasta > momento;

    /// <summary>Si la cuenta puede iniciar sesión en este momento.</summary>
    /// <param name="momento">Ahora.</param>
    /// <remarks>
    /// Las dos condiciones son las dos que hay, y son distintas: la cuenta no está dada de baja
    /// (R16) y no está rechazando intentos ahora mismo. Fundirlas sería fundir los dos bloqueos.
    /// </remarks>
    public bool PuedeIniciarSesion(DateTimeOffset momento) =>
        !Bloqueo.EstaBloqueado && !EstaRechazado(momento);

    /// <summary>Apunta un intento fallido y rechaza la cuenta si se pasa del tope.</summary>
    /// <param name="momento">Ahora.</param>
    public void RegistrarIntentoFallido(DateTimeOffset momento)
    {
        IntentosFallidos++;

        if (IntentosFallidos >= IntentosTolerados)
        {
            RechazadoHasta = momento + EsperaTrasIntentosFallidos;
        }
    }

    /// <summary>Apunta un inicio de sesión correcto y borra el rastro de los fallidos.</summary>
    /// <param name="momento">Ahora.</param>
    public void RegistrarAccesoCorrecto(DateTimeOffset momento)
    {
        IntentosFallidos = 0;
        RechazadoHasta = null;
        UltimoAccesoEn = momento;
    }

    /// <summary>Le da de alta en una empresa. Repetirlo devuelve la pertenencia que ya había.</summary>
    /// <param name="empresaId">Empresa, comprobada antes contra Organización.</param>
    public Membresia Conceder(Guid empresaId)
    {
        Membresia? existente = EnEmpresa(empresaId);

        if (existente is not null)
        {
            return existente;
        }

        var membresia = new Membresia(Id, empresaId);
        _membresias.Add(membresia);
        return membresia;
    }

    /// <summary>Le da de baja de una empresa, con sus roles ahí.</summary>
    /// <param name="empresaId">Empresa.</param>
    /// <returns>Si pertenecía.</returns>
    public bool Retirar(Guid empresaId) =>
        _membresias.RemoveAll(membresia => membresia.EmpresaId == empresaId) > 0;

    /// <summary>Su pertenencia a esa empresa, o nulo si no pertenece.</summary>
    /// <param name="empresaId">Empresa.</param>
    public Membresia? EnEmpresa(Guid empresaId) =>
        _membresias.Find(membresia => membresia.EmpresaId == empresaId);

    /// <summary>Si pertenece a esa empresa.</summary>
    /// <param name="empresaId">Empresa.</param>
    public bool PerteneceA(Guid empresaId) => EnEmpresa(empresaId) is not null;
}
