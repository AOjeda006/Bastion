using System.Globalization;
using System.Text.RegularExpressions;
using Bastion.Api.FunctionalTests.Salud;
using Bastion.Auditoria.Infrastructure.Persistencia;
using Bastion.BuildingBlocks.Infrastructure.Idempotencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Bastion.Api.FunctionalTests.Idempotencia;

/// <summary>
/// La sentencia cruda que reclama una clave sigue diciendo lo que su excepción dice que dice.
/// </summary>
/// <remarks>
/// <para>
/// <b>Este fichero es la contrapartida de una excepción.</b> <c>ElFiltroNoSeSaltaPorAhiTests</c>
/// permite una única llamada a <c>ExecuteSql</c> en todo el sistema, y la permite por un argumento
/// concreto: esa sentencia <b>no lee ninguna tabla</b>, y la fila que escribe lleva
/// <c>empresa_id</c> dentro de su clave primaria completa, tomado del <i>claim</i>. El día que
/// alguien quite <c>empresa_id</c> de la lista de columnas o del objetivo del conflicto, el
/// argumento deja de ser cierto y la excepción pasa a cubrir algo que nunca se autorizó — sin que
/// nada se ponga rojo. Aquí se pone.
/// </para>
/// <para>
/// <b>Y la segunda mitad: SQL escrito a mano contra un modelo que se mueve.</b> Una columna nueva
/// obligatoria en la entidad no toca este texto; la sentencia seguiría compilando y reventaría en
/// ejecución, en la primera petición con cabecera, con una violación de <c>NOT NULL</c>. Comparar
/// la sentencia contra el modelo <b>ya construido</b> saca ese fallo al paso rápido de la CI, sin
/// base de datos y sin que haga falta que nadie acierte a probar esa ruta.
/// </para>
/// </remarks>
public sealed class LaClaveDeIdempotenciaEsLaTuplaEnteraTests : IDisposable
{
    // Las columnas que la sentencia rellena y que NO son parte de la identidad: la huella del
    // cuerpo y el instante. Las de la respuesta no están porque la fila nace sin respuesta.
    private static readonly string[] s_fueraDeLaClave = ["huella", "creada_en"];

    private readonly ApiSinDependencias _api = new();

    public void Dispose() => _api.Dispose();

    [Fact]
    public void La_sentencia_nombra_la_tabla_del_modelo()
    {
        IEntityType entidad = Entidad();

        AlmacenDeIdempotencia.SqlDeLaReclamacion.ShouldContain(
            $"INSERT INTO {entidad.GetSchema()}.{entidad.GetTableName()} ",
            Case.Sensitive,
            "la sentencia escribe en una tabla y el modelo lee de otra");
    }

    // El corazón del argumento de la excepción: la empresa está en la clave, así que esta escritura
    // no puede tocar la fila de otra empresa aunque quisiera.
    [Fact]
    public void La_empresa_esta_en_las_columnas_y_en_el_objetivo_del_conflicto()
    {
        ColumnasInsertadas().ShouldContain("empresa_id");
        ColumnasDelConflicto().ShouldContain("empresa_id");
    }

    // El ON CONFLICT tiene que apuntar a la clave primaria ENTERA. Apuntando a menos columnas,
    // PostgreSQL no encontraría un índice único que las cubra y la sentencia fallaría; apuntando a
    // otras, dos claves distintas se pisarían en silencio.
    [Fact]
    public void El_objetivo_del_conflicto_es_la_clave_primaria_entera()
    {
        string[] delModelo = [.. Entidad().FindPrimaryKey()!.Properties.Select(Columna)];

        ColumnasDelConflicto().ShouldBe(delModelo, ignoreOrder: false);
    }

    [Fact]
    public void La_clave_primaria_es_la_tupla_entera()
    {
        Entidad().FindPrimaryKey()!.Properties.Select(propiedad => propiedad.Name).ShouldBe(
            ["EmpresaId", "UsuarioId", "Metodo", "Ruta", "Clave"],
            ignoreOrder: false,
            "la identidad de una petición repetible es la tupla entera, no la clave del cliente");
    }

