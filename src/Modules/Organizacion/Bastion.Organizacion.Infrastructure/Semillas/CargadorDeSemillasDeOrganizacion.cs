using System.Globalization;
using Bastion.BuildingBlocks.Application.Multiempresa;
using Bastion.Organizacion.Application;
using Bastion.Organizacion.Domain.Impuestos;
using Bastion.Organizacion.Domain.Unidades;
using Bastion.Organizacion.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Bastion.Organizacion.Infrastructure.Semillas;

/// <summary>
/// Carga los maestros de <c>db/semillas/</c> en la base: los tipos impositivos y las unidades de
/// medida. Lo llama el migrador, después de aplicar el esquema.
/// </summary>
/// <remarks>
/// <para>
/// <b>Lo hace el migrador y no el arranque de la API</b>, por lo mismo que las migraciones: con
/// dos réplicas, dos procesos cargarían a la vez y el segundo se estrellaría contra el índice
/// único del primero. El migrador es un contenedor de un solo uso, corre una vez, y su código de
/// salida es la señal que mira el compose.
/// </para>
/// <para>
/// <b>Es repetible, y no por comodidad:</b> el migrador se ejecuta en cada despliegue, no solo en
/// el primero. Cada fila se busca por su identidad natural —el código en las unidades, el código
/// y el primer día de vigencia en los impuestos— y solo se añade si falta. Lo que ya está NO se
/// toca: una instalación que renombró «Caja» a «Caja de 12» no quiere que el siguiente despliegue
/// se lo devuelva.
/// </para>
/// <para>
/// <b>No hay borrado.</b> Quitar una fila del <c>.json</c> no la borra de la base, y es
/// deliberado: un impuesto sembrado puede llevar meses en las líneas de una factura, y la R16 dice
/// que suprimir no es borrar. Una semilla retirada se bloquea desde la aplicación, que es donde se
/// ve lo que se está bloqueando.
/// </para>
/// <para>
/// <b>Un <c>SaveChanges</c> por fichero</b>, no uno por fila ni uno para todo: los impuestos
/// entran o no entra ninguno, y lo mismo las unidades. La granularidad es la del fichero porque es
/// la del fallo — si el <c>.json</c> de impuestos tiene un tramo solapado, lo que hay que dejar
/// fuera es ese fichero, no las unidades, que no tienen nada que ver.
/// </para>
/// </remarks>
/// <param name="contexto">Contexto del módulo, para consultar lo que ya está y añadir lo que falta.</param>
/// <param name="unidad">La unidad de trabajo del módulo, que es quien confirma.</param>
/// <param name="inquilino">De dónde sale —y dónde se abre— el ámbito sin empresa.</param>
/// <param name="reloj">El reloj inyectado, que pone las marcas de tiempo (R14).</param>
/// <param name="registro">Registro estructurado.</param>
public sealed partial class CargadorDeSemillasDeOrganizacion(
    OrganizacionDbContext contexto,
    IUnidadTrabajoDeOrganizacion unidad,
    IInquilinoActual inquilino,
    TimeProvider reloj,
    ILogger<CargadorDeSemillasDeOrganizacion> registro)
{
    /// <summary>Carga las semillas que falten y comprueba que después hay algo.</summary>
    /// <param name="cancelacion">Cancelación de la operación.</param>
    /// <exception cref="SemillasQueNoLleganException">
    /// Si los ficheros no han llegado, vienen vacíos o, tras cargarlos, la base sigue sin
    /// maestros.
    /// </exception>
    public async Task CargarAsync(CancellationToken cancelacion)
    {
        string carpeta = SemillasDeOrganizacion.CarpetaPublicada;

        SemillasDeOrganizacion.ComprobarQueEstanTodas(carpeta);

        IReadOnlyList<FilaDeImpuesto> impuestos =
            SemillasDeOrganizacion.Leer<FilaDeImpuesto>(carpeta, SemillasDeOrganizacion.Impuestos);
        IReadOnlyList<FilaDeUnidad> unidades =
            SemillasDeOrganizacion.Leer<FilaDeUnidad>(carpeta, SemillasDeOrganizacion.UnidadesDeMedida);

        // Los maestros son de la INSTALACIÓN y no de una empresa (R8): no hay ninguna a la que
        // pertenezcan y aquí no hay petición de la que sacarla. El ámbito no es para poder
        // consultarlos —ni `Impuesto` ni `UnidadMedida` llevan filtro— sino para que la traza de
        // cada alta pueda escribirse: sin empresa y sin ámbito, el interceptor de auditoría lanza,
        // que es exactamente lo que tiene que hacer.
        using IDisposable ambito = inquilino.SinInquilino(MotivoSinInquilino.CargaDeMaestros);

        int unidadesNuevas = await CargarUnidadesAsync(unidades, cancelacion).ConfigureAwait(false);
        int impuestosNuevos = await CargarImpuestosAsync(impuestos, cancelacion).ConfigureAwait(false);

        // LA AFIRMACIÓN DE CONJUNTO NO VACÍO, y la que de verdad cierra el agujero. Las de más
        // arriba miran el FICHERO; esta mira la BASE, que es donde tienen que acabar. Entre las
        // dos hay sitio para un fallo silencioso —un `SaveChanges` sobre el contexto equivocado
        // devuelve cero filas sin quejarse—, y sin este recuento la carga saldría con 0 habiendo
        // dejado el catálogo vacío.
        int impuestosEnLaBase = await contexto.Impuestos.CountAsync(cancelacion).ConfigureAwait(false);
        int unidadesEnLaBase = await contexto.UnidadesDeMedida.CountAsync(cancelacion).ConfigureAwait(false);

        ComprobarQueQuedaronDentro(SemillasDeOrganizacion.Impuestos, impuestos.Count, impuestosEnLaBase);
        ComprobarQueQuedaronDentro(SemillasDeOrganizacion.UnidadesDeMedida, unidades.Count, unidadesEnLaBase);

        // Se dice SIEMPRE, también cuando no se ha añadido nada. Un cargador silencioso no
        // distingue «ya estaban» de «no he mirado», y las dos salen con 0.
        SemillasCargadas(
            registro,
            impuestosNuevos,
            impuestos.Count,
            impuestosEnLaBase,
            unidadesNuevas,
            unidades.Count,
            unidadesEnLaBase);
    }

    private static void ComprobarQueQuedaronDentro(string fichero, int enElFichero, int enLaBase)
    {
        if (enLaBase < enElFichero)
        {
            throw new SemillasQueNoLleganException(
                $"«{fichero}» trae {enElFichero.ToString(CultureInfo.InvariantCulture)} filas y en " +
                $"la base hay {enLaBase.ToString(CultureInfo.InvariantCulture)}. La carga ha salido " +
                "bien sin dejar los maestros dentro, que es la avería que no da error.");
        }
    }

    // Tipo con nombre y no anónimo: el proyector de EF Core traduce los dos, pero un anónimo no se
    // puede declarar como el tipo de la lista, y sin eso el `await` queda dentro de una expresión
    // de colección que no hay quien lea.
    private sealed record TramoGuardado(string Codigo, DateOnly Desde);

    private async Task<int> CargarUnidadesAsync(
        IReadOnlyList<FilaDeUnidad> filas,
        CancellationToken cancelacion)
    {
        // Los códigos ya guardados, en una sola consulta. Preguntar uno a uno serían tantas idas y
        // vueltas como filas trae el fichero, y la lista de unidades de una instalación cabe
        // holgadamente en memoria.
        HashSet<string> presentes =
        [
            .. await contexto.UnidadesDeMedida
                .Select(unidadDeMedida => unidadDeMedida.Codigo)
                .ToListAsync(cancelacion)
                .ConfigureAwait(false),
        ];

        DateTimeOffset ahora = reloj.GetUtcNow();
        int nuevas = 0;

        foreach (FilaDeUnidad fila in filas)
        {
            // Se compara por el código NORMALIZADO, que es la forma en la que se guarda. Comparar
            // por lo que trae el fichero dejaría que un `kg` en minúsculas se diera de alta como
            // una segunda unidad, y el índice único la rechazaría con un 500 en el migrador.
            if (!presentes.Add(UnidadMedida.NormalizarCodigo(fila.Codigo)))
            {
                continue;
            }

            contexto.UnidadesDeMedida.Add(
                UnidadMedida.Crear(fila.Codigo, fila.Nombre, fila.Decimales, ahora));
            nuevas++;
        }

        if (nuevas > 0)
        {
            await unidad.ConfirmarAsync(cancelacion).ConfigureAwait(false);
        }

        return nuevas;
    }

    private async Task<int> CargarImpuestosAsync(
        IReadOnlyList<FilaDeImpuesto> filas,
        CancellationToken cancelacion)
    {
        // La identidad de un tramo es el código MÁS el día en que empieza a regir: `IVA-GENERAL`
        // tiene tantas filas como veces ha cambiado el tipo, y buscarlo solo por el código daría
        // «ya está» a partir del segundo despliegue, dejando fuera todos los tramos menos el
        // primero.
        List<TramoGuardado> guardados = await contexto.Impuestos
            .Select(impuesto => new TramoGuardado(impuesto.Codigo, impuesto.VigenteDesde))
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

        HashSet<(string Codigo, DateOnly Desde)> presentes =
            [.. guardados.Select(tramo => (tramo.Codigo, tramo.Desde))];

        DateTimeOffset ahora = reloj.GetUtcNow();
        int nuevos = 0;

        foreach (FilaDeImpuesto fila in filas)
        {
            if (!presentes.Add((Impuesto.NormalizarCodigo(fila.Codigo), fila.VigenteDesde)))
            {
                continue;
            }

            contexto.Impuestos.Add(Impuesto.Crear(
                fila.Codigo,
                fila.Nombre,
                fila.Tipo,
                fila.Porcentaje,
                fila.VigenteDesde,
                fila.VigenteHasta,
                fila.CuentaRepercutido,
                fila.CuentaSoportado,
                ahora));
            nuevos++;
        }

        if (nuevos > 0)
        {
            await unidad.ConfirmarAsync(cancelacion).ConfigureAwait(false);
        }

        return nuevos;
    }

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Semillas de Organización: {ImpuestosNuevos} impuestos nuevos de {ImpuestosDelFichero} " +
            "en el fichero, {ImpuestosEnLaBase} en la base; {UnidadesNuevas} unidades nuevas de " +
            "{UnidadesDelFichero} en el fichero, {UnidadesEnLaBase} en la base.")]
    private static partial void SemillasCargadas(
        ILogger logger,
        int impuestosNuevos,
        int impuestosDelFichero,
        int impuestosEnLaBase,
        int unidadesNuevas,
        int unidadesDelFichero,
        int unidadesEnLaBase);
}
