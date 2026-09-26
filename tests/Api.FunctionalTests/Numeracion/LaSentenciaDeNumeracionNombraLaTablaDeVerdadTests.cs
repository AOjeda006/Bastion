using System.Text.RegularExpressions;
using Bastion.Api.FunctionalTests.Salud;
using Bastion.BuildingBlocks.Infrastructure.Numeracion;
using Bastion.Organizacion.Domain.Ejercicios;
using Bastion.Organizacion.Domain.Series;
using Bastion.Organizacion.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Bastion.Api.FunctionalTests.Numeracion;

/// <summary>
/// Lo que la sentencia de numeración escribe a mano es lo que el modelo de EF Core mapea.
/// </summary>
/// <remarks>
/// <para>
/// <b>El mecanismo no puede leer el modelo, y de ahí sale este fichero.</b>
/// <see cref="NumeradorDeSerie"/> vive en el bloque común, y el bloque común no ve el interior de
/// ningún módulo: <c>ContadorDeSerie</c> es de <c>Organizacion.Domain</c> y desde allí no existe.
/// Así que el esquema, las tres tablas y la columna van escritos como cadenas, y una cadena no se
/// entera de un renombrado. La tercera tabla, la de ejercicios, llegó con el ADR-0043.
/// </para>
/// <para>
/// <b>Lo que se rompería es silencioso, y por eso el precio de este fichero es barato.</b> Una
/// tabla renombrada en la migración deja la sentencia apuntando a algo que no existe: el fallo
/// sale en ejecución, en la confirmación de un documento, y no lo ve ni el compilador ni ningún
/// test que no baje a PostgreSQL. Una COLUMNA renombrada es peor todavía cuando el nombre sigue
/// existiendo con otro significado.
/// </para>
/// <para>
/// <b>Se compara contra el modelo ya construido, que es quien manda</b>, y no contra la migración:
/// el modelo es lo que EF Core usa para todo lo demás, y si el modelo y la base divergieran eso lo
/// dice <c>comprobar-migraciones.sh</c>, que es su trabajo y no el de aquí.
/// </para>
/// </remarks>
public sealed class LaSentenciaDeNumeracionNombraLaTablaDeVerdadTests : IDisposable
{
    // Las columnas que las tres sentencias nombran con alias, por su alias. Lista CERRADA y
    // comparada en los dos sentidos: añadir una condición sobre otra columna obliga a pasar por
    // aquí, que es donde alguien decide si esa columna es de verdad parte de la regla. Y una que
    // sobre delata la condición que se quitó sin quitar su línea.
    private static readonly string[] s_columnasQueLaSentenciaNombra =
    [
        "c.serie_id",
        "c.ultimo_numero",
        "e.fecha_de_fin",
        "e.fecha_de_inicio",
        "e.id",
        "s.ejercicio_id",
        "s.empresa_id",
        "s.estado",
        "s.id",
        "s.tipo_de_documento",
    ];

    private readonly ApiSinDependencias _api = new();

    public void Dispose() => _api.Dispose();

    [Fact]
    public void Las_cinco_cadenas_escritas_a_mano_son_las_del_modelo()
    {
        IEntityType contador = Entidad(typeof(ContadorDeSerie));
        IEntityType serie = Entidad(typeof(Serie));
        IEntityType ejercicio = Entidad(typeof(Ejercicio));

        NumeradorDeSerie<TipoDeDocumento>.Esquema.ShouldBe(contador.GetSchema());
        NumeradorDeSerie<TipoDeDocumento>.Esquema.ShouldBe(serie.GetSchema());
        NumeradorDeSerie<TipoDeDocumento>.Esquema.ShouldBe(ejercicio.GetSchema());
        NumeradorDeSerie<TipoDeDocumento>.TablaDeContadores.ShouldBe(contador.GetTableName());
        NumeradorDeSerie<TipoDeDocumento>.TablaDeSeries.ShouldBe(serie.GetTableName());
        NumeradorDeSerie<TipoDeDocumento>.TablaDeEjercicios.ShouldBe(ejercicio.GetTableName());
        NumeradorDeSerie<TipoDeDocumento>.ColumnaDelNumero.ShouldBe(
            Columna(contador, nameof(ContadorDeSerie.UltimoNumero)));
    }

