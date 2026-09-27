using Bastion.Api.IntegrationTests.Persistencia;
using Bastion.Inventario.Domain.Existencias;
using Bastion.Inventario.Domain.Movimientos;
using Bastion.Inventario.Infrastructure.Persistencia;
using Bastion.Inventario.Infrastructure.Persistencia.Existencias;
using Microsoft.EntityFrameworkCore;

namespace Bastion.Api.IntegrationTests.Inventario;

/// <summary>Una clave de la existencia mientras el libro no lleve lote.</summary>
/// <param name="ArticuloId">El artículo.</param>
/// <param name="AlmacenId">El almacén.</param>
/// <param name="UbicacionId">La ubicación.</param>
internal readonly record struct ClaveDeExistencia(Guid ArticuloId, Guid AlmacenId, Guid UbicacionId);

/// <summary>Una fila del libro, reducida a lo que la proyección suma.</summary>
/// <param name="Clave">De qué existencia es.</param>
/// <param name="Fecha">El día al que se imputa.</param>
/// <param name="Cantidad">Lo que mueve, en unidad base y con signo.</param>
internal sealed record ApunteDelLibro(ClaveDeExistencia Clave, DateOnly Fecha, decimal Cantidad);

/// <summary>Una instantánea, reducida a su clave, su mes y lo que dice.</summary>
/// <param name="Clave">De qué existencia es.</param>
/// <param name="Mes">El primer día del mes.</param>
/// <param name="Fisico">El saldo al acabar ese mes.</param>
internal sealed record FotoDelMes(ClaveDeExistencia Clave, DateOnly Mes, decimal Fisico);

/// <summary>
/// Lo que los casos del ítem 2.7 leen de la proyección del libro y lo que le mandan hacer.
/// </summary>
/// <remarks>
/// <para>
/// <b>Las lecturas van por EF Core, con el filtro de la empresa puesto</b>, y no en crudo: lo que se
/// afirma es lo que el sistema enseña a quien pregunta desde su empresa. Lo que un caso necesita
/// escribir por debajo del sistema lo escribe él, a la vista.
/// </para>
/// <para>
/// <b><see cref="DebidasEnCSharp"/> es una segunda cuenta, no una copia de la primera.</b> El
/// recálculo y el cuadre comparten un fragmento de SQL con funciones de ventana; esto recorre mes a
/// mes y suma con un bucle. Si las dos se equivocaran igual, lo harían por caminos que no se
/// parecen en nada, y eso es lo que da valor a que coincidan.
/// </para>
/// </remarks>
internal static class LasExistencias
{
    /// <summary>Cuadra la empresa por el camino de producción.</summary>
    /// <param name="postgres">El contenedor.</param>
    /// <param name="empresaId">La empresa que se cuadra.</param>
    /// <returns>Lo que contestó el cuadre.</returns>
    internal static async Task<CuadreDeLasExistencias> CuadrarAsync(
        PostgresConTodosLosModulos postgres, Guid empresaId)
    {
        await using InventarioDbContext contexto = postgres.AbrirInventario(empresaId);

        return await CuadrarEnAsync(contexto, empresaId);
    }

    /// <summary>Cuadra con un contexto que ya trae quien llama, y con su transacción si la tiene.</summary>
    /// <param name="contexto">El contexto de Inventario.</param>
    /// <param name="empresaId">La empresa que se cuadra.</param>
    /// <returns>Lo que contestó el cuadre.</returns>
    internal static Task<CuadreDeLasExistencias> CuadrarEnAsync(
        InventarioDbContext contexto, Guid empresaId) =>
        new ElCuadreDeLasExistencias(contexto, new InquilinoFijo(empresaId), TimeProvider.System)
            .CuadrarAsync(CancellationToken.None);

    /// <summary>Recalcula las instantáneas de la empresa hasta un mes, con su propio contexto.</summary>
    /// <param name="postgres">El contenedor.</param>
    /// <param name="empresaId">La empresa que se recalcula.</param>
    /// <param name="hastaElMes">El primer día del último mes con instantánea.</param>
    /// <returns>Cuántas instantáneas repuso.</returns>
    internal static async Task<int> RecalcularAsync(
        PostgresConTodosLosModulos postgres, Guid empresaId, DateOnly hastaElMes)
    {
        await using InventarioDbContext contexto = postgres.AbrirInventario(empresaId);

        return await new LasInstantaneasMensuales(contexto, new InquilinoFijo(empresaId))
            .RecalcularAsync(hastaElMes, CancellationToken.None);
    }

    /// <summary>Las filas vivas de la empresa, en un orden que no depende del motor.</summary>
    /// <param name="postgres">El contenedor.</param>
    /// <param name="empresaId">La empresa.</param>
    /// <returns>Las existencias, ordenadas por su clave.</returns>
    internal static async Task<IReadOnlyList<Existencia>> VivasAsync(
        PostgresConTodosLosModulos postgres, Guid empresaId)
    {
        await using InventarioDbContext contexto = postgres.AbrirInventario(empresaId);

        List<Existencia> vivas = await contexto.Existencias.AsNoTracking().ToListAsync();

        return [.. vivas.OrderBy(fila => ClaveDe(fila), ComparadorDeClaves.Instancia)];
    }

