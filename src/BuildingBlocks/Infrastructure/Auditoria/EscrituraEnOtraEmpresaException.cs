namespace Bastion.BuildingBlocks.Infrastructure.Auditoria;

/// <summary>
/// Alguien ha intentado dar de alta o modificar una fila con la empresa de otro.
/// </summary>
/// <remarks>
/// <para>
/// Es un fallo del <b>programa</b>, no de quien usa el sistema: ninguna petición puede nombrar una
/// empresa —lo comprueba <c>NingunaPeticionNombraLaEmpresaTests</c>—, así que si una fila llega
/// aquí con la empresa cambiada, es que un caso de uso la ha puesto mal. Por eso lanza y no
/// devuelve un <c>Resultado</c>: no hay nada que el cliente pueda hacer distinto, y devolver un
/// <c>400</c> haría pasar por entrada inválida lo que es un defecto.
/// </para>
/// <para>
/// Sale por la política de errores como un <c>500</c> sin contar por dentro qué empresa era: el
/// mensaje es para el registro, no para la respuesta.
/// </para>
/// </remarks>
public sealed class EscrituraEnOtraEmpresaException : InvalidOperationException
{
    /// <summary>Con el detalle de qué se intentaba escribir y dónde.</summary>
    /// <param name="entidad">Tipo de la fila.</param>
    /// <param name="intentada">Empresa que traía la fila.</param>
    /// <param name="activa">Empresa del ámbito en curso.</param>
    public EscrituraEnOtraEmpresaException(string entidad, Guid intentada, Guid activa)
        : base($"Se intentó escribir un '{entidad}' de la empresa {intentada} desde la empresa {activa}.")
    {
        Entidad = entidad;
        Intentada = intentada;
        Activa = activa;
    }

    /// <summary>Sin detalle.</summary>
    public EscrituraEnOtraEmpresaException()
        : this("desconocida", Guid.Empty, Guid.Empty)
    {
    }

    /// <summary>Con un mensaje propio.</summary>
    /// <param name="message">El mensaje.</param>
    public EscrituraEnOtraEmpresaException(string message)
        : base(message)
    {
        Entidad = string.Empty;
    }

    /// <summary>Con un mensaje propio y la causa.</summary>
    /// <param name="message">El mensaje.</param>
    /// <param name="innerException">La causa.</param>
    public EscrituraEnOtraEmpresaException(string message, Exception innerException)
        : base(message, innerException)
    {
        Entidad = string.Empty;
    }

    /// <summary>Tipo de la fila que se intentaba escribir.</summary>
    public string Entidad { get; }

    /// <summary>Empresa que traía la fila.</summary>
    public Guid Intentada { get; }

    /// <summary>Empresa desde la que se estaba operando.</summary>
    public Guid Activa { get; }
}
