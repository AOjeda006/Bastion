using System.Globalization;
using NetArchTest.Rules;
using Shouldly;

namespace Bastion.Arquitectura.Tests;

/// <summary>
/// El barrido que sostiene a todos los demás: qué módulos hay, qué capas tienen y cuáles llevan
/// tipos. Sin esto, cualquier regla de frontera puede estar mirando a un conjunto vacío.
/// </summary>
/// <remarks>
/// <para>
/// Es el sexto barrido de lista entera del proyecto, y tiene la misma forma que los cinco
/// anteriores: se DESCUBRE el conjunto real, se compara ENTERO contra el declarado y en los dos
/// sentidos. De más es alguien que ha empezado algo sin decirlo; de menos es algo que ha
/// desaparecido y que las reglas han dejado de mirar sin avisar.
/// </para>
/// <para>
/// El motivo de que exista está en el §2 del encargo: trece de los dieciséis módulos del §5
/// todavía no existen. Escribir sus reglas por nombre daría trece baterías verdes que no miran
/// nada. Así que las reglas se aplican a lo que se descubre, y lo que se descubre se compara.
/// </para>
/// </remarks>
public sealed class ElInventarioDeModulosTests
{
    [Fact]
    public void El_mapa_de_modulos_declara_los_dieciseis_del_quinto_apartado()
    {
        // El §5 del plan maestro lista dieciséis módulos. El número está aquí porque es el único
        // dato de este fichero que NO se puede descubrir: el mapa de módulos vive fuera del
        // repositorio. Si algún día son diecisiete, esta línea es la que obliga a mirarlo.
        Inventario.Modulos.Count.ShouldBe(
            16,
            "el §5 del plan maestro lista dieciséis módulos y el inventario declara " +
            Inventario.Modulos.Count.ToString(CultureInfo.InvariantCulture));
    }

    [Fact]
    public void Las_carpetas_de_modulo_son_las_declaradas()
    {
        IReadOnlyList<string> enDisco =
        [
            .. Directory.EnumerateDirectories(Path.Combine(Ensamblados.Raiz(), "src", "Modules"))
                .Select(Path.GetFileName)
                .Select(nombre => nombre!)
                .Order(StringComparer.Ordinal),
        ];

        IReadOnlyList<string> declaradas =
        [
            .. Inventario.Modulos
                .Where(par => par.Value != Presencia.SinCarpeta)
                .Select(par => par.Key)
                .Order(StringComparer.Ordinal),
        ];

        // Enteras y en los dos sentidos. De más: alguien ha creado un módulo y no lo ha declarado,
        // así que ninguna regla de frontera lo mira. De menos: una carpeta que se ha ido y trece
        // reglas que ahora se aplican a un módulo menos sin decirlo.
        enDisco.ShouldBe(declaradas);
    }

    [Fact]
    public void Cada_carpeta_de_modulo_tiene_sus_cinco_capas()
    {
        List<string> mal = [];

        foreach (string modulo in Inventario.Modulos
            .Where(par => par.Value != Presencia.SinCarpeta)
            .Select(par => par.Key))
        {
            string carpeta = Path.Combine(Ensamblados.Raiz(), "src", "Modules", modulo);

            IReadOnlyList<string> capas =
            [
                .. Directory.EnumerateDirectories(carpeta)
                    .Select(Path.GetFileName)
                    .Select(nombre => nombre!)
                    .Order(StringComparer.Ordinal),
            ];

            IReadOnlyList<string> esperadas =
            [
                .. Inventario.Capas
                    .Select(capa => Inventario.Raiz + "." + modulo + "." + capa)
                    .Order(StringComparer.Ordinal),
            ];

            if (!capas.SequenceEqual(esperadas, StringComparer.Ordinal))
            {
                mal.Add($"{modulo}: {string.Join(", ", capas)}");
            }
        }

        // El andamio de las cinco capas se pone en el 0.1 y se llena por fases. Una carpeta con
        // cuatro es un módulo al que le falta sitio donde poner algo, y se descubre el día que hay
        // que ponerlo — que es el peor día.
        mal.ShouldBeEmpty(
            "estos módulos no tienen las cinco capas del §4:" + Environment.NewLine +
            Ensamblados.Enumerar(mal));
    }

