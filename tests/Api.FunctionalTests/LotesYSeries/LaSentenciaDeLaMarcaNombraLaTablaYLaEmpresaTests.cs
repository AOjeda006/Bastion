using System.Text.RegularExpressions;
using Bastion.Api.FunctionalTests.Salud;
using Bastion.Catalogo.Contracts.Catalogo;
using Bastion.Catalogo.Domain.Catalogo;
using Bastion.Catalogo.Infrastructure.Persistencia;
using Bastion.Inventario.Infrastructure.Persistencia.Repositorios;
using Bastion.Pruebas.Comun;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Bastion.Api.FunctionalTests.LotesYSeries;

/// <summary>
/// Las dos sentencias con las que Inventario lee la marca del artículo nombran la tabla del modelo
/// de Catálogo, llevan la cláusula que les toca y comparan la empresa ellas mismas.
/// </summary>
/// <remarks>
/// <para>
/// <b>El trato del puerto del ejercicio, y por lo mismo</b>
/// (<c>LaSentenciaDelEjercicioNombraLaTablaDeVerdadTests</c>). La tabla es de otro módulo y desde
/// Inventario no se ve, así que la sentencia la nombra en crudo, y una cadena no se entera de un
/// renombrado.
/// </para>
/// <para>
/// <b>La guarda comparte y la cortesía no bloquea.</b> Sin <c>FOR SHARE</c> al confirmar, un cambio
/// de marca se colaría entre la lectura y el <c>COMMIT</c>, y nada fallaría (ADR-0048 §4). Con él al
/// abrir, un borrador bloquearía la ficha sin transacción que lo sostuviera.
/// </para>
/// <para>
/// <b>Y los nombres de la marca son el contrato.</b> Catálogo guarda la marca como texto, con el
/// nombre del valor de su dominio, y el adaptador la traduce por los nombres del enumerado del
/// <c>Contracts</c>. Los dos enumerados tienen que tener los mismos nombres, o la primera
/// confirmación de un artículo con la marca nueva revienta en ejecución.
/// </para>
/// </remarks>
public sealed class LaSentenciaDeLaMarcaNombraLaTablaYLaEmpresaTests : IDisposable
{
    private readonly ApiSinDependencias _api = new();

    public void Dispose() => _api.Dispose();

    [Fact]
    public void Las_cadenas_del_puerto_son_las_del_modelo()
    {
        IEntityType articulo = Entidad();

        LaTrazabilidadDesdeInventario.Esquema.ShouldBe(articulo.GetSchema());
        LaTrazabilidadDesdeInventario.Tabla.ShouldBe(articulo.GetTableName());
        LaTrazabilidadDesdeInventario.ColumnaDeTrazabilidad.ShouldBe(
            Columna(articulo, nameof(Articulo.Trazabilidad)));
    }

    [Fact]
    public void Cada_columna_que_las_sentencias_nombran_existe_en_la_tabla()
    {
        // Se EXTRAEN de las cadenas: una lista copiada a mano seguiría verde el día que alguien
        // añadiera otra condición a la sentencia de verdad.
        List<string> encontradas = [.. Regex
            .Matches(
                LaTrazabilidadDesdeInventario.SqlDeLasMarcas + " " +
                LaTrazabilidadDesdeInventario.SqlDeLasMarcasConCerrojo,
                @"\ba\.([a-z_]+)\b",
                RegexOptions.None,
                TimeSpan.FromSeconds(1))
            .Select(coincidencia => coincidencia.Value)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)];

        // El barrido se afirma a sí mismo antes de mirar nada (ADR-0020).
        encontradas.ShouldBe(["a.empresa_id", "a.id", "a.trazabilidad"]);

        IEntityType articulo = Entidad();
        List<string> mapeadas =
            [.. articulo.GetProperties().Select(propiedad => Columna(articulo, propiedad.Name))];