    /// <summary>Las instantáneas de la empresa, con la clave de su fila viva.</summary>
    /// <param name="postgres">El contenedor.</param>
    /// <param name="empresaId">La empresa.</param>
    /// <returns>Las instantáneas, ordenadas por clave y mes.</returns>
    internal static async Task<IReadOnlyList<FotoDelMes>> InstantaneasAsync(
        PostgresConTodosLosModulos postgres, Guid empresaId)
    {
        await using InventarioDbContext contexto = postgres.AbrirInventario(empresaId);

        var leidas = await contexto.InstantaneasMensuales
            .AsNoTracking()
            .Join(
                contexto.Existencias,
                instantanea => instantanea.ExistenciaId,
                existencia => existencia.Id,
                (instantanea, existencia) => new
                {
                    existencia.ArticuloId,
                    existencia.AlmacenId,
                    existencia.UbicacionId,
                    instantanea.Mes,
                    instantanea.Fisico,
                })
            .ToListAsync();

        return Ordenadas(leidas.Select(fila => new FotoDelMes(
            new ClaveDeExistencia(fila.ArticuloId, fila.AlmacenId, fila.UbicacionId),
            fila.Mes,
            fila.Fisico)));
    }

    /// <summary>El libro entero de la empresa, reducido a lo que la proyección suma.</summary>
    /// <param name="postgres">El contenedor.</param>
    /// <param name="empresaId">La empresa.</param>
    /// <returns>Los apuntes, en el orden en que llegan.</returns>
    internal static async Task<IReadOnlyList<ApunteDelLibro>> LibroAsync(
        PostgresConTodosLosModulos postgres, Guid empresaId)
    {
        await using InventarioDbContext contexto = postgres.AbrirInventario(empresaId);

        List<MovimientoStock> filas = await contexto.Movimientos.AsNoTracking().ToListAsync();

        return [.. filas.Select(fila => new ApunteDelLibro(
            new ClaveDeExistencia(fila.ArticuloId, fila.AlmacenId, fila.UbicacionId),
            fila.FechaDeOperacion,
            fila.CantidadEnUnidadBase))];
    }

    /// <summary>
    /// Las instantáneas que tiene que haber, contadas en C# y sin mirar el SQL que las escribe.
    /// </summary>
    /// <remarks>
    /// La definición, escrita de la manera más tonta posible: por cada clave, desde el primer mes en
    /// que se movió hasta el corte, <b>sin huecos</b>, el saldo al acabar el mes —la suma de lo que
    /// el libro tiene antes del primer día del mes siguiente—. Una clave que solo se movió después
    /// del corte no tiene ninguna, y sin corte no hay ninguna.
    /// </remarks>
    /// <param name="libro">Los apuntes.</param>
    /// <param name="corte">El primer día del último mes con instantánea, o nulo si no hay corte.</param>
    /// <returns>Las instantáneas debidas, ordenadas por clave y mes.</returns>
    internal static IReadOnlyList<FotoDelMes> DebidasEnCSharp(
        IEnumerable<ApunteDelLibro> libro, DateOnly? corte)
    {
        if (corte is not DateOnly hasta)
        {
            return [];
        }

        DateOnly despuesDelCorte = hasta.AddMonths(1);
        List<FotoDelMes> debidas = [];

        foreach (IGrouping<ClaveDeExistencia, ApunteDelLibro> clave in libro
            .Where(apunte => apunte.Fecha < despuesDelCorte)
            .GroupBy(apunte => apunte.Clave))
        {
            DateOnly primera = clave.Min(apunte => apunte.Fecha);

            for (DateOnly mes = new(primera.Year, primera.Month, 1); mes <= hasta; mes = mes.AddMonths(1))
            {
                DateOnly siguiente = mes.AddMonths(1);
                decimal saldo = 0m;

                foreach (ApunteDelLibro apunte in clave)
                {
                    if (apunte.Fecha < siguiente)
                    {
                        saldo += apunte.Cantidad;
                    }
                }

                debidas.Add(new FotoDelMes(clave.Key, mes, saldo));
            }
        }

        return Ordenadas(debidas);
    }

    /// <summary>El saldo de cada clave según el libro: la definición de la R3.</summary>
    /// <param name="libro">Los apuntes.</param>
    /// <returns>La suma por clave.</returns>
    internal static IReadOnlyDictionary<ClaveDeExistencia, decimal> SaldosDelLibro(
        IEnumerable<ApunteDelLibro> libro) =>
        libro
            .GroupBy(apunte => apunte.Clave)
            .ToDictionary(clave => clave.Key, clave => clave.Sum(apunte => apunte.Cantidad));

    /// <summary>La clave de una fila viva.</summary>
    /// <param name="existencia">La fila.</param>
    /// <returns>Su clave, sin el lote.</returns>
    internal static ClaveDeExistencia ClaveDe(Existencia existencia) =>
        new(existencia.ArticuloId, existencia.AlmacenId, existencia.UbicacionId);

    /// <summary>El primer día del mes de una fecha.</summary>
    /// <param name="fecha">La fecha.</param>
    /// <returns>El día 1 de su mes.</returns>
    internal static DateOnly MesDe(DateOnly fecha) => new(fecha.Year, fecha.Month, 1);

    private static FotoDelMes[] Ordenadas(IEnumerable<FotoDelMes> fotos) =>
        [.. fotos
            .OrderBy(foto => foto.Clave, ComparadorDeClaves.Instancia)
            .ThenBy(foto => foto.Mes)];

    /// <summary>Un orden de claves que no depende de cómo ordene el motor los <c>uuid</c>.</summary>
    private sealed class ComparadorDeClaves : IComparer<ClaveDeExistencia>
    {
        internal static readonly ComparadorDeClaves Instancia = new();

        public int Compare(ClaveDeExistencia x, ClaveDeExistencia y)
        {
            int porArticulo = x.ArticuloId.CompareTo(y.ArticuloId);

            if (porArticulo != 0)
            {
                return porArticulo;
            }

            int porAlmacen = x.AlmacenId.CompareTo(y.AlmacenId);

            return porAlmacen != 0 ? porAlmacen : x.UbicacionId.CompareTo(y.UbicacionId);
        }
    }
}
