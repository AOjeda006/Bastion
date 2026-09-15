using Bastion.BuildingBlocks.Application.Autorizacion;
using Bastion.BuildingBlocks.Application.Bloqueos;
using Bastion.BuildingBlocks.Application.Direcciones;
using Bastion.BuildingBlocks.Application.Importacion;
using Bastion.BuildingBlocks.Application.Multiempresa;
using Bastion.BuildingBlocks.Contracts.Importacion;
using Bastion.BuildingBlocks.Domain.Autorizacion;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Organizacion.Contracts.Empresas;
using Bastion.Terceros.Contracts;
using Bastion.Terceros.Contracts.Terceros;
using Bastion.Terceros.Domain.Terceros;

namespace Bastion.Terceros.Application.Terceros;

/// <summary>Da de alta los terceros de un fichero CSV, fila a fila, y dice cuáles no.</summary>
public interface IImportarTerceros
{
    /// <summary>Ejecuta el caso de uso.</summary>
    /// <param name="fichero">
    /// El contenido del fichero, ya leído por el borde con el tope de
    /// <see cref="ImportacionDeTerceros.TopeDeBytes"/>.
    /// </param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<Resultado<InformeDeImportacionDto>> EjecutarAsync(ReadOnlyMemory<byte> fichero, CancellationToken cancelacion);
}

/// <inheritdoc cref="IImportarTerceros"/>
/// <remarks>
/// <para>
/// <b>La fila decide, el fichero escribe</b> (ADR-0034 §1). Cada fila se valida y se decide por
/// separado, y una mala nunca impide que entren las buenas. Las altas aceptadas se confirman en UN
/// <c>ConfirmarAsync</c>, que cae dentro de la transacción del filtro de idempotencia, junto con el
/// recibo. Si el proceso se cae antes, no hay nada importado: ni filas ni recibo, y el reintento con la
/// misma clave es la primera vez.
/// </para>
/// <para>
/// <b>La R12 se rompe aquí a sabiendas.</b> Una transacción, N agregados: pero ninguno se modifica, se
/// crean N independientes, cada uno por su fábrica, y ningún invariante los cruza. Una transacción por
/// fila cumpliría la letra de la R12 y rompería la de la R10: un proceso caído en la fila 1 500 dejaría
/// 1 499 terceros sin recibo, y el reintento diría que «ya existen».
/// </para>
/// <para>
/// <b>Solo altas.</b> Lo que ya existe es <c>ya-existe</c>, activo o bloqueado sin distinguir, y nunca se
/// actualiza: el permiso de crear no es el de modificar, y una fila de CSV no trae la versión de la
/// ficha que pisaría.
/// </para>
/// </remarks>
internal sealed class ImportarTerceros(
    IUsuarioActual usuarioActual,
    IRepositorioDeTerceros terceros,
    IConsultaDeEmpresas empresas,
    IAccesoALoBloqueado bloqueados,
    IUnidadTrabajoDeTerceros unidadTrabajo,
    TimeProvider reloj) : IImportarTerceros
{
    private static readonly Permiso s_crear = Permiso.De(PermisosDeTerceros.TerceroCrear);
    private static readonly Permiso s_fijarLimite = Permiso.De(PermisosDeTerceros.LimiteCreditoFijar);

    public async Task<Resultado<InformeDeImportacionDto>> EjecutarAsync(
        ReadOnlyMemory<byte> fichero,
        CancellationToken cancelacion)
    {
        // La puerta pide importar; lo que se escribe son altas, y el alta tiene su permiso. Va antes de
        // leer el fichero: sin él no hay nada que decirle sobre sus filas.
        if (!usuarioActual.Tiene(s_crear))
        {
            return Resultado.Fallo<InformeDeImportacionDto>(ErroresDeImportacionDeTerceros.SinPermisoDeAlta());
        }

        Resultado<HojaCsv> leida = LectorCsv.Leer(
            fichero.Span, ImportacionDeTerceros.Cabecera, ImportacionDeTerceros.TopeDeFilas);

        if (!leida.EsCorrecto)
        {
            return Resultado.Fallo<InformeDeImportacionDto>(leida.Error!);
        }

        HojaCsv hoja = leida.Valor;

        // Tipo por verbo: el límite tiene su permiso, y sin él se rechaza el FICHERO. Rechazar solo
        // esas filas mezclaría un permiso con un dato mal escrito en el mismo informe; importarlas sin
        // el límite las dejaría a medias sin que nadie lo hubiera pedido.
        if (hoja.Filas.Any(FilasDeTerceros.TraeLimite) && !usuarioActual.Tiene(s_fijarLimite))
        {
            return Resultado.Fallo<InformeDeImportacionDto>(ErroresDeImportacionDeTerceros.SinPermisoParaElLimite());
        }

        Guid empresaId = usuarioActual.EmpresaId;

        if (!await empresas.EstaActivaAsync(empresaId, cancelacion).ConfigureAwait(false))
        {
            return Resultado.Fallo<InformeDeImportacionDto>(ErroresDeInquilinato.EmpresaActivaNoOperativa());
        }

        var rechazos = new RechazosDeImportacion(ImportacionDeTerceros.Cabecera);
        List<FilaDecidida> decididas = [.. hoja.Filas.Select(fila => FilasDeTerceros.Decidir(fila, rechazos))];

        await ApuntarLasQueYaExistenAsync(empresaId, decididas, rechazos, cancelacion).ConfigureAwait(false);
        ApuntarLasRepetidas(decididas, rechazos);

        int importadas = 0;
        DateTimeOffset ahora = reloj.GetUtcNow();

        foreach (FilaDecidida decidida in decididas)
        {
            if (decidida.Alta is not { } alta || rechazos.EstaRechazada(decidida.Linea))
            {
                continue;
            }

            var tercero = Tercero.Crear(
                empresaId,
                alta.Identificacion,
                alta.Datos.RazonSocial,
                alta.Datos.NombreComercial,
                alta.Datos.DomicilioFiscal.ADireccion(),
                alta.Datos.EsCliente,
                alta.Datos.EsProveedor,
                alta.Regimen,
                ahora);

            if (alta.Limite is not null)
            {
                tercero.FijarLimiteDeCredito(alta.Limite);
            }

            terceros.Agregar(tercero);
            importadas++;
        }

        // UNA confirmación para todo el fichero. Sin altas no hay nada que confirmar, y el recibo lo
        // guarda igualmente el filtro: un fichero en el que no entra nada también es una operación
        // atendida, y su reintento tiene que devolver el mismo informe.
        if (importadas > 0)
        {
            await unidadTrabajo.ConfirmarAsync(cancelacion).ConfigureAwait(false);
        }

        return Resultado.Correcto(new InformeDeImportacionDto(
            hoja.Filas.Count, importadas, rechazos.Rechazadas, rechazos.AInforme()));
    }

    // Una sola consulta para todas las identificaciones del fichero, y dentro del ámbito que ve lo
    // bloqueado, por lo mismo que el alta suelta: la unicidad abarca las fichas bloqueadas, y sin verlas
    // la fila chocaría al escribir y se llevaría el fichero entero. El puerto devuelve QUÉ está ocupado,
    // no si lo que ocupa está activo o bloqueado, así que el informe no puede distinguirlos (art. 32). Y
    // es la misma consulta para las dos, así que tampoco los distingue lo que tarda.
    private async Task ApuntarLasQueYaExistenAsync(
        Guid empresaId,
        List<FilaDecidida> decididas,
        RechazosDeImportacion rechazos,
        CancellationToken cancelacion)
    {
        List<(string Pais, string Numero)> leidas =
        [
            .. decididas
                .Where(decidida => decidida.Identificacion is not null)
                .Select(decidida => (decidida.Identificacion!.Pais, decidida.Identificacion.Numero))
                .Distinct(),
        ];

        if (leidas.Count == 0)
        {
            return;
        }

        IReadOnlySet<(string Pais, string Numero)> ocupadas;

        using (bloqueados.ViendoLoBloqueado(MotivoParaVerLoBloqueado.ComprobacionDeUnicidadDeIdentificador))
        {
            ocupadas = await terceros
                .IdentificacionesOcupadasAsync(empresaId, leidas, cancelacion)
                .ConfigureAwait(false);
        }

        foreach (FilaDecidida decidida in decididas)
        {
            if (decidida.Identificacion is { } identificacion
                && ocupadas.Contains((identificacion.Pais, identificacion.Numero)))
            {
                rechazos.Apuntar(decidida.Linea, ImportacionDeTerceros.IdentificacionNumero, MotivoDeRechazo.YaExiste);
            }
        }
    }

    // La primera aparición es la que cuenta, tenga o no otros motivos: la segunda es repetida aunque la
    // primera no vaya a entrar, porque quien corrige el fichero tiene que enterarse de las dos.
    private static void ApuntarLasRepetidas(List<FilaDecidida> decididas, RechazosDeImportacion rechazos)
    {
        HashSet<(string Pais, string Numero)> vistas = [];

        foreach (FilaDecidida decidida in decididas)
        {
            if (decidida.Identificacion is { } identificacion
                && !vistas.Add((identificacion.Pais, identificacion.Numero)))
            {
                rechazos.Apuntar(
                    decidida.Linea, ImportacionDeTerceros.IdentificacionNumero, MotivoDeRechazo.RepetidaEnElFichero);
            }
        }
    }
}