        encontradas.Where(nombrada => !mapeadas.Contains(nombrada[2..], StringComparer.Ordinal))
            .ShouldBeEmpty("las sentencias de la marca nombran columnas que el modelo no mapea");
    }

    [Fact]
    public void La_guarda_comparte_y_la_cortesia_no_bloquea()
    {
        LaTrazabilidadDesdeInventario.SqlDeLasMarcasConCerrojo.ShouldEndWith(" FOR SHARE");
        LaTrazabilidadDesdeInventario.SqlDeLasMarcasConCerrojo.ShouldNotContain("UPDATE");

        LaTrazabilidadDesdeInventario.SqlDeLasMarcas.ShouldNotContain(" FOR ");
    }

    [Fact]
    public void Ninguna_de_las_dos_lleva_punto_y_coma_final()
    {
        // EF Core las envuelve en un `SELECT ... FROM (...)`, y ahí dentro un punto y coma es un
        // 42601 en ejecución (ADR-0043).
        LaTrazabilidadDesdeInventario.SqlDeLasMarcas.ShouldNotContain(";");
        LaTrazabilidadDesdeInventario.SqlDeLasMarcasConCerrojo.ShouldNotContain(";");
    }

    [Theory]
    [InlineData(nameof(LaTrazabilidadDesdeInventario.SqlDeLasMarcas))]
    [InlineData(nameof(LaTrazabilidadDesdeInventario.SqlDeLasMarcasConCerrojo))]
    public void Las_dos_comparan_la_empresa_contra_el_segundo_parametro(string cual)
    {
        // `{1}` y no otro: la llamada pasa `distintos, empresaId`, en ese orden, y un `{0}`
        // compararía la empresa contra la lista de artículos. Una por sentencia, porque la
        // concatenación de las dos seguiría nombrando la empresa aunque una la perdiera.
        string sentencia = (string)typeof(LaTrazabilidadDesdeInventario).GetField(cual)!.GetValue(null)!;
        string empresa = Columna(Entidad(), nameof(Articulo.EmpresaId));

        Match condicion = Regex.Match(
            sentencia,
            @"\b(?:WHERE|AND) a\." + Regex.Escape(empresa) + @" = \{(\d+)\}",
            RegexOptions.None,
            TimeSpan.FromSeconds(1));

        (condicion.Success ? condicion.Groups[1].Value : "(ninguna)").ShouldBe(
            "1", $"la sentencia {cual} ya no compara la empresa contra `{{1}}`: {sentencia}");
    }

    [Fact]
    public void El_puerto_no_deja_que_quien_llama_elija_la_empresa()
    {
        Parametros(nameof(IConsultaDeTrazabilidad.MarcasDeAsync)).ShouldBe(["articulos", "cancelacion"]);
        Parametros(nameof(IConsultaDeTrazabilidad.MarcasParaMoverAsync))
            .ShouldBe(["articulos", "cancelacion"]);
    }

    [Fact]
    public void El_valor_que_comparan_sale_del_inquilino_y_va_en_su_sitio()
    {
        // Qué va en la posición `{1}` es una línea de C#, y se lee del fuente.
        string fuente = File.ReadAllText(Path.Combine(
            RaizDelRepositorio.Ruta(),
            "src/Modules/Inventario/Bastion.Inventario.Infrastructure/Persistencia/Repositorios/" +
            "LaTrazabilidadDesdeInventario.cs"));

        // Primero que se ha leído algo: un fichero movido dejaría lo de abajo sobre una cadena
        // vacía.
        fuente.ShouldContain("SqlDeLasMarcasConCerrojo");

        fuente.ShouldContain("Guid empresaId = inquilino.EmpresaDelFiltro");
        fuente.ShouldContain("SqlQueryRaw<FilaDeMarca>(sql, distintos, empresaId)");
    }

    [Fact]
    public void La_marca_se_guarda_como_texto_con_los_nombres_que_el_puerto_traduce()
    {
        IProperty trazabilidad = Entidad().FindProperty(nameof(Articulo.Trazabilidad))!;

        (trazabilidad.GetTypeMapping().Converter?.ProviderClrType ?? trazabilidad.ClrType)
            .ShouldBe(typeof(string));

        // Los dos enumerados, con los mismos nombres. Los números no importan, y son distintos a
        // propósito: el del Contracts no tiene cero (su cabecera dice por qué).
        Enum.GetNames<MarcaDeTrazabilidad>().Order(StringComparer.Ordinal)
            .ShouldBe(Enum.GetNames<Trazabilidad>().Order(StringComparer.Ordinal));
    }

    private IEntityType Entidad()
    {
        using IServiceScope alcance = _api.Services.CreateScope();

        return alcance.ServiceProvider.GetRequiredService<CatalogoDbContext>()
            .Model.FindEntityType(typeof(Articulo))
            ?? throw new InvalidOperationException("Articulo no está en el modelo.");
    }

    private static string Columna(IEntityType entidad, string propiedad) =>
        entidad.FindProperty(propiedad)!.GetColumnName(
            StoreObjectIdentifier.Create(entidad, StoreObjectType.Table)!.Value)!;

    private static List<string> Parametros(string metodo) =>
        [.. typeof(IConsultaDeTrazabilidad).GetMethod(metodo)!.GetParameters().Select(parametro => parametro.Name!)];
}
