using System.Globalization;
using System.Text;
using Bastion.BuildingBlocks.Domain.Entidades;
using Shouldly;

namespace Bastion.Arquitectura.Tests;

/// <summary>
/// El glosario del lenguaje ubicuo y los agregados del dominio son la misma lista.
/// </summary>
/// <remarks>
/// <para>
/// <b>Un glosario es documentación, y la documentación miente en silencio.</b> Este no puede:
/// <c>docs/dominio/glosario.md</c> nombra los agregados en una tabla, y aquí se comparan esa tabla
/// y el dominio COMPILADO, enteros y en los dos sentidos. Un agregado nuevo sin entrada pone esto
/// en rojo el día que se escribe; una entrada que sobra —el término de algo que se renombró o se
/// borró— también. Sin la comparación, un glosario es un fichero que alguien leyó una vez.
/// </para>
/// <para>
/// <b>Qué cuenta como agregado, dicho una sola vez.</b> Lo que hereda de
/// <see cref="EntidadBase"/>: «toda entidad que es un recurso por sí misma», que es exactamente lo
/// que esa clase base dice de sí misma. Incluye a <c>RaizAgregado</c>, que hereda de ella. Deja
/// fuera a las entidades hijas —<c>Membresia</c>, <c>PermisoDeRol</c>, <c>TokenDeRefresco</c>,
/// <c>RolDeMembresia</c>—, que no se dan de alta solas, y a los objetos de valor, que no son
/// entidades. La definición no está escrita dos veces: el glosario la cita y esta regla la ejerce.
/// </para>
/// <para>
/// <b>Se recorre también el dominio común</b> aunque hoy no tenga ningún agregado. Si el alcance
/// fueran solo los módulos, una entidad concreta puesta en <c>BuildingBlocks.Domain</c> no
/// existiría para esta comparación, y ese es justo el sitio donde menos se la echaría de menos.
/// </para>
/// </remarks>
public sealed class ElGlosarioDelDominioTests
{
    private const string Glosario = "docs/dominio/glosario.md";
    private const string Seccion = "## Agregados";

    [Fact]
    public void La_tabla_de_agregados_del_glosario_se_lee_y_no_esta_vacia()
    {
        // La afirmación de conjunto no vacío, por triplicado, porque hay tres maneras distintas de
        // que esta comparación acabe comparando la nada: que el fichero no esté donde se busca, que
        // el trozo de tabla no se sepa leer —un encabezado renombrado, un formato distinto— y que
        // no quede ningún ensamblado de dominio en el alcance.
        File.Exists(Path.Combine(Ensamblados.Raiz(), Glosario)).ShouldBeTrue(
            $"no hay glosario en {Glosario}: sin él esta regla no compara nada");

        FilasDelGlosario().ShouldNotBeEmpty(
            $"no se ha podido leer ninguna fila de la sección «{Seccion}» de {Glosario}. O la " +
            "tabla está vacía, o el formato ha cambiado y este lector se ha quedado mirando cero " +
            "filas — que sale verde igual de bien que un glosario correcto");

        EnsambladosDeDominio().ShouldContain(
            "BuildingBlocks.Domain",
            "el dominio común no está en el alcance, así que un agregado escrito ahí no lo vería " +
            "nadie");

        TiposDelDominio().ShouldNotBeEmpty("no se ha encontrado ni un tipo en el dominio");
    }

    [Fact]
    public void Los_agregados_del_dominio_son_los_que_el_glosario_nombra()
    {
        IReadOnlyList<string> enElDominio =
        [
            .. from tipo in TiposDelDominio()
               where !tipo.IsAbstract && typeof(EntidadBase).IsAssignableFrom(tipo)
               orderby tipo.Name, StringComparer.Ordinal
               select tipo.Name,
        ];

        IReadOnlyList<string> enElGlosario =
            [.. FilasDelGlosario().Select(fila => fila.Tipo).Order(StringComparer.Ordinal)];

        enElDominio.ShouldNotBeEmpty("no hay ningún agregado en el dominio");

        // Entera y en los dos sentidos. La diferencia se imprime a los dos lados porque las dos
        // faltas se arreglan en sitios distintos: la de menos, escribiendo la entrada; la de más,
        // borrándola —o dándose cuenta de que el agregado se renombró y el glosario no—.
        enElGlosario.ShouldBe(
            enElDominio,
            $"la tabla de agregados de {Glosario} y el dominio no dicen lo mismo. Sobran en el " +
            $"glosario: [{string.Join(", ", enElGlosario.Except(enElDominio, StringComparer.Ordinal))}]. " +
            $"Faltan: [{string.Join(", ", enElDominio.Except(enElGlosario, StringComparer.Ordinal))}].");
    }