/// <summary>Lo que rechaza un fichero de terceros por algo que no es el formato.</summary>
internal static class ErroresDeImportacionDeTerceros
{
    /// <summary>Código estable del <c>403</c> por importar sin el permiso de dar de alta.</summary>
    internal const string CodigoDeSinPermisoDeAlta = "importacion-sin-permiso-de-alta";

    /// <summary>Código estable del <c>403</c> por traer límites sin el permiso de fijarlos.</summary>
    internal const string CodigoDeSinPermisoParaElLimite = "importacion-sin-permiso-de-limite";

    /// <summary>La sesión puede importar, pero no dar de alta terceros, que es lo que importar escribe.</summary>
    internal static ErrorDeOperacion SinPermisoDeAlta() => ErrorDeOperacion.PermisoDenegado(
        CodigoDeSinPermisoDeAlta,
        "Importar terceros es darlos de alta, y esta sesión no tiene el permiso de dar de alta terceros.");

    /// <summary>Alguna fila trae límite de crédito, y la sesión no puede fijarlo.</summary>
    /// <remarks>
    /// Sin decir qué filas: con el permiso que falta no se mira ninguna, y la corrección es la misma
    /// para todas —vaciar las dos columnas del límite o conseguir el permiso—.
    /// </remarks>
    internal static ErrorDeOperacion SinPermisoParaElLimite() => ErrorDeOperacion.PermisoDenegado(
        CodigoDeSinPermisoParaElLimite,
        "Alguna fila del fichero trae límite de crédito, y fijarlo exige un permiso que esta sesión no " +
        "tiene. Deje vacías las columnas del límite o pida el permiso, y vuelva a importar el fichero.");
}
