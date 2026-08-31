namespace Bastion.BuildingBlocks.Domain.Bloqueos;

/// <summary>
/// El estado de bloqueo de una entidad, con su fecha y su motivo (R16).
/// </summary>
/// <remarks>
/// <para>
/// <b>Suprimir no es borrar: es bloquear.</b> El artículo 32 de la LOPDGDD obliga a identificar y
/// reservar los datos cuando procede su supresión, impidiendo su tratamiento —incluida la
/// visualización— salvo para jueces, Fiscalía y Administraciones competentes, y solo durante el
/// plazo de prescripción. De ahí las tres piezas: que esté bloqueado, <b>desde cuándo</b> —porque
/// de esa fecha cuelga el plazo— y <b>por qué</b>.
/// </para>
/// <para>
/// <b>Estaba escrito tres veces.</b> Hasta el 0.10 había un <c>EstadoDeEmpresa</c>, un
/// <c>EstadoDeAlmacen</c> y un <c>EstadoDeUsuario</c>, los tres con dos valores, los tres con su
/// <c>BloqueadoEn</c> al lado y los tres con su <c>Bloquear</c>/<c>Desbloquear</c> copiado. Tres
/// copias de una regla legal son tres sitios donde la regla puede divergir, y ninguna de las tres
/// llevaba motivo. Esta es la versión escrita para el caso general; las de antes las escribió
/// quien tenía prisa.
/// </para>
/// <para>
/// <b>Es un objeto de valor, no un enumerado.</b> Un enumerado dice que está bloqueado y deja la
/// fecha y el motivo sueltos al lado, donde nada obliga a que estén puestos cuando el estado lo
/// dice ni vacíos cuando no. Aquí las tres van juntas y las combinaciones imposibles no se pueden
/// construir: no hay bloqueo sin fecha ni motivo, y no hay fecha ni motivo sin bloqueo.
/// </para>
/// </remarks>
public sealed record Bloqueo
{
    private Bloqueo(bool estaBloqueado, DateTimeOffset? desde, MotivoDeBloqueo? motivo)
    {
        EstaBloqueado = estaBloqueado;
        Desde = desde;
        Motivo = motivo;
    }

    // EF Core materializa el tipo complejo sin pasar por las invariantes: lo que está guardado ya
    // pasó por ellas.
    private Bloqueo()
    {
    }

    /// <summary>Si la entidad está bloqueada ahora mismo.</summary>
    public bool EstaBloqueado { get; private set; }

    /// <summary>
    /// Instante del bloqueo, o <c>null</c> si no lo está. De aquí arranca el plazo de
    /// prescripción, así que es un instante con zona horaria y no una fecha.
    /// </summary>
    public DateTimeOffset? Desde { get; private set; }

    /// <summary>Por qué se bloqueó, o <c>null</c> si no lo está.</summary>
    public MotivoDeBloqueo? Motivo { get; private set; }

    /// <summary>El estado de quien no está bloqueado.</summary>
    /// <remarks>
    /// Es un método y no una constante compartida a propósito: EF Core sigue un tipo complejo por
    /// valor <b>dentro</b> de la entidad que lo contiene, y una única instancia repartida entre
    /// todas las entidades del modelo sería una instancia compartida entre agregados distintos.
    /// Sale gratis devolver una nueva y evita una clase entera de sorpresas.
    /// </remarks>
    public static Bloqueo Ninguno() => new(false, null, null);

    /// <summary>Bloquea, si no lo estaba ya.</summary>
    /// <remarks>
    /// <b>Bloquear dos veces devuelve el bloqueo que ya había, sin tocarlo.</b> No mueve la fecha
    /// —de ella cuelga el plazo del artículo 32, y rebloquear alargaría la conservación sin que
    /// nadie lo hubiera decidido— y tampoco reemplaza el motivo: el que vale es el que justificó
    /// la reserva. Que sea idempotente no es comodidad, es el contrato del verbo que lo provoca:
    /// un <c>DELETE</c> repetido tiene que dar el mismo resultado que uno solo.
    /// </remarks>
    /// <param name="motivo">Por qué se bloquea. Va a la columna y a la traza.</param>
    /// <param name="momento">Ahora.</param>
    public Bloqueo Bloquear(MotivoDeBloqueo motivo, DateTimeOffset momento) =>
        EstaBloqueado ? this : new Bloqueo(true, momento, motivo);

    /// <summary>Levanta el bloqueo.</summary>
    /// <remarks>
    /// <b>Desbloquear lo que no está bloqueado no es un error: devuelve lo mismo.</b> La
    /// postcondición —«esto no está bloqueado»— ya se cumple, y lanzar obligaría a todo el que
    /// llame a preguntar antes. Se borran la fecha y el motivo, que es lo coherente con que las
    /// tres piezas viajen juntas: dejar la fecha del bloqueo anterior en una entidad activa sería
    /// exactamente el estado imposible que este tipo existe para no permitir.
    /// </remarks>
    public Bloqueo Desbloquear() => EstaBloqueado ? Ninguno() : this;

    /// <summary>Exige que no esté bloqueado, y si lo está, explica por qué eso importa.</summary>
    /// <remarks>
    /// <b>La comprobación es la misma en las tres entidades; el motivo, no.</b> En una empresa y
    /// en un usuario es el artículo 32 de la LOPDGDD: modificar un dato bloqueado es tratarlo. En
    /// un almacén es el histórico de valoración, que apunta a él para siempre. Por eso la frase se
    /// compone aquí y la razón la pone quien llama: unificar también el porqué habría metido una
    /// cita legal en el mensaje de un almacén, que no le corresponde.
    /// </remarks>
    /// <param name="sujeto">
    /// El sujeto de la frase, ya concordado: «Una empresa bloqueada», «El almacén FAC, bloqueado,».
    /// Se pide completo porque en castellano el género lo pone la entidad, y componerlo aquí con un
    /// adjetivo fijo escribiría «Un almacén bloqueada».
    /// </param>
    /// <param name="porque">Por qué esa entidad no admite cambios estando bloqueada.</param>
    /// <exception cref="InvalidOperationException">Si está bloqueada.</exception>
    public void ExigirQueNoEsteBloqueado(string sujeto, string porque)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sujeto);
        ArgumentException.ThrowIfNullOrWhiteSpace(porque);

        if (EstaBloqueado)
        {
            throw new InvalidOperationException($"{sujeto} no admite cambios: {porque}.");
        }
    }
}
