using Bastion.BuildingBlocks.Application.Importacion;
using Bastion.BuildingBlocks.Application.Validacion;
using Bastion.BuildingBlocks.Contracts.Direcciones;
using Bastion.BuildingBlocks.Contracts.Importacion;
using Bastion.BuildingBlocks.Domain.Dinero;
using Bastion.Terceros.Application.Comun;
using Bastion.Terceros.Contracts.Terceros;
using Bastion.Terceros.Domain.Terceros;
using static Bastion.Terceros.Contracts.Terceros.ImportacionDeTerceros;

namespace Bastion.Terceros.Application.Terceros;

/// <summary>
/// Decide una fila del fichero de terceros: la lee, la valida con las mismas reglas que el alta por
/// JSON, y apunta por qué no vale. No escribe nada (ADR-0034 §1).
/// </summary>
/// <remarks>
/// <para>
/// <b>Todo lo que puede rechazar una fila se comprueba aquí, leyendo</b>, porque la escritura es una
/// para el fichero entero: si una fila pudiera fallar al escribir por algo suyo, se llevaría por
/// delante a las buenas. Por eso se comprueban también las invariantes que en el alta por JSON solo
/// defiende el dominio lanzando —el país del domicilio—: allí un fallo es la respuesta de una
/// petición; aquí sería la de cinco mil.
/// </para>
/// <para>
/// <b>El orden en que se apunta es el de lo más básico a lo más elaborado</b> —comillas, formato,
/// contrato, regla—, y cada columna de cada fila se queda con el primero. Una identificación vacía
/// es <c>obligatorio</c>; que además no sea un NIF válido no ayuda a nadie a arreglarla.
/// </para>
/// </remarks>
internal static class FilasDeTerceros
{
    // Del nombre con que los errores por campo y las anotaciones nombran un campo del alta, a la
    // columna del fichero. Un campo que no esté aquí es un fallo de programación y lanza: el informe
    // no puede decir una columna que el fichero no tiene, ni callarse un motivo.
    private static readonly Dictionary<string, string> s_columnaDelCampo = new(StringComparer.Ordinal)
    {
        ["identificacion.pais"] = IdentificacionPais,
        ["identificacion.numero"] = IdentificacionNumero,
        ["razonSocial"] = RazonSocial,
        ["nombreComercial"] = NombreComercial,
        ["domicilioFiscal.calle"] = DomicilioCalle,
        ["domicilioFiscal.numero"] = DomicilioNumero,
        ["domicilioFiscal.codigoPostal"] = DomicilioCodigoPostal,
        ["domicilioFiscal.poblacion"] = DomicilioPoblacion,
        ["domicilioFiscal.subdivision"] = DomicilioSubdivision,
        ["domicilioFiscal.pais"] = DomicilioPais,
        ["regimenFiscal.territorio"] = Territorio,
        ["cantidad"] = LimiteCredito,
        ["divisa"] = LimiteCreditoDivisa,
    };

    /// <summary>Si la fila trae algo en alguna de las dos columnas del límite de crédito.</summary>
    /// <param name="fila">La fila.</param>
    internal static bool TraeLimite(FilaCsv fila) =>
        fila.CuadraConLaCabecera
        && (fila.Campos[IndiceDe(LimiteCredito)].Length > 0 || fila.Campos[IndiceDe(LimiteCreditoDivisa)].Length > 0);

