using Bastion.BuildingBlocks.Domain.Entidades;
using Bastion.BuildingBlocks.Domain.Identificacion;

namespace Bastion.Terceros.Domain.Terceros;

/// <summary>
/// Una cuenta del tercero: por dónde se le paga, o de dónde se le cobra.
/// </summary>
/// <remarks>
/// <para>
/// <b>La cuenta por omisión es una RESTRICCIÓN, no una convención.</b> Un tercero puede tener
/// varias cuentas y como mucho una preferente. Si fueran dos, el fichero de adeudos tendría que
/// elegir, y elegiría por el orden en que salieran de la base — o sea, distinto según el plan de
/// ejecución. Por eso no es una regla de pantalla: es un índice único parcial sobre
/// <c>(tercero_id) WHERE es_preferente</c>, que es donde el motor la aplica pase lo que pase.
/// </para>
/// <para>
/// <b>Y la interacción con el bloqueo se resuelve como en el ítem 1.5: es la misma decisión.</b>
/// Allí, la unicidad de <c>(empresa, identificador)</c> se dejó <b>sin predicado parcial</b>, o sea
/// viendo también las filas bloqueadas, porque una unicidad que se salta lo bloqueado deja crear un
/// duplicado del que solo se entera quien mira el listado del art. 32. Aquí la decisión es la misma
/// y además cuesta menos discutirla: el bloqueo vive en el <b>tercero</b>, no en la cuenta, así que
/// las cuentas que compiten por la preferencia son siempre del mismo tercero y nunca están unas
/// bloqueadas y otras no. El predicado del índice es sobre <c>es_preferente</c> y sobre nada más.
/// </para>
/// <para>
/// <b>El IBAN no viaja a los registros.</b> <see cref="Iban"/> enmascara al convertirse en cadena
/// justo por esto: una cuenta bancaria en el registro de la aplicación se queda ahí para siempre.
/// </para>
/// </remarks>
public sealed class CuentaBancaria : EntidadBase
{
    /// <summary>Un BIC son ocho u once posiciones. Las once llevan la sucursal.</summary>
    public const int LongitudMinimaDeBic = 8;

    /// <summary>Y ni una más.</summary>
    public const int LongitudMaximaDeBic = 11;

    /// <summary>Tope del alias con el que se distingue una cuenta de otra en un desplegable.</summary>
    public const int LongitudMaximaDeAlias = 60;

    private CuentaBancaria(
        Guid id,
        Guid terceroId,
        Iban iban,
        string? bic,
        string? alias,
        bool esPreferente,
        DateTimeOffset momento)
        : base(momento)
    {
        Id = id;
        TerceroId = terceroId;
        Iban = iban;
        Bic = bic;
        Alias = alias;
        EsPreferente = esPreferente;
    }

    // EF Core materializa desde la base sin pasar por las invariantes.
    private CuentaBancaria() => Iban = null!;

    /// <summary>Identificador de la cuenta.</summary>
    public Guid Id { get; private set; }

    /// <summary>La ficha a la que pertenece.</summary>
    public Guid TerceroId { get; private set; }

    /// <summary>El IBAN, validado.</summary>
    public Iban Iban { get; private set; }

    /// <summary>
    /// El BIC de la entidad, si se conoce.
    /// </summary>
    /// <remarks>
    /// Opcional a propósito: dentro de la zona SEPA el BIC dejó de ser obligatorio, y exigirlo
    /// obligaría a inventárselo. Lo que se comprueba es su forma —ocho u once posiciones
    /// alfanuméricas— y no que exista: comprobar que existe es preguntarle al directorio SWIFT, y
    /// eso es una llamada externa que este ítem no trae.
    /// </remarks>
    public string? Bic { get; private set; }

    /// <summary>Cómo se llama esta cuenta en un desplegable. «La del Santander», «la de las nóminas».</summary>
    public string? Alias { get; private set; }

    /// <summary>Si es la que se usa cuando nadie dice otra cosa.</summary>
    public bool EsPreferente { get; private set; }

    /// <summary>Da de alta una cuenta.</summary>
    /// <param name="terceroId">La ficha a la que se cuelga.</param>
    /// <param name="iban">El IBAN, ya validado.</param>
    /// <param name="bic">El BIC, si se conoce.</param>
    /// <param name="alias">Con qué nombre se distingue de las demás.</param>
    /// <param name="esPreferente">Si es la de por omisión.</param>
    /// <param name="momento">Cuándo, del reloj inyectado.</param>
    public static CuentaBancaria Crear(
        Guid terceroId,
        Iban iban,
        string? bic,
        string? alias,
        bool esPreferente,
        DateTimeOffset momento)
    {
        ArgumentNullException.ThrowIfNull(iban);

        if (terceroId == Guid.Empty)
        {
            throw new ArgumentException(
                "Una cuenta cuelga siempre de una ficha.", nameof(terceroId));
        }

        return new CuentaBancaria(
            Guid.CreateVersion7(),
            terceroId,
            iban,
            BicValido(bic),
            Opcional(alias, nameof(alias), LongitudMaximaDeAlias),
            esPreferente,
            momento);
    }

    /// <summary>Cambia lo que puede cambiar.</summary>
    /// <remarks>
    /// El IBAN <b>no</b> está entre ello, igual que el identificador fiscal del tercero: una
    /// cuenta distinta es otra cuenta. Cambiarlo en la fila existente dejaría los adeudos ya
    /// presentados apuntando a un número que no era el suyo.
    /// </remarks>
    /// <param name="bic">El BIC, si se conoce.</param>
    /// <param name="alias">Con qué nombre se distingue de las demás.</param>
    public void Modificar(string? bic, string? alias)
    {
        Bic = BicValido(bic);
        Alias = Opcional(alias, nameof(alias), LongitudMaximaDeAlias);
    }

    /// <summary>La hace preferente, o deja de serlo.</summary>
    /// <remarks>
    /// Quien coordina que solo haya una es el caso de uso, que baja la anterior en la misma
    /// transacción; quien lo garantiza es el índice único. Los dos, y no uno de los dos: sin el
    /// caso de uso, marcar una segunda revienta con un choque de índice en la cara del usuario; sin
    /// el índice, dos peticiones a la vez dejan dos preferentes y nadie se entera.
    /// </remarks>
    /// <param name="preferente">Si pasa a ser la de por omisión.</param>
    public void MarcarPreferente(bool preferente) => EsPreferente = preferente;

    private static string? BicValido(string? bic)
    {
        if (string.IsNullOrWhiteSpace(bic))
        {
            return null;
        }

        string limpio = bic.Trim().ToUpperInvariant();

        return limpio.Length is LongitudMinimaDeBic or LongitudMaximaDeBic
            && limpio.All(char.IsAsciiLetterOrDigit)
            ? limpio
            : throw new ArgumentException(
                $"Un BIC tiene {LongitudMinimaDeBic} u {LongitudMaximaDeBic} posiciones " +
                $"alfanuméricas, y este trae {limpio.Length}.",
                nameof(bic));
    }

    private static string? Opcional(string? valor, string campo, int longitudMaxima)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return null;
        }

        string recortado = valor.Trim();

        return recortado.Length <= longitudMaxima
            ? recortado
            : throw new ArgumentException(
                $"«{campo}» admite {longitudMaxima} caracteres como máximo y trae {recortado.Length}.",
                campo);
    }
}
