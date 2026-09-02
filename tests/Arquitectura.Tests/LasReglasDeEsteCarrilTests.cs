using System.Reflection;
using Shouldly;

namespace Bastion.Arquitectura.Tests;

/// <summary>
/// El barrido que se mira a sí mismo: qué reglas tiene este carril, nombradas una a una.
/// </summary>
/// <remarks>
/// <para>
/// Es la última rendija de la vacuidad, y la única que las demás no pueden tapar. Todo lo de este
/// proyecto está montado para que una regla no pueda quedarse mirando al vacío; nada de eso sirve
/// contra una regla <b>borrada</b>. Un <c>[Fact]</c> que desaparece no deja hueco: la suite sale
/// verde, más rápida, con un caso menos que nadie echa de menos, y la frontera que guardaba pasa a
/// no estar guardada por nadie.
/// </para>
/// <para>
/// Contra eso, la lista entera y comparada, que es la misma forma que usan los otros seis
/// barridos del proyecto. Y la lista de NOMBRES en vez de un recuento a secas: un número diría que
/// falta uno, y esto dice cuál.
/// </para>
/// </remarks>
public sealed class LasReglasDeEsteCarrilTests
{
    /// <summary>
    /// Las reglas de este carril, como <c>Clase.Metodo</c>. Añadir una obliga a escribir su línea
    /// aquí; quitarla, a borrarla — y las dos cosas son decisiones que merecen quedar en el
    /// historial de git en vez de pasar como un fichero con dos líneas menos.
    /// </summary>
    private static readonly string[] s_declaradas =
    [
        // El inventario: qué módulos hay, qué capas tienen y cuáles llevan tipos. De aquí cuelgan
        // todas las demás, porque son las que dan derecho a decir que una regla mira algo.
        "ElInventarioDeModulosTests.Cada_carpeta_de_modulo_tiene_sus_cinco_capas",
        "ElInventarioDeModulosTests.Cada_ensamblado_modular_lleva_los_tipos_que_el_inventario_declara",
        "ElInventarioDeModulosTests.Cada_tipo_vive_en_el_espacio_de_nombres_de_su_ensamblado",
        "ElInventarioDeModulosTests.El_bloque_comun_tiene_sus_tres_capas_y_todas_llevan_tipos",
        "ElInventarioDeModulosTests.El_mapa_de_modulos_declara_los_dieciseis_del_quinto_apartado",
        "ElInventarioDeModulosTests.Las_carpetas_de_modulo_son_las_declaradas",
        "ElInventarioDeModulosTests.Los_ensamblados_modulares_de_la_salida_son_los_declarados",

        // §4, regla 1 (y lo que se puede decir de la 5): entre módulos, solo por el contrato.
        "LasFronterasEntreModulosTests.El_unico_cruce_entre_modulos_va_por_contratos",
        "LasFronterasEntreModulosTests.Las_puertas_publicas_de_los_contratos_son_las_declaradas",
        "LasFronterasEntreModulosTests.Las_referencias_de_proyecto_son_las_declaradas",
        "LasFronterasEntreModulosTests.Ningun_modulo_ve_el_interior_de_otro",

        // §4, regla 2 y el reparto por capas: siempre hacia dentro.
        "LasCapasVanHaciaDentroTests.El_dominio_no_conoce_la_infraestructura_ni_el_framework",
        "LasCapasVanHaciaDentroTests.La_prohibicion_al_dominio_puede_dispararse",
        "LasCapasVanHaciaDentroTests.Ninguna_capa_mira_hacia_fuera_de_su_modulo",

        // Y esta.
        "LasReglasDeEsteCarrilTests.Las_reglas_de_este_carril_son_las_declaradas",
    ];

    [Fact]
    public void Las_reglas_de_este_carril_son_las_declaradas()
    {
        IReadOnlyList<string> encontradas =
        [
            .. from tipo in typeof(LasReglasDeEsteCarrilTests).Assembly.GetTypes()
               where tipo.IsPublic && tipo.IsClass
               from metodo in tipo.GetMethods(BindingFlags.Public | BindingFlags.Instance)
               where metodo.GetCustomAttribute<FactAttribute>() is not null
               orderby tipo.Name + "." + metodo.Name, StringComparer.Ordinal
               select tipo.Name + "." + metodo.Name,
        ];

        // Entera y en los dos sentidos, como los demás barridos. De menos: una regla borrada, y
        // aquí sale por su nombre. De más: una regla nueva sin declarar — que suena inocente y no
        // lo es, porque la que se añade sin pasar por esta lista es la que se añade sin que nadie
        // decida si de verdad protege algo.
        encontradas.ShouldBe([.. s_declaradas.Order(StringComparer.Ordinal)]);
    }
}