    /// <summary>Lee y valida una fila, apuntando en <paramref name="rechazos"/> lo que no vale.</summary>
    /// <param name="fila">La fila, tal como la ha partido el lector.</param>
    /// <param name="rechazos">Dónde se apuntan los motivos.</param>
    /// <returns>
    /// La identificación, si se ha podido leer —para buscar repetidas y existentes aunque la fila tenga
    /// otros motivos—, y el alta, solo si la fila no tiene ninguno.
    /// </returns>
    internal static FilaDecidida Decidir(FilaCsv fila, RechazosDeImportacion rechazos)
    {
        if (!fila.CuadraConLaCabecera)
        {
            // Con otro número de campos no se sabe qué campo es qué columna, así que no se mira
            // ninguno: cualquier otro motivo sería de la columna equivocada.
            rechazos.Apuntar(fila.Linea, null, MotivoDeRechazo.NumeroDeCamposDistinto);
            return new FilaDecidida(fila.Linea, null, null);
        }

        var lectura = new Lectura(fila, rechazos);

        foreach (int indice in fila.ComillasMalColocadas.Order())
        {
            lectura.Apuntar(Cabecera[indice], MotivoDeRechazo.ComillasMalColocadas);
        }

        bool? esCliente = lectura.SiNo(EsCliente);
        bool? esProveedor = lectura.SiNo(EsProveedor);
        bool? recargo = lectura.SiNo(RecargoDeEquivalencia);
        bool? criterioDeCaja = lectura.SiNo(CriterioDeCaja);
        bool? retencion = lectura.SiNo(SujetoARetencionIrpf);
        decimal? cantidad = lectura.Importe(LimiteCredito);

        var datos = new CrearTerceroDto
        {
            Identificacion = new IdentificacionDeAltaDto
            {
                Pais = lectura.Texto(IdentificacionPais),
                Numero = lectura.Texto(IdentificacionNumero),
            },
            RazonSocial = lectura.Texto(RazonSocial),
            NombreComercial = lectura.Opcional(NombreComercial),
            DomicilioFiscal = new DireccionDto
            {
                Calle = lectura.Texto(DomicilioCalle),
                Numero = lectura.Opcional(DomicilioNumero),
                CodigoPostal = lectura.Texto(DomicilioCodigoPostal),
                Poblacion = lectura.Texto(DomicilioPoblacion),
                Subdivision = lectura.Opcional(DomicilioSubdivision),
                Pais = lectura.Texto(DomicilioPais),
            },
            EsCliente = esCliente ?? false,
            EsProveedor = esProveedor ?? false,
            RegimenFiscal = new RegimenFiscalDeAltaDto
            {
                // Vacío es el de por omisión, igual que no mandar el campo en JSON.
                Territorio = lectura.Opcional(Territorio) ?? new RegimenFiscalDeAltaDto().Territorio,
                RecargoDeEquivalencia = recargo ?? false,
                CriterioDeCaja = criterioDeCaja ?? false,
                SujetoARetencionIrpf = retencion ?? false,
            },
        };

        var limite = new LimiteCreditoDeAltaDto
        {
            Cantidad = cantidad,
            Divisa = lectura.Opcional(LimiteCreditoDivisa),
        };

        IEnumerable<(string Campo, MotivoDeRechazo Motivo)> delContrato = ContratoDeLaFila.Incumplimientos(datos, string.Empty)
            .Concat(ContratoDeLaFila.Incumplimientos(datos.Identificacion, "identificacion."))
            .Concat(ContratoDeLaFila.Incumplimientos(datos.DomicilioFiscal, "domicilioFiscal."))
            .Concat(ContratoDeLaFila.Incumplimientos(datos.RegimenFiscal, "regimenFiscal."))
            .Concat(ContratoDeLaFila.Incumplimientos(limite, string.Empty));

        foreach ((string campo, MotivoDeRechazo motivo) in delContrato)
        {
            lectura.Apuntar(ColumnaDe(campo), motivo);
        }

        var errores = new ErroresPorCampo();
        IdentificacionFiscal? identificacion = Identificaciones.Leer(
            datos.Identificacion.Pais, datos.Identificacion.Numero, "identificacion.", errores);
        RegimenFiscal? regimen = RegimenesFiscales.Leer(datos.RegimenFiscal, "regimenFiscal.", errores);

        // La regla del país de `Direccion` es la misma que la del país del identificador —dos letras
        // ASCII, sin distinguir mayúsculas—, y esta es la puerta que pregunta sin lanzar.
        if (IdentificacionFiscal.PaisNormalizado(datos.DomicilioFiscal.Pais) is null)
        {
            errores.Agregar("domicilioFiscal.pais", "No es un código de país ISO 3166-1 alfa-2.");
        }

        // Aparte: `LimitesDeCredito.Leer` devuelve nulo si hay CUALQUIER error apuntado, y un NIF mal
        // escrito no tiene por qué dejar la fila sin límite antes de haberla rechazado.
        var erroresDelLimite = new ErroresPorCampo();
        Importe? importe = LimitesDeCredito.Leer(limite, erroresDelLimite);

        foreach (string campo in errores.CamposConError.Concat(erroresDelLimite.CamposConError))
        {
            lectura.Apuntar(ColumnaDe(campo), MotivoDeRechazo.NoValido);
        }

        if (esCliente == false && esProveedor == false)
        {
            lectura.Apuntar(EsCliente, MotivoDeRechazo.NiClienteNiProveedor);
        }

        // Para buscar repetidas y existentes solo sirve una identificación leída de campos sanos: una
        // sacada de un campo con comillas mal puestas podría marcar como repetida a otra fila por un
        // texto que el propio fichero no quería decir.
        IdentificacionFiscal? comparable =
            lectura.Legible(IdentificacionPais) && lectura.Legible(IdentificacionNumero) ? identificacion : null;

        AltaDecidida? alta = rechazos.EstaRechazada(fila.Linea) || identificacion is null || regimen is null
            ? null
            : new AltaDecidida(identificacion, datos, regimen, importe);

        return new FilaDecidida(fila.Linea, comparable, alta);
    }