    [Fact]
    public void Cada_agregado_del_glosario_dice_el_modulo_en_el_que_vive()
    {
        SortedDictionary<string, string> moduloReal =
            new(
                (from clave in EnsambladosDeDominio()
                 from tipo in Ensamblados.Todos[clave].GetTypes()
                 where tipo.IsPublic && !tipo.IsAbstract && typeof(EntidadBase).IsAssignableFrom(tipo)
                 select new { tipo.Name, Modulo = clave.Split('.')[0] })
                .ToDictionary(cual => cual.Name, cual => cual.Modulo, StringComparer.Ordinal),
                StringComparer.Ordinal);

        moduloReal.ShouldNotBeEmpty("no hay ningún agregado del que comprobar el módulo");

        // El módulo se escribe en el glosario con su nombre de persona —«Organización», con
        // tilde— y en el ensamblado sin ella (Anexo A.1). Compararlos exige quitar las tildes, no
        // quitar la tilde del documento: el glosario lo lee gente.
        List<string> descolocados =
        [
            .. from fila in FilasDelGlosario()
               where moduloReal.TryGetValue(fila.Tipo, out string? donde)
                  && !string.Equals(SinTildes(fila.Modulo), donde, StringComparison.OrdinalIgnoreCase)
               select $"{fila.Tipo}: el glosario dice «{fila.Modulo}» y vive en {moduloReal[fila.Tipo]}",
        ];

        descolocados.ShouldBeEmpty(
            "hay agregados atribuidos al módulo equivocado: " + string.Join("; ", descolocados));
    }

    /// <summary>Los ensamblados de dominio del alcance: los de módulo con tipos, y el común.</summary>
    private static IReadOnlyList<string> EnsambladosDeDominio() =>
    [
        .. from clave in Ensamblados.Todos.Keys
           where clave.EndsWith(".Domain", StringComparison.Ordinal)
              && (Inventario.EnsambladosConTipos.Contains(clave)
                  || Inventario.ComunesConTipos.Contains(clave))
           orderby clave, StringComparer.Ordinal
           select clave,
    ];

    private static IReadOnlyList<Type> TiposDelDominio() =>
    [
        .. from clave in EnsambladosDeDominio()
           from tipo in Ensamblados.Todos[clave].GetTypes()
           where tipo.IsPublic
           select tipo,
    ];

    /// <summary>
    /// Las filas de la tabla de agregados: el tipo (segunda columna, entre comillas invertidas) y
    /// el módulo (tercera).
    /// </summary>
    /// <remarks>
    /// Se lee el Markdown a mano y no con una biblioteca porque lo que hace falta es una cosa y
    /// muy concreta, y una dependencia nueva para esto costaría más de lo que ahorra. La lectura
    /// se detiene en el siguiente <c>##</c>: así una tabla de otra sección no se cuela.
    /// </remarks>
    private static List<Fila> FilasDelGlosario()
    {
        string ruta = Path.Combine(Ensamblados.Raiz(), Glosario);

        if (!File.Exists(ruta))
        {
            return [];
        }

        List<Fila> filas = [];
        bool dentro = false;

        foreach (string linea in File.ReadLines(ruta))
        {
            if (linea.StartsWith("## ", StringComparison.Ordinal))
            {
                dentro = string.Equals(linea.Trim(), Seccion, StringComparison.Ordinal);
                continue;
            }

            if (!dentro || !linea.TrimStart().StartsWith('|'))
            {
                continue;
            }

            string[] celdas = [.. linea.Trim().Trim('|').Split('|').Select(celda => celda.Trim())];

            // El encabezado y la línea de guiones de la tabla no son datos.
            if (celdas.Length < 3 || celdas[1].StartsWith("---", StringComparison.Ordinal)
                || !celdas[1].StartsWith('`'))
            {
                continue;
            }

            filas.Add(new Fila(celdas[1].Trim('`'), celdas[2]));
        }

        return filas;
    }

    private static string SinTildes(string texto)
    {
        StringBuilder limpio = new(texto.Length);

        foreach (char letra in texto.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(letra) != UnicodeCategory.NonSpacingMark)
            {
                limpio.Append(letra);
            }
        }

        return limpio.ToString().Normalize(NormalizationForm.FormC);
    }

    /// <summary>Una fila de la tabla de agregados del glosario.</summary>
    /// <param name="Tipo">El nombre del tipo de dominio, sin las comillas invertidas.</param>
    /// <param name="Modulo">El módulo en el que el glosario dice que vive, con su tilde.</param>
    private sealed record Fila(string Tipo, string Modulo);
}
