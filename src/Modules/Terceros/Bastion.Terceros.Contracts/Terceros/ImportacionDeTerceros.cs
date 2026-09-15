namespace Bastion.Terceros.Contracts.Terceros;

/// <summary>
/// El contrato del fichero con el que se importan terceros: sus dieciocho columnas y sus dos topes
/// (ADR-0034 §3 y §5).
/// </summary>
/// <remarks>
/// <para>
/// <b>La cabecera es contrato publicado</b>, igual que un campo de un DTO: quien prepara el fichero
/// la copia, y cambiar un nombre rompe los ficheros que ya existen. Los nombres son los de los campos
/// del alta de un tercero, en minúsculas y con guion bajo, que es lo que se escribe sin tildes en una
/// hoja de cálculo sin que ninguna configuración regional lo toque.
/// </para>
/// <para>
/// <b>Los topes están aquí y no solo donde se imponen</b> para que los lea quien prepara el fichero,
/// y para que la acción y el caso de uso los tomen del mismo sitio.
/// </para>
/// </remarks>
public static class ImportacionDeTerceros
{
    /// <summary>Tope del fichero, en bytes: 2 MiB. Lo impone el borde mientras lee.</summary>
    public const long TopeDeBytes = 2 * 1024 * 1024;

    /// <summary>Tope de filas detrás de la cabecera, contando las vacías.</summary>
    public const int TopeDeFilas = 5000;

    /// <summary>País del identificador fiscal, en ISO 3166-1 alfa-2.</summary>
    public const string IdentificacionPais = "identificacion_pais";

    /// <summary>El identificador fiscal.</summary>
    public const string IdentificacionNumero = "identificacion_numero";

    /// <summary>Razón social.</summary>
    public const string RazonSocial = "razon_social";

    /// <summary>Nombre comercial; puede ir vacío.</summary>
    public const string NombreComercial = "nombre_comercial";

    /// <summary>Calle del domicilio fiscal.</summary>
    public const string DomicilioCalle = "domicilio_calle";

    /// <summary>Número del domicilio fiscal; puede ir vacío.</summary>
    public const string DomicilioNumero = "domicilio_numero";

    /// <summary>Código postal del domicilio fiscal.</summary>
    public const string DomicilioCodigoPostal = "domicilio_codigo_postal";

    /// <summary>Población del domicilio fiscal.</summary>
    public const string DomicilioPoblacion = "domicilio_poblacion";

    /// <summary>Provincia o subdivisión del domicilio fiscal; puede ir vacía.</summary>
    public const string DomicilioSubdivision = "domicilio_subdivision";

    /// <summary>País del domicilio fiscal, en ISO 3166-1 alfa-2.</summary>
    public const string DomicilioPais = "domicilio_pais";

    /// <summary>Si es cliente: sí o no.</summary>
    public const string EsCliente = "es_cliente";

    /// <summary>Si es proveedor: sí o no.</summary>
    public const string EsProveedor = "es_proveedor";

    /// <summary>Territorio fiscal; vacío es península y Baleares.</summary>
    public const string Territorio = "territorio";

    /// <summary>Recargo de equivalencia: sí o no.</summary>
    public const string RecargoDeEquivalencia = "recargo_de_equivalencia";

    /// <summary>Criterio de caja: sí o no.</summary>
    public const string CriterioDeCaja = "criterio_de_caja";

    /// <summary>Sujeto a retención de IRPF: sí o no.</summary>
    public const string SujetoARetencionIrpf = "sujeto_a_retencion_irpf";

    /// <summary>Límite de crédito, con coma decimal; vacío es sin límite.</summary>
    public const string LimiteCredito = "limite_credito";

    /// <summary>Divisa del límite de crédito, en ISO 4217; va si y solo si va el límite.</summary>
    public const string LimiteCreditoDivisa = "limite_credito_divisa";

    /// <summary>Las dieciocho columnas, en el orden en que tiene que traerlas la primera fila.</summary>
    public static IReadOnlyList<string> Cabecera { get; } =
    [
        IdentificacionPais,
        IdentificacionNumero,
        RazonSocial,
        NombreComercial,
        DomicilioCalle,
        DomicilioNumero,
        DomicilioCodigoPostal,
        DomicilioPoblacion,
        DomicilioSubdivision,
        DomicilioPais,
        EsCliente,
        EsProveedor,
        Territorio,
        RecargoDeEquivalencia,
        CriterioDeCaja,
        SujetoARetencionIrpf,
        LimiteCredito,
        LimiteCreditoDivisa,
    ];
}
