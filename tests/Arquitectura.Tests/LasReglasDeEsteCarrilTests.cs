using Bastion.Pruebas.Comun;

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
/// <para>
/// <b>Desde el ítem 1.4 este ya no es el único carril censado</b>, y el descubrimiento se comparte
/// en vez de copiarse. Hasta entonces esta clase era la única, y las reglas que viven fuera de este
/// ensamblado —las del carril funcional, las de los dos de integración— se podían borrar sin que
/// nada se pusiera rojo. Cada uno tiene ahora su <c>ElCensoDeEsteCarrilTests</c> con su lista,
/// porque la reflexión solo alcanza al ensamblado propio; lo que no está repetido es la consulta
/// que descubre los casos, que vive enlazada en <c>tests/Comun/CensoDeReglas.cs</c>.
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
        "ElInventarioDeModulosTests.El_bloque_comun_tiene_las_capas_declaradas_y_todas_llevan_tipos",
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

        // ADR-0024, la cuarta vía: un identificador de otro módulo guardado sin nada que lo valide
        // no lo rechaza ni el SQL, ni el compilador, ni las tres reglas de arriba. Estas cinco
        // afirmaciones son las que lo rechazan.
        "LosIdentificadoresAjenosTests.Cada_modulo_de_la_lista_tiene_su_cruce_y_su_puerto",
        "LosIdentificadoresAjenosTests.Las_dos_fuentes_encuentran_algo",
        "LosIdentificadoresAjenosTests.Ningun_identificador_del_dominio_se_queda_sin_clasificar",
        "LosIdentificadoresAjenosTests.Toda_declaracion_sigue_correspondiendo_a_una_propiedad_del_dominio",
        "LosIdentificadoresAjenosTests.Todo_identificador_de_otro_modulo_esta_declarado_con_su_puerto",

        // El art. 32 alcanza a TODO lo que se bloquea: la lista cerrada que publica el listado y
        // la marca `IBloqueable` del dominio son el mismo conjunto, en las dos direcciones. La
        // doctrina estaba escrita en el comentario de la propia interfaz y el barrido no existía,
        // así que la lista se quedó vieja dos veces sin que nada se pusiera rojo.
        //
        // Y la cuarta afirmación es la que hace que las otras tres sirvan de algo: el enumerado es
        // una lista de NOMBRES, así que se pone verde en cuanto alguien añade el valor, y las filas
        // las trae quien implementa el puerto. Sin ella, el arreglo del ítem 1.6 se podía dar por
        // hecho añadiendo dos líneas a un `enum`.
        "LoBloqueadoSeVeEnteroTests.El_barrido_ve_los_agregados_que_se_bloquean",
        "LoBloqueadoSeVeEnteroTests.Ningun_valor_de_la_lista_nombra_algo_que_ya_no_se_bloquea",
        "LoBloqueadoSeVeEnteroTests.Todo_agregado_bloqueable_esta_en_la_lista_del_articulo_32",
        "LoBloqueadoSeVeEnteroTests.Todo_modulo_que_bloquea_contesta_por_lo_suyo",

        // El glosario del lenguaje ubicuo: su tabla de agregados y el dominio compilado son la
        // misma lista. Es lo único de docs/ que este carril vigila, y lo vigila porque es la
        // lista que dice qué cosas hay.
        "ElGlosarioDelDominioTests.Cada_agregado_del_glosario_dice_el_modulo_en_el_que_vive",
        "ElGlosarioDelDominioTests.La_tabla_de_agregados_del_glosario_se_lee_y_no_esta_vacia",
        "ElGlosarioDelDominioTests.Los_agregados_del_dominio_son_los_que_el_glosario_nombra",

        // Ningún identificador fiscal ni ningún IBAN con forma de real se queda escrito. El
        // ítem 1.5 lo dejó como prosa y lo limpió a mano; una limpieza a mano se deshace.
        "NingunDatoConFormaDeRealTests.Cada_detector_encuentra_lo_que_dice_buscar",
        "NingunDatoConFormaDeRealTests.El_barrido_lee_los_ficheros_del_repositorio",
        "NingunDatoConFormaDeRealTests.Las_formas_declaradas_son_las_que_el_barrido_detecta",
        "NingunDatoConFormaDeRealTests.Ningun_dato_con_forma_de_real_se_queda_escrito",

        // El número de un ADR lo lleva un fichero y solo uno. Esta regla nace de haberla
        // incumplido en este mismo ítem: dos ADR salieron 0031 y nada se puso rojo, porque son dos
        // ficheros de texto. El número es como se cita un ADR, así que compartirlo vuelve ambigua
        // toda cita ya escrita.
        "ElNumeroDeUnAdrEsSuyoYDeNadieMasTests.Cada_ADR_lleva_en_su_titulo_el_numero_de_su_nombre_de_fichero",
        "ElNumeroDeUnAdrEsSuyoYDeNadieMasTests.El_barrido_encuentra_los_ADR_del_repositorio",
        "ElNumeroDeUnAdrEsSuyoYDeNadieMasTests.Ningun_numero_de_ADR_lo_llevan_dos_ficheros",

        // Y esta.
        "LasReglasDeEsteCarrilTests.Las_reglas_de_este_carril_son_las_declaradas",
    ];

    [Fact]
    public void Las_reglas_de_este_carril_son_las_declaradas() =>
        CensoDeReglas.Comprobar(typeof(LasReglasDeEsteCarrilTests).Assembly, s_declaradas);
}