    [Fact]
    public void Cada_columna_que_las_sentencias_nombran_existe_en_la_tabla_que_le_toca()
    {
        // Se EXTRAEN de las cadenas, no se escriben otra vez: una lista copiada a mano seguiría
        // verde el día que alguien añadiera `AND s.lo_que_sea = ...` a la sentencia de verdad.
        List<string> encontradas = [.. Regex
            .Matches(
                NumeradorDeSerie<TipoDeDocumento>.SqlDelIncremento + " " +
                NumeradorDeSerie<TipoDeDocumento>.SqlDelNumeroTomado + " " +
                NumeradorDeSerie<TipoDeDocumento>.SqlDelMotivo,
                @"\b([cse])\.([a-z_]+)\b",
                RegexOptions.None,
                TimeSpan.FromSeconds(1))
            .Select(coincidencia => coincidencia.Value)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)];

        // Primero el barrido se afirma a sí mismo (ADR-0020): si la expresión dejara de casar, lo
        // de abajo recorrería una lista vacía y saldría verde sin haber mirado ni una columna.
        encontradas.ShouldBe(s_columnasQueLaSentenciaNombra);

        Dictionary<char, IEntityType> porAlias = new()
        {
            ['c'] = Entidad(typeof(ContadorDeSerie)),
            ['s'] = Entidad(typeof(Serie)),
            ['e'] = Entidad(typeof(Ejercicio)),
        };

        List<string> inexistentes = [.. encontradas
            .Where(nombrada => !ColumnasDe(porAlias[nombrada[0]])
                .Contains(nombrada[2..], StringComparer.Ordinal))];

        inexistentes.ShouldBeEmpty(
            "la sentencia de numeración nombra columnas que el modelo no mapea: " +
            string.Join(", ", inexistentes));
    }

    [Fact]
    public void El_estado_que_condiciona_el_incremento_es_el_del_enumerado_y_se_guarda_como_texto()
    {
        // LAS DOS MITADES SON LA MISMA DECISIÓN. La sentencia compara contra el literal 'Activa',
        // que solo casa si el estado se guarda como TEXTO y si ese texto es el nombre del valor
        // del enumerado. Cambiar la conversión a entero no rompería nada visible: la comparación
        // dejaría de casar con todo, la sentencia devolvería cero filas y TODA confirmación
        // contestaría «esta serie no numera» -un 409 razonable, por un motivo que no es-.
        IEntityType serie = Entidad(typeof(Serie));
        IProperty estado = serie.FindProperty(nameof(Serie.Estado))!;

        (estado.GetTypeMapping().Converter?.ProviderClrType ?? estado.ClrType)
            .ShouldBe(typeof(string));

        NumeradorDeSerie<TipoDeDocumento>.SqlDelIncremento
            .ShouldContain($"'{nameof(EstadoDeSerie.Activa)}'");
    }

    [Fact]
    public void El_tipo_que_condiciona_el_incremento_se_guarda_como_el_nombre_del_enumerado()
    {
        // LA MISMA TRAMPA QUE LA DEL ESTADO, con el parámetro en vez del literal (ADR-0043). Cada
        // módulo pasa el NOMBRE de un valor de `TipoDeDocumento`; eso solo casa con la columna si
        // la columna guarda ese nombre. Con una conversión a entero, el parámetro de texto no
        // casaría con nada y todo ajuste contestaría «esta serie es de otro documento».
        IEntityType serie = Entidad(typeof(Serie));
        IProperty tipo = serie.FindProperty(nameof(Serie.TipoDeDocumento))!;

        (tipo.GetTypeMapping().Converter?.ProviderClrType ?? tipo.ClrType)
            .ShouldBe(typeof(string));

        Columna(serie, nameof(Serie.TipoDeDocumento)).ShouldBe("tipo_de_documento");
        Columna(serie, nameof(Serie.EjercicioId)).ShouldBe("ejercicio_id");
    }

    private IEntityType Entidad(Type tipo)
    {
        using IServiceScope alcance = _api.Services.CreateScope();

        return alcance.ServiceProvider.GetRequiredService<OrganizacionDbContext>()
            .Model.FindEntityType(tipo)
            ?? throw new InvalidOperationException($"{tipo.Name} no está en el modelo.");
    }

    private static string Columna(IEntityType entidad, string propiedad) =>
        entidad.FindProperty(propiedad)!.GetColumnName(
            StoreObjectIdentifier.Create(entidad, StoreObjectType.Table)!.Value)!;

    private static IReadOnlyList<string> ColumnasDe(IEntityType entidad) =>
        [.. entidad.GetProperties().Select(propiedad => Columna(entidad, propiedad.Name))];
}
