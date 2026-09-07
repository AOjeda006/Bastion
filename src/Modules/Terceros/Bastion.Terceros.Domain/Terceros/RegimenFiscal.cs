namespace Bastion.Terceros.Domain.Terceros;

/// <summary>
/// El régimen fiscal del tercero: dónde tributa y bajo qué tres condiciones especiales.
/// </summary>
/// <remarks>
/// <para>
/// <b>El campo sí, el comportamiento no</b>, y el corte se sostiene por un motivo que no es
/// «todavía no toca»: los cuatro datos de aquí son <b>hechos declarados del tercero</b>, no
/// resultados de un cálculo. Que un minorista esté en recargo de equivalencia o que un profesional
/// esté acogido al criterio de caja es algo que él dice y que su ficha registra, exactamente igual
/// que su domicilio. La regla que convierte esos hechos en una línea de cuota es de la fase 3.
/// </para>
/// <para>
/// <b>Lo que se ha quedado fuera, y por qué.</b> El §7.2 pide también «si procede retención de
/// IRPF <i>y a qué tipo</i>». El <b>tipo</b> no cabe aquí, y no por falta de sitio: ese porcentaje
/// ya tiene dueño desde el ítem 1.2. Es un <c>Impuesto</c> de Organización con
/// <c>TipoDeImpuesto.Retencion</c>, con su <c>Porcentaje</c> en <c>numeric</c>, su ventana de
/// vigencia y sus cuentas contables. Un <c>decimal</c> suelto en esta ficha sería una segunda copia
/// de un maestro, sin vigencia, que se queda vieja el día que el tipo general cambie — y el tipo
/// aplicable depende del tercero (el 7 % del profesional recién dado de alta frente al 15 %
/// general), así que lo correcto es una <b>referencia</b> al impuesto, y una referencia a otro
/// módulo obliga a declarar el cruce con su puerto (ADR-0024). Eso es más que «el campo y nada
/// más», así que se queda fuera del ítem con su motivo escrito.
/// </para>
/// <para>
/// Lo que sí entra es <see cref="SujetoARetencionIrpf"/>, que <b>es</b> un hecho del tercero: que
/// sus facturas llevan retención. Cuál es el tipo lo dice el impuesto, no la ficha.
/// </para>
/// <para>
/// Es un valor, no una entidad: no tiene identidad ni vida fuera de la ficha, así que se mapea
/// como tipo complejo en las columnas del propio tercero (ADR-0016).
/// </para>
/// </remarks>
public sealed record RegimenFiscal
{
    private RegimenFiscal(
        TerritorioFiscal territorio,
        bool recargoDeEquivalencia,
        bool criterioDeCaja,
        bool sujetoARetencionIrpf)
    {
        Territorio = territorio;
        RecargoDeEquivalencia = recargoDeEquivalencia;
        CriterioDeCaja = criterioDeCaja;
        SujetoARetencionIrpf = sujetoARetencionIrpf;
    }

    /// <summary>Dónde tributa.</summary>
    public TerritorioFiscal Territorio { get; private set; }

    /// <summary>
    /// Si es un minorista en recargo de equivalencia.
    /// </summary>
    /// <remarks>
    /// El recargo <b>añade</b> una línea de cuota, no la quita (§7.2). Es la confusión más cara del
    /// régimen y está escrita aquí para que quien implemente el cálculo no tenga que deducirla.
    /// </remarks>
    public bool RecargoDeEquivalencia { get; private set; }

    /// <summary>Si está acogido al régimen especial del criterio de caja.</summary>
    /// <remarks>
    /// Cambia <b>cuándo</b> se devenga el IVA —al cobrar, no al emitir—, y alcanza también a quien
    /// recibe la factura de alguien acogido. Es un hecho registral: se está en el régimen o no.
    /// </remarks>
    public bool CriterioDeCaja { get; private set; }

    /// <summary>Si sus facturas llevan retención de IRPF.</summary>
    /// <remarks>
    /// El <b>tipo</b> no está aquí: ver la nota de la clase.
    /// </remarks>
    public bool SujetoARetencionIrpf { get; private set; }

    /// <summary>El régimen de quien tributa en el territorio común y sin especialidades.</summary>
    /// <remarks>
    /// Es el de la inmensa mayoría de las fichas, y por eso existe: obligar a nombrar cuatro
    /// valores en cada alta ordinaria hace que se copien sin mirarlos.
    /// </remarks>
    public static RegimenFiscal Comun() =>
        new(TerritorioFiscal.PeninsulaYBaleares, false, false, false);

    /// <summary>Declara el régimen.</summary>
    /// <param name="territorio">Dónde tributa.</param>
    /// <param name="recargoDeEquivalencia">Si es minorista en recargo.</param>
    /// <param name="criterioDeCaja">Si está acogido al criterio de caja.</param>
    /// <param name="sujetoARetencionIrpf">Si sus facturas llevan retención.</param>
    public static RegimenFiscal De(
        TerritorioFiscal territorio,
        bool recargoDeEquivalencia,
        bool criterioDeCaja,
        bool sujetoARetencionIrpf)
    {
        if (!Enum.IsDefined(territorio))
        {
            throw new ArgumentOutOfRangeException(
                nameof(territorio),
                territorio,
                "El territorio fiscal tiene que ser uno de los cinco del §7.2.");
        }

        return new RegimenFiscal(
            territorio, recargoDeEquivalencia, criterioDeCaja, sujetoARetencionIrpf);
    }

    // EF Core materializa el tipo complejo sin pasar por la fábrica.
    private RegimenFiscal()
    {
    }
}
