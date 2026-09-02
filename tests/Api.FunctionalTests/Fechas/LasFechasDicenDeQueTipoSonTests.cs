using Bastion.Api.FunctionalTests.Salud;
using Bastion.Auditoria.Infrastructure.Persistencia;
using Bastion.BuildingBlocks.Infrastructure.Auditoria;
using Bastion.Identidad.Infrastructure.Persistencia;
using Bastion.Organizacion.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Bastion.Api.FunctionalTests.Fechas;

/// <summary>
/// R14 sobre el modelo entero: un instante se guarda con zona horaria, una fecha de calendario sin
/// ella, y no hay ninguna columna que no diga cuál de las dos cosas es.
/// </summary>
/// <remarks>
/// <para>
/// <b>Por qué un barrido y no un test por columna.</b> Las columnas de fecha se prueban una a una
/// contra <c>information_schema</c> en los tests de esquema, y eso comprueba lo que hay; esto
/// comprueba lo que <b>vaya habiendo</b>. Una entidad nueva con una fecha mal tipada entra en este
/// barrido el día que se escribe, sin que nadie tenga que acordarse de venir aquí.
/// </para>
/// <para>
/// <b>La distinción no es estilística.</b> El ejercicio 2026 empieza el 1 de enero en Madrid y en
/// Canarias: guardarlo como <c>timestamptz</c> obliga a elegir una zona, y el 1 de enero a las
/// 00:00 en Madrid ya es el 31 de diciembre en UTC-1. Al revés, un instante sin zona —cuándo
/// caducó un token, cuándo se bloqueó una ficha— significa lo que diga el reloj del servidor que
/// lo lea, y de la fecha de bloqueo cuelga el plazo del art. 32 de la LOPDGDD.
/// </para>
/// <para>
/// <b>Recorre los tipos complejos.</b> Usa el mismo <c>PropiedadesConCamino()</c> que el barrido de
/// auditoría, así que <c>Bloqueo.Desde</c> —que vive dentro de un tipo complejo y NO sale en
/// <c>GetProperties()</c>— entra aquí igual que cualquier otra.
/// </para>
/// </remarks>
public sealed class LasFechasDicenDeQueTipoSonTests : IDisposable
{
    private readonly ApiSinDependencias _api = new();

    public void Dispose() => _api.Dispose();

    [Fact]
    public void Todo_instante_se_guarda_con_zona_horaria()
    {
        List<string> descolocadas = [.. Columnas()
            .Where(columna => EsDelTipo<DateTimeOffset>(columna.Propiedad))
            .Where(columna => columna.Tipo != "timestamp with time zone")
            .Select(columna => $"{columna.Donde} es {columna.Tipo}")];

        descolocadas.ShouldBeEmpty(
            "un `DateTimeOffset` es un punto en la línea del tiempo y se guarda como " +
            "`timestamptz`: " + string.Join(", ", descolocadas));
    }

    [Fact]
    public void Toda_fecha_de_calendario_se_guarda_sin_zona_horaria()
    {
        List<string> descolocadas = [.. Columnas()
            .Where(columna => EsDelTipo<DateOnly>(columna.Propiedad))
            .Where(columna => columna.Tipo != "date")
            .Select(columna => $"{columna.Donde} es {columna.Tipo}")];

        descolocadas.ShouldBeEmpty(
            "un `DateOnly` es una fecha de calendario y se guarda como `date`: " +
            string.Join(", ", descolocadas));
    }

    [Fact]
    public void No_hay_ni_una_fecha_que_no_diga_si_lleva_zona()
    {
        // `DateTime` es el tipo que NO contesta la pregunta: el mismo valor puede ser local, UTC o
        // «no consta», y quién lo decide es el `Kind`, que se pierde en cuanto el valor viaja por
        // JSON o vuelve de la base. Que no exista ninguno no es purismo: es que la pregunta «¿de
        // qué tipo es esta fecha?» tiene que tener respuesta mirando el tipo, no rastreando quién
        // la construyó.
        //
        // Npgsql además mapea `DateTime` a `timestamp with time zone` o a `timestamp without time
        // zone` según el `Kind`, así que una entidad con `DateTime` decide su propio tipo de
        // columna en tiempo de ejecución.
        List<string> ambiguas = [.. Columnas()
            .Where(columna => EsDelTipo<DateTime>(columna.Propiedad))
            .Select(columna => columna.Donde)];

        ambiguas.ShouldBeEmpty(
            "usa `DateTimeOffset` para un instante y `DateOnly` para una fecha de calendario: " +
            string.Join(", ", ambiguas));
    }

    [Fact]
    public void El_barrido_encuentra_fechas_de_las_dos_clases()
    {
        // El caso que hace que los tres de arriba signifiquen algo. Sin él, un `Columnas()` que
        // devolviera la lista vacía —porque el recorrido se rompió, o porque el modelo no llegó a
        // construirse— dejaría los tres en verde diciendo que todo está bien tipado.
        (int Instantes, int Fechas) cuantas = (
            Columnas().Count(columna => EsDelTipo<DateTimeOffset>(columna.Propiedad)),
            Columnas().Count(columna => EsDelTipo<DateOnly>(columna.Propiedad)));

        cuantas.Instantes.ShouldBeGreaterThan(10, "hay instantes de sobra en el modelo");

        // El recuento es EXACTO a propósito, no un «al menos»: una fecha de negocio nueva tiene
        // que pasar por aquí y que alguien la nombre. Las cinco de hoy son las dos del ejercicio,
        // las dos de la vigencia de un impuesto y la del tipo de cambio — las tres últimas, del
        // 0.15. Ninguna tiene hora ni zona: el 1 de septiembre de 2012 el IVA subió en Madrid y
        // en Canarias el mismo día.
        cuantas.Fechas.ShouldBe(
            5,
            "las dos del ejercicio, las dos de la vigencia del impuesto y la del tipo de cambio");
    }

    private static bool EsDelTipo<T>(IReadOnlyProperty propiedad) =>
        Nullable.GetUnderlyingType(propiedad.ClrType) is { } interno
            ? interno == typeof(T)
            : propiedad.ClrType == typeof(T);

    private IEnumerable<(string Donde, IReadOnlyProperty Propiedad, string? Tipo)> Columnas()
    {
        using IServiceScope alcance = _api.Services.CreateScope();

        return
        [
            .. Del(alcance.ServiceProvider.GetRequiredService<OrganizacionDbContext>()),
            .. Del(alcance.ServiceProvider.GetRequiredService<IdentidadDbContext>()),
            .. Del(alcance.ServiceProvider.GetRequiredService<AuditoriaDbContext>()),
        ];
    }

    private static IEnumerable<(string Donde, IReadOnlyProperty Propiedad, string? Tipo)> Del(
        DbContext contexto) =>
        contexto.Model.GetEntityTypes().SelectMany(tipo => tipo.PropiedadesConCamino()
            .Select(par => (
                Donde: $"{tipo.ShortName()}.{par.Camino}",
                par.Propiedad,
                Tipo: par.Propiedad.GetColumnType())));
}