    // Toda columna obligatoria del modelo tiene que estar en el INSERT. Las de la respuesta son
    // anulables a propósito —la fila nace antes de que exista la respuesta— y por eso no salen.
    [Fact]
    public void La_sentencia_rellena_todas_las_columnas_obligatorias()
    {
        string[] obligatorias =
        [
            .. Entidad().GetProperties()
                .Where(propiedad => !propiedad.IsNullable)
                .Select(Columna)
                .Where(columna => !string.Equals(columna, "xmin", StringComparison.Ordinal)),
        ];

        List<string> olvidadas = [.. obligatorias.Except(ColumnasInsertadas(), StringComparer.Ordinal)];

        olvidadas.ShouldBeEmpty(
            "estas columnas son obligatorias en el modelo y la sentencia no las rellena, así que " +
            "la primera petición con Idempotency-Key fallaría con una violación de NOT NULL: " +
            string.Join(", ", olvidadas));
    }

    [Fact]
    public void Ninguna_columna_de_la_sentencia_se_ha_inventado()
    {
        string[] delModelo = [.. Entidad().GetProperties().Select(Columna)];

        List<string> inventadas = [.. ColumnasInsertadas().Except(delModelo, StringComparer.Ordinal)];

        inventadas.ShouldBeEmpty(
            "estas columnas están en la sentencia y no en el modelo: " + string.Join(", ", inventadas));
    }

    // Un hueco de más deja un parámetro sin columna donde ponerlo; uno de menos, al revés. Las dos
    // cosas fallan en ejecución y ninguna en compilación.
    [Fact]
    public void Hay_un_hueco_por_columna_y_estan_numerados_en_orden()
    {
        string[] huecos =
        [
            .. Regex.Matches(AlmacenDeIdempotencia.SqlDeLaReclamacion, "\\{(\\d+)\\}")
                .Select(coincidencia => coincidencia.Groups[1].Value),
        ];

        string[] esperados =
        [
            .. Enumerable.Range(0, ColumnasInsertadas().Length)
                .Select(indice => indice.ToString(CultureInfo.InvariantCulture)),
        ];

        huecos.ShouldBe(esperados, ignoreOrder: false);
    }

    // Lo que la sentencia escribe es exactamente la identidad más la huella y el instante: ninguna
    // columna de la respuesta puede colarse aquí, porque la respuesta todavía no existe.
    [Fact]
    public void La_sentencia_escribe_la_identidad_mas_la_huella_y_el_instante()
    {
        string[] esperadas = [.. ColumnasDelConflicto(), .. s_fueraDeLaClave];

        ColumnasInsertadas().ShouldBe(esperadas, ignoreOrder: false);
    }

    private static string[] ColumnasInsertadas() =>
        EntreParentesisTrasEl("la lista de columnas", AlmacenDeIdempotencia.SqlDeLaReclamacion
            .IndexOf(" (", StringComparison.Ordinal));

    private static string[] ColumnasDelConflicto() =>
        EntreParentesisTrasEl("ON CONFLICT", AlmacenDeIdempotencia.SqlDeLaReclamacion
            .IndexOf("ON CONFLICT", StringComparison.Ordinal));

    private static string[] EntreParentesisTrasEl(string que, int desde)
    {
        desde.ShouldBeGreaterThanOrEqualTo(0, $"la sentencia ya no contiene «{que}»");

        string sql = AlmacenDeIdempotencia.SqlDeLaReclamacion;
        int abre = sql.IndexOf('(', desde);
        int cierra = sql.IndexOf(')', abre);

        abre.ShouldBeGreaterThanOrEqualTo(0);
        cierra.ShouldBeGreaterThan(abre);

        return
        [
            .. sql[(abre + 1)..cierra]
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
        ];
    }

    private static string Columna(IProperty propiedad) =>
        propiedad.GetColumnName(
            StoreObjectIdentifier.Create(propiedad.DeclaringType, StoreObjectType.Table)!.Value)!;

    private IEntityType Entidad()
    {
        using IServiceScope alcance = _api.Services.CreateScope();

        return alcance.ServiceProvider.GetRequiredService<AuditoriaDbContext>()
            .Model.FindEntityType(typeof(RegistroDeIdempotencia))!;
    }
}