    [Fact]
    public void Los_ensamblados_modulares_de_la_salida_son_los_declarados()
    {
        IReadOnlyList<string> esperados =
        [
            .. from par in Inventario.Modulos
               where par.Value == Presencia.Montado
               from capa in Inventario.Capas
               orderby par.Key + "." + capa, StringComparer.Ordinal
               select par.Key + "." + capa,
        ];

        // Un módulo montado compila sus cinco capas, así que sus cinco ensamblados tienen que
        // estar en la salida de este proyecto de test. Que estén es lo que permite decir que las
        // reglas los cubren; si uno dejara de copiarse, sus fronteras dejarían de comprobarse en
        // silencio y este es el único sitio donde se notaría.
        Ensamblados.Modulares.Keys.Order(StringComparer.Ordinal).ShouldBe(esperados);
    }

    [Fact]
    public void Cada_ensamblado_modular_lleva_los_tipos_que_el_inventario_declara()
    {
        IReadOnlyList<string> conTipos =
        [
            .. Ensamblados.Modulares
                .Where(par => Ensamblados.Tipos(par.Value) > 0)
                .Select(par => par.Key)
                .Order(StringComparer.Ordinal),
        ];

        // ESTE es el test del que cuelga todo el carril. Un ensamblado vacío cumple cualquier
        // regla —no hay ningún tipo que la incumpla— así que una frontera aplicada a un ensamblado
        // vacío es verde para siempre. La lista declarada dice cuáles llevan tipos hoy; mientras
        // las dos cuadren, ninguna regla puede estar mirando al vacío sin que alguien lo sepa.
        //
        // Hoy no cuadran por casualidad: Auditoría tiene cuatro de sus cinco capas vacías, y está
        // escrito en el inventario. El día que estrene su primera entidad, esto se pone rojo y
        // obliga a añadir la línea — que es el día en que esas reglas empiezan a proteger algo.
        conTipos.ShouldBe([.. Inventario.EnsambladosConTipos.Order(StringComparer.Ordinal)]);
    }

    [Fact]
    public void El_bloque_comun_tiene_sus_tres_capas_y_todas_llevan_tipos()
    {
        IReadOnlyList<string> conTipos =
        [
            .. Ensamblados.Comunes
                .Where(par => Ensamblados.Tipos(par.Value) > 0)
                .Select(par => par.Key)
                .Order(StringComparer.Ordinal),
        ];

        // `BuildingBlocks` no es un módulo y no entra en el mapa del §5, pero la regla 2 le vale
        // igual y de hecho le vale más: una fuga de infraestructura en el DOMINIO COMÚN la heredan
        // los dieciséis módulos a la vez. Se comprueba aquí que sus tres capas existen y llevan
        // tipos, porque es lo que da derecho a aplicarles la regla en el fichero de al lado.
        conTipos.ShouldBe([.. Inventario.ComunesConTipos.Order(StringComparer.Ordinal)]);
    }

    [Fact]
    public void Cada_tipo_vive_en_el_espacio_de_nombres_de_su_ensamblado()
    {
        // Todas las prohibiciones de este carril se escriben como prefijos de espacio de nombres
        // —«Bastion.Organizacion.Domain»— y eso solo vale mientras el espacio de nombres siga el
        // nombre del ensamblado. Un tipo colocado en otro sitio no lo alcanzaría ninguna regla, y
        // no habría nada rojo: sería un tipo invisible para las fronteras.
        foreach (string clave in Inventario.EnsambladosConTipos.Concat(Inventario.ComunesConTipos))
        {
            Barrido.Exige(
                $"todo tipo de {Inventario.Raiz}.{clave} vive en su propio espacio de nombres",
                [clave],
                tipos => tipos.Should().ResideInNamespace(Inventario.Raiz + "." + clave));
        }
    }
}
