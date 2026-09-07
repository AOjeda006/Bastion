using Bastion.BuildingBlocks.Domain.Entidades;
using Bastion.BuildingBlocks.Domain.Identificacion;

namespace Bastion.Terceros.Domain.Terceros;

/// <summary>
/// La persona con la que se habla en casa del tercero: quién es, qué hace allí y por dónde se le
/// localiza.
/// </summary>
/// <remarks>
/// <para>
/// <b>Cada campo pasó las cuatro preguntas antes de existir</b> —finalidad, plazo, quién lo ve y si
/// es de categoría especial— porque esto no es la ficha de una empresa: es la de una <b>persona</b>,
/// y de una que ni siquiera es cliente. Es el empleado de un cliente.
/// </para>
/// <list type="table">
/// <listheader>
///   <term>Campo</term><description>Finalidad · plazo · quién lo ve · categoría especial</description>
/// </listheader>
/// <item>
///   <term><see cref="Nombre"/></term>
///   <description>Saber a quién dirigirse en un pedido o una incidencia · mientras dure la
///   relación comercial, y después lo que dure la prescripción de las obligaciones del contrato ·
///   quien tiene el permiso de terceros de esa empresa · no.</description>
/// </item>
/// <item>
///   <term><see cref="Cargo"/></term>
///   <description>Saber si es la persona adecuada para el asunto —no se le reclama una factura a
///   quien lleva el almacén— · el mismo · el mismo · no. Es un dato del <b>puesto</b>, no de la
///   persona.</description>
/// </item>
/// <item>
///   <term><see cref="Correo"/> y <see cref="Telefono"/></term>
///   <description>Localizar a la empresa a través de quien la atiende · el mismo · el mismo ·
///   no, <b>siempre que sean profesionales</b>. Ver la nota de abajo.</description>
/// </item>
/// </list>
/// <para>
/// <b>La base de los datos de contacto profesionales, escrita.</b> El artículo 19 de la LOPDGDD da
/// por amparado en el interés legítimo el tratamiento de los datos de contacto de quien presta
/// servicios en una persona jurídica, <b>siempre que se refieran a esa actividad</b> y se usen para
/// mantener la relación con ella. Eso cubre el teléfono del departamento de compras y el correo
/// corporativo. <b>No cubre el móvil personal de nadie</b>, ni su correo particular, aunque sea lo
/// único que alguien apuntó: eso es un dato de su vida privada y necesita otra base. El modelo no
/// puede distinguir un número de otro, así que lo que hace es <b>no invitar a guardarlo</b> —un
/// solo teléfono, el del puesto— y decirlo aquí, que es donde lo lee quien añade un campo.
/// </para>
/// <para>
/// <b>Y no hay campo de notas. Es una decisión, no un olvido.</b> Un <c>text</c> libre en la ficha
/// de una persona es donde acaban «está de baja por depresión», «es el yerno del dueño» y «no lo
/// llames los viernes»: datos de salud, de vida familiar y de opinión, o sea justo las categorías
/// especiales que ninguna de las cuatro preguntas de arriba podría contestar. No tiene finalidad
/// declarada, así que no se le puede poner plazo; no tiene estructura, así que no se puede
/// contestar a un derecho de acceso con él; y aparece en los formularios por costumbre, no porque
/// alguien lo haya pedido. Cuando haga falta anotar algo del <b>trato comercial</b>, su sitio es el
/// CRM del §7.11, con su tipo y su plazo.
/// </para>
/// </remarks>
public sealed class Contacto : EntidadBase
{
    /// <summary>Tope del nombre. El mismo que el de la razón social, que es el de la AEAT.</summary>
    public const int LongitudMaximaDeNombre = 120;

    /// <summary>Tope del cargo.</summary>
    public const int LongitudMaximaDeCargo = 60;

    /// <summary>
    /// Tope del teléfono. Caben el prefijo internacional, el número y los separadores con los que
    /// se escribe, y no cabe una lista de tres números en el mismo campo.
    /// </summary>
    public const int LongitudMaximaDeTelefono = 20;

    private Contacto(
        Guid id,
        Guid terceroId,
        string nombre,
        string? cargo,
        Correo? correo,
        string? telefono,
        DateTimeOffset momento)
        : base(momento)
    {
        Id = id;
        TerceroId = terceroId;
        Nombre = nombre;
        Cargo = cargo;
        Correo = correo;
        Telefono = telefono;
    }

    // EF Core materializa desde la base sin pasar por las invariantes.
    private Contacto() => Nombre = null!;

    /// <summary>Identificador del contacto.</summary>
    public Guid Id { get; private set; }

    /// <summary>La ficha a la que pertenece.</summary>
    public Guid TerceroId { get; private set; }

    /// <summary>Nombre de la persona.</summary>
    public string Nombre { get; private set; }

    /// <summary>Qué hace en casa del tercero.</summary>
    public string? Cargo { get; private set; }

    /// <summary>Correo profesional.</summary>
    public Correo? Correo { get; private set; }

    /// <summary>Teléfono profesional.</summary>
    public string? Telefono { get; private set; }

    /// <summary>Da de alta un contacto.</summary>
    /// <param name="terceroId">La ficha a la que se cuelga.</param>
    /// <param name="nombre">Nombre de la persona. Es lo único obligatorio.</param>
    /// <param name="cargo">Qué hace allí.</param>
    /// <param name="correo">Correo profesional.</param>
    /// <param name="telefono">Teléfono profesional.</param>
    /// <param name="momento">Cuándo, del reloj inyectado.</param>
    public static Contacto Crear(
        Guid terceroId,
        string nombre,
        string? cargo,
        Correo? correo,
        string? telefono,
        DateTimeOffset momento)
    {
        if (terceroId == Guid.Empty)
        {
            throw new ArgumentException(
                "Un contacto cuelga siempre de una ficha.", nameof(terceroId));
        }

        return new Contacto(
            Guid.CreateVersion7(),
            terceroId,
            Recortado(nombre, nameof(nombre), LongitudMaximaDeNombre),
            Opcional(cargo, nameof(cargo), LongitudMaximaDeCargo),
            correo,
            Opcional(telefono, nameof(telefono), LongitudMaximaDeTelefono),
            momento);
    }

    /// <summary>Cambia lo que puede cambiar. La ficha a la que cuelga, no.</summary>
    /// <param name="nombre">Nombre de la persona.</param>
    /// <param name="cargo">Qué hace allí.</param>
    /// <param name="correo">Correo profesional.</param>
    /// <param name="telefono">Teléfono profesional.</param>
    public void Modificar(string nombre, string? cargo, Correo? correo, string? telefono)
    {
        Nombre = Recortado(nombre, nameof(nombre), LongitudMaximaDeNombre);
        Cargo = Opcional(cargo, nameof(cargo), LongitudMaximaDeCargo);
        Correo = correo;
        Telefono = Opcional(telefono, nameof(telefono), LongitudMaximaDeTelefono);
    }

    private static string Recortado(string valor, string campo, int longitudMaxima)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(valor, campo);

        string recortado = valor.Trim();

        return recortado.Length <= longitudMaxima
            ? recortado
            : throw new ArgumentException(
                $"«{campo}» admite {longitudMaxima} caracteres como máximo y trae {recortado.Length}.",
                campo);
    }

    private static string? Opcional(string? valor, string campo, int longitudMaxima) =>
        string.IsNullOrWhiteSpace(valor) ? null : Recortado(valor, campo, longitudMaxima);
}
