using System.Text.RegularExpressions;
using Bastion.Api.FunctionalTests.Salud;
using Bastion.Inventario.Application.Recuentos;
using Bastion.Inventario.Domain.Recuentos;
using Bastion.Inventario.Infrastructure.Persistencia;
using Bastion.Inventario.Infrastructure.Persistencia.Repositorios;
using Bastion.Pruebas.Comun;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Bastion.Api.FunctionalTests.Recuentos;

/// <summary>
/// La sentencia del cerrojo del recuento nombra la tabla del modelo, lleva la cláusula que le toca
/// y compara la empresa ella misma, con el valor del inquilino.
/// </summary>
/// <remarks>
/// <para>
/// <b>El mismo trato que el cerrojo del artículo</b>
/// (<c>LaSentenciaDelArticuloNombraLaTablaYLaEmpresaTests</c>): la cláusula de bloqueo no tiene
/// traductor en EF Core, así que la sentencia va escrita a mano, y una cadena no se entera de un
/// renombrado.
/// </para>
/// <para>
/// <b>Aquí la empresa sí la distingue un caso por la API</b>: en
/// <c>LasCarrerasDeLasLineasDelRecuentoTests</c>, otra empresa no espera al cerrojo de un recuento
/// que no es suyo. Ese caso necesita Docker y tarda; este lee la cadena en el carril rápido, y dice
/// qué parte de la sentencia se ha movido en vez de dar un plazo vencido.
/// </para>
/// <para>
/// <b>Y la tabla se saca de la propia sentencia</b>, no de una constante: la sentencia la escribe
/// entera, así que lo que se compara con el modelo es lo que de verdad va a la base.
/// </para>
/// </remarks>
public sealed class LaSentenciaDelRecuentoNombraLaTablaYLaEmpresaTests : IDisposable
{
    private readonly ApiSinDependencias _api = new();

    public void Dispose() => _api.Dispose();

    [Fact]
    public void La_tabla_que_la_sentencia_bloquea_es_la_del_modelo()
    {
        Match desde = Regex.Match(
            RepositorioDeRecuentos.SqlDelCerrojo,
            @"\bFROM ([a-z_]+)\.([a-z_]+) AS r\b",
            RegexOptions.None,
            TimeSpan.FromSeconds(1));

        // Primero que la ha encontrado: sin esto, una sentencia reescrita sin alias compararía dos
        // cadenas vacías.
        desde.Success.ShouldBeTrue(RepositorioDeRecuentos.SqlDelCerrojo);

        IEntityType recuento = Entidad();

        desde.Groups[1].Value.ShouldBe(recuento.GetSchema());
        desde.Groups[2].Value.ShouldBe(recuento.GetTableName());
    }

    [Fact]
    public void Cada_columna_que_la_sentencia_nombra_existe_en_la_tabla()
    {
        List<string> encontradas = [.. Regex
            .Matches(
                RepositorioDeRecuentos.SqlDelCerrojo,
                @"\br\.([a-z_]+)\b",
                RegexOptions.None,
                TimeSpan.FromSeconds(1))
            .Select(coincidencia => coincidencia.Value)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)];

        // El barrido se afirma a sí mismo antes de mirar nada (ADR-0020).
        encontradas.ShouldBe(["r.empresa_id", "r.id"]);

        IEntityType recuento = Entidad();
        List<string> mapeadas =
            [.. recuento.GetProperties().Select(propiedad => Columna(recuento, propiedad.Name))];

        encontradas.Where(nombrada => !mapeadas.Contains(nombrada[2..], StringComparer.Ordinal))
            .ShouldBeEmpty("la sentencia del cerrojo nombra columnas que el modelo no mapea");
    }

    [Fact]
    public void Es_el_cerrojo_del_UPDATE_y_no_otro()
    {
        // `FOR UPDATE` también chocaría con el `FOR KEY SHARE` con el que una línea nueva mira que
        // su recuento existe, y añadir esperaría a contar sin necesidad (ADR-0055 §4).
        RepositorioDeRecuentos.SqlDelCerrojo.ShouldEndWith(" FOR NO KEY UPDATE");
        RepositorioDeRecuentos.SqlDelCerrojo.ShouldNotContain("FOR SHARE");
        RepositorioDeRecuentos.SqlDelCerrojo.ShouldNotContain("FOR UPDATE");
    }

    [Fact]
    public void No_lleva_punto_y_coma_final()
    {
        // EF Core la envuelve en un `SELECT ... FROM (...)`, y ahí dentro un punto y coma es un
        // 42601 en ejecución (ADR-0043).
        RepositorioDeRecuentos.SqlDelCerrojo.ShouldNotContain(";");
    }

    [Fact]
    public void Compara_el_recuento_contra_el_primer_parametro_y_la_empresa_contra_el_segundo()
    {
        // La llamada pasa `id, empresaId`, en ese orden: con los dos cambiados, la sentencia no
        // bloquearía nada y todo sería un 404.
        Posicion(Columna(Entidad(), nameof(Recuento.Id))).ShouldBe("0", RepositorioDeRecuentos.SqlDelCerrojo);
        Posicion(Columna(Entidad(), nameof(Recuento.EmpresaId))).ShouldBe(
            "1", RepositorioDeRecuentos.SqlDelCerrojo);
    }

    [Fact]
    public void El_puerto_no_deja_que_quien_llama_elija_la_empresa()
    {
        typeof(IRepositorioDeRecuentos)
            .GetMethod(nameof(IRepositorioDeRecuentos.BloquearAsync))!
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
            "src/Modules/Inventario/Bastion.Inventario.Infrastructure/Persistencia/Repositorios/" +
            "RepositorioDeRecuentos.cs"));

        // Primero que se ha leído algo: un fichero movido dejaría lo de abajo sobre una cadena
        // vacía.
        fuente.ShouldContain("SqlDelCerrojo");

        fuente.ShouldContain("Guid empresaId = inquilino.EmpresaDelFiltro");
        fuente.ShouldContain("SqlQueryRaw<Guid>(SqlDelCerrojo, id, empresaId)");
    }

    private static string Posicion(string columna)
    {
        Match condicion = Regex.Match(
            RepositorioDeRecuentos.SqlDelCerrojo,
            @"\b(?:WHERE|AND) r\." + Regex.Escape(columna) + @" = \{(\d+)\}",
            RegexOptions.None,
            TimeSpan.FromSeconds(1));

        return condicion.Success ? condicion.Groups[1].Value : "(ninguna)";
    }

    private IEntityType Entidad()
    {
        using IServiceScope alcance = _api.Services.CreateScope();

        return alcance.ServiceProvider.GetRequiredService<InventarioDbContext>()
            .Model.FindEntityType(typeof(Recuento))
            ?? throw new InvalidOperationException("Recuento no está en el modelo.");
    }

    private static string Columna(IEntityType entidad, string propiedad) =>
        entidad.FindProperty(propiedad)!.GetColumnName(
            StoreObjectIdentifier.Create(entidad, StoreObjectType.Table)!.Value)!;
}
