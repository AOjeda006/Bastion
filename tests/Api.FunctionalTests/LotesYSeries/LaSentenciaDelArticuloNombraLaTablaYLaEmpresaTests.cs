using System.Text.RegularExpressions;
using Bastion.Api.FunctionalTests.Salud;
using Bastion.Catalogo.Application.Catalogo;
using Bastion.Catalogo.Domain.Catalogo;
using Bastion.Catalogo.Infrastructure.Persistencia;
using Bastion.Catalogo.Infrastructure.Persistencia.Repositorios;
using Bastion.Pruebas.Comun;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Bastion.Api.FunctionalTests.LotesYSeries;

/// <summary>
/// La sentencia del cerrojo del artículo nombra la tabla del modelo, lleva la cláusula que le
/// toca y compara la empresa ella misma, con el valor del inquilino.
/// </summary>
/// <remarks>
/// <para>
/// <b>El mismo trato que el cerrojo del ejercicio, y por lo mismo</b>
/// (<c>LaSentenciaDelEjercicioNombraLaTablaDeVerdadTests</c> y
/// <c>LaSentenciaDelEjercicioMiraLaEmpresaTests</c>). La cláusula de bloqueo no tiene traductor en
/// EF Core, así que la sentencia va escrita a mano, y una cadena no se entera de un renombrado.
/// </para>
/// <para>
/// <b>La empresa, sobre todo.</b> El identificador del artículo llega de la ruta. Sin la
/// comparación, el cerrojo bloquearía la fila de un artículo ajeno y contestaría que existe; la
/// lectura por el ORM, que sí lleva el filtro, diría después que no, y el <c>404</c> saldría igual.
/// Ningún caso por la API lo distingue: por eso se lee la cadena, como en el 2.6.
/// </para>
/// <para>
/// <b>Y la cláusula es la del <c>UPDATE</c>.</b> <c>FOR NO KEY UPDATE</c> choca con el
/// <c>FOR SHARE</c> con el que Inventario lee la marca. <c>FOR SHARE</c> aquí no esperaría a
/// ninguna confirmación, porque dos compartidos conviven, y el mecanismo quedaría de adorno sin
/// que nada fallase (ADR-0048 §4).
/// </para>
/// </remarks>
public sealed class LaSentenciaDelArticuloNombraLaTablaYLaEmpresaTests : IDisposable
{
    private readonly ApiSinDependencias _api = new();

    public void Dispose() => _api.Dispose();

    [Fact]
    public void Las_cadenas_del_cerrojo_son_las_del_modelo()
    {
        IEntityType articulo = Entidad();

        CerrojoDeArticulos.Esquema.ShouldBe(articulo.GetSchema());
        CerrojoDeArticulos.Tabla.ShouldBe(articulo.GetTableName());
        CerrojoDeArticulos.ColumnaDeEmpresa.ShouldBe(
            Columna(articulo, nameof(Articulo.EmpresaId)));
    }

    [Fact]
    public void Cada_columna_que_la_sentencia_nombra_existe_en_la_tabla()
    {
        // Se EXTRAEN de la cadena: una lista copiada a mano seguiría verde el día que alguien
        // añadiera otra condición a la sentencia de verdad.
        List<string> encontradas = [.. Regex
            .Matches(
                CerrojoDeArticulos.SqlDelCerrojo,
                @"\ba\.([a-z_]+)\b",
                RegexOptions.None,
                TimeSpan.FromSeconds(1))
            .Select(coincidencia => coincidencia.Value)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)];

        // El barrido se afirma a sí mismo antes de mirar nada (ADR-0020).
        encontradas.ShouldBe(["a.empresa_id", "a.id"]);

        IEntityType articulo = Entidad();
        List<string> mapeadas =
            [.. articulo.GetProperties().Select(propiedad => Columna(articulo, propiedad.Name))];

        encontradas.Where(nombrada => !mapeadas.Contains(nombrada[2..], StringComparer.Ordinal))
            .ShouldBeEmpty("la sentencia del cerrojo nombra columnas que el modelo no mapea");
    }

    [Fact]
    public void Es_el_cerrojo_del_UPDATE_y_no_otro()
    {
        CerrojoDeArticulos.SqlDelCerrojo.ShouldEndWith(" FOR NO KEY UPDATE");
        CerrojoDeArticulos.SqlDelCerrojo.ShouldNotContain("FOR SHARE");
        CerrojoDeArticulos.SqlDelCerrojo.ShouldNotContain("FOR UPDATE");
    }

    [Fact]
    public void No_lleva_punto_y_coma_final()
    {
        // EF Core la envuelve en un `SELECT ... FROM (...)`, y ahí dentro un punto y coma es un
        // 42601 en ejecución (ADR-0043).
        CerrojoDeArticulos.SqlDelCerrojo.ShouldNotContain(";");
    }

    [Fact]
    public void Compara_la_empresa_contra_el_segundo_parametro()
    {
        // `{1}` y no otro: la llamada pasa `id, empresaId`, en ese orden, y un `{0}` compararía la
        // empresa contra el identificador del artículo.
        string empresa = Columna(Entidad(), nameof(Articulo.EmpresaId));

        Match condicion = Regex.Match(
            CerrojoDeArticulos.SqlDelCerrojo,
            @"\b(?:WHERE|AND) a\." + Regex.Escape(empresa) + @" = \{(\d+)\}",
            RegexOptions.None,
            TimeSpan.FromSeconds(1));

        (condicion.Success ? condicion.Groups[1].Value : "(ninguna)").ShouldBe(
            "1", "el cerrojo del artículo ya no compara la empresa contra `{1}`: " +
            CerrojoDeArticulos.SqlDelCerrojo);
    }

    [Fact]
    public void El_puerto_no_deja_que_quien_llama_elija_la_empresa()
    {
        typeof(ICerrojoDeArticulos)
            .GetMethod(nameof(ICerrojoDeArticulos.TomarEnExclusivaAsync))!
            .GetParameters()
            .Select(parametro => parametro.Name!)
            .ShouldBe(["id", "cancelacion"]);
    }

    [Fact]
    public void El_valor_que_compara_sale_del_inquilino_y_va_en_su_sitio()
    {
        // Qué va en la posición `{1}` es una línea de C#, y se lee del fuente.
        string fuente = File.ReadAllText(Path.Combine(
            RaizDelRepositorio.Ruta(),
            "src/Modules/Catalogo/Bastion.Catalogo.Infrastructure/Persistencia/Repositorios/" +
            "CerrojoDeArticulos.cs"));

        // Primero que se ha leído algo: un fichero movido dejaría lo de abajo sobre una cadena
        // vacía.
        fuente.ShouldContain("SqlDelCerrojo");

        fuente.ShouldContain("Guid empresaId = inquilino.EmpresaDelFiltro");
        fuente.ShouldContain("SqlQueryRaw<Guid>(SqlDelCerrojo, id, empresaId)");
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
}