    private static string ColumnaDe(string campo) =>
        s_columnaDelCampo.TryGetValue(campo, out string? columna)
            ? columna
            : throw new InvalidOperationException(
                $"El campo «{campo}» del alta de un tercero no tiene columna en el fichero de importación.");

    private static int IndiceDe(string columna)
    {
        for (int indice = 0; indice < Cabecera.Count; indice++)
        {
            if (string.Equals(Cabecera[indice], columna, StringComparison.Ordinal))
            {
                return indice;
            }
        }

        throw new ArgumentOutOfRangeException(nameof(columna), columna, "No es una columna de la cabecera.");
    }

    // Lo que se lee de una fila, columna a columna, apuntando el formato que no vale.
    private sealed class Lectura(FilaCsv fila, RechazosDeImportacion rechazos)
    {
        public string Texto(string columna) => fila.Campos[IndiceDe(columna)];

        public string? Opcional(string columna) => Texto(columna) is { Length: > 0 } texto ? texto : null;

        public bool Legible(string columna) => !fila.ComillasMalColocadas.Contains(IndiceDe(columna));

        public void Apuntar(string columna, MotivoDeRechazo motivo) => rechazos.Apuntar(fila.Linea, columna, motivo);

        public bool? SiNo(string columna)
        {
            if (!Legible(columna))
            {
                return null;
            }

            if (CamposCsv.IntentarLeerSiNo(Texto(columna), out bool valor))
            {
                return valor;
            }

            Apuntar(columna, MotivoDeRechazo.FormatoNoValido);
            return null;
        }

        public decimal? Importe(string columna)
        {
            if (!Legible(columna) || Texto(columna).Length == 0)
            {
                return null;
            }

            if (CamposCsv.IntentarLeerImporte(Texto(columna), out decimal valor))
            {
                return valor;
            }

            Apuntar(columna, MotivoDeRechazo.FormatoNoValido);
            return null;
        }
    }
}

/// <summary>Lo que queda de una fila después de decidirla.</summary>
/// <param name="Linea">Su línea en el fichero.</param>
/// <param name="Identificacion">La identificación, si se ha podido leer de campos sanos.</param>
/// <param name="Alta">Lo necesario para darla de alta, solo si la fila no tiene ningún motivo.</param>
internal sealed record FilaDecidida(int Linea, IdentificacionFiscal? Identificacion, AltaDecidida? Alta);

/// <summary>Una fila que, a falta de mirar repeticiones y existencia, se puede dar de alta.</summary>
/// <param name="Identificacion">La identificación ya normalizada.</param>
/// <param name="Datos">Los datos del alta, validados.</param>
/// <param name="Regimen">El régimen fiscal ya leído.</param>
/// <param name="Limite">El límite de crédito, o nulo si la fila no trae.</param>
internal sealed record AltaDecidida(
    IdentificacionFiscal Identificacion,
    CrearTerceroDto Datos,
    RegimenFiscal Regimen,
    Importe? Limite);
