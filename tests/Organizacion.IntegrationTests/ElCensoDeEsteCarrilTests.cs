using Bastion.Pruebas.Comun;

namespace Bastion.Organizacion.IntegrationTests;

/// <summary>
/// El censo del carril de integración de Organización: sus casos, nombrados uno a uno.
/// </summary>
/// <remarks>
/// <para>
/// Este carril comprueba el esquema del módulo tal como queda en la base, sus semillas y la traducción a SQL de sus
/// listados. Casi todos sus casos son <b>reglas</b>: afirmaciones sobre un
/// universo que se descubre, no sobre un ejemplo escrito a mano. Y una regla borrada no deja hueco
/// — la suite sale verde, más rápida, y la frontera que guardaba pasa a no estar guardada por
/// nadie. Esta lista es lo único que lo nota.
/// </para>
/// <para>
/// <b>Entró en el ítem 1.4 y no antes por una razón de coste medida</b>, no de gusto: el censo
/// existía desde el 0.16 y solo cubría el carril de arquitectura, veintitrés casos. El 1.3 añadió
/// seis reglas nuevas fuera de ese carril, el 1.4 añade las suyas y el 1.5 traerá el barrido del
/// ADR-0030. La rendija crecía más rápido que el trabajo de taparla, así que se tapa ahora, cuando
/// taparla es <b>extender</b> el censo a los carriles que ya tienen reglas y no inventar nada.
/// </para>
/// <para>
/// <b>El descubrimiento es compartido y enlazado</b> (<c>tests/Comun/CensoDeReglas.cs</c>): la
/// lista de nombres es de cada carril, pero la consulta que los encuentra es una sola. Copiada, el
/// día que una versión contara los <c>[Theory]</c> y otra no, el carril que se quedara atrás
/// dejaría de censar justo lo que dejó de contar.
/// </para>
/// <para>
/// <b>No lleva <c>Category=Integracion</c>, y su ausencia es deliberada.</b> Esto es reflexión
/// sobre el ensamblado ya compilado: no abre conexiones, no levanta contenedores y no tarda. Correr
/// solo con Docker delante lo dejaría fuera del carril rápido, que es donde se nota antes que
/// alguien ha borrado una regla. El precio es que este ensamblado corre en los dos carriles, y por
/// eso está declarado en los dos en <c>ci.yml</c>.
/// </para>
/// </remarks>
public sealed class ElCensoDeEsteCarrilTests
{
    /// <summary>
    /// Los casos de este carril, como <c>Clase.Metodo</c>. Añadir uno obliga a escribir su línea
    /// aquí; quitarlo, a borrarla — y las dos cosas son decisiones que merecen quedar en el
    /// historial de git en vez de pasar como un fichero con dos líneas menos.
    /// </summary>
    private static readonly string[] s_declarados =
    [
        "ElCensoDeEsteCarrilTests.Los_casos_de_este_carril_son_los_declarados",

        "EsquemaDelModuloTests.El_NIF_lleva_tope_porque_su_longitud_es_una_regla_y_no_una_estimacion",
        "EsquemaDelModuloTests.El_contador_de_la_serie_es_una_columna_y_NO_una_secuencia_de_PostgreSQL",
        "EsquemaDelModuloTests.El_domicilio_fiscal_esta_en_campos_estructurados",
        "EsquemaDelModuloTests.El_historial_de_migraciones_vive_en_el_esquema_del_modulo_y_no_en_public",
        "EsquemaDelModuloTests.En_el_esquema_public_no_hay_ni_una_tabla_del_modulo",
        "EsquemaDelModuloTests.La_empresa_no_lleva_empresa_id_porque_ella_es_el_inquilino",
        "EsquemaDelModuloTests.Las_cuatro_tablas_estan_en_el_esquema_del_modulo_y_en_snake_case",
        "EsquemaDelModuloTests.Las_fechas_de_negocio_son_date_y_no_timestamptz",
        "EsquemaDelModuloTests.Lo_que_no_tiene_un_limite_de_negocio_es_text_y_no_un_varchar_inventado",
        "EsquemaDelModuloTests.Lo_que_toda_fila_tiene_que_llevar_es_NOT_NULL_y_sin_DEFAULT",
        "EsquemaDelModuloTests.Los_enumerados_se_guardan_como_texto_y_no_como_numero",
        "EsquemaDelModuloTests.Los_instantes_llevan_zona_horaria_porque_son_momentos",
        "EsquemaDelModuloTests.Los_topes_de_la_direccion_son_los_del_rulebook_de_SEPA",
        "EsquemaDelModuloTests.No_hay_ninguna_clave_foranea_que_salga_del_esquema_del_modulo",
        "EsquemaDelModuloTests.Toda_entidad_transaccional_lleva_su_empresa_desde_la_primera_tabla",

        "LaCargaDeSemillasTests.Cargar_dos_veces_no_duplica_nada",
        "LaCargaDeSemillasTests.El_porcentaje_se_guarda_con_los_decimales_que_tiene_la_columna",
        "LaCargaDeSemillasTests.La_carga_declara_por_que_no_tiene_empresa",
        "LaCargaDeSemillasTests.La_carga_deja_dentro_todo_lo_que_trae_el_fichero",
        "LaCargaDeSemillasTests.Los_tramos_del_IVA_general_entran_los_tres_y_solo_uno_queda_abierto",

        "LaTraduccionASqlTests.El_barrido_ve_los_listados_y_reconoce_una_consulta_intraducible",
        "LaTraduccionASqlTests.El_listado_de_lo_bloqueado_se_traduce_entero",
        "LaTraduccionASqlTests.La_busqueda_por_criterio_y_su_cursor_se_traducen_a_sql",
        "LaTraduccionASqlTests.Todo_orden_y_todo_filtro_declarado_se_traduce_a_sql",

        "LosPuertosDeLecturaTests.El_impuesto_que_no_esta_no_existe",
        "LosPuertosDeLecturaTests.El_impuesto_que_rige_en_la_fecha_se_ofrece_para_lo_nuevo",
        "LosPuertosDeLecturaTests.El_tramo_cerrado_sigue_resolviendo_lo_viejo_pero_no_se_ofrece",
        "LosPuertosDeLecturaTests.El_tramo_que_todavia_no_ha_entrado_tampoco_se_ofrece",
        "LosPuertosDeLecturaTests.La_divisa_dada_de_alta_se_ofrece_y_la_que_no_esta_no_existe",
        "LosPuertosDeLecturaTests.La_unidad_dada_de_alta_se_ofrece_y_la_que_no_esta_no_existe",
        "LosPuertosDeLecturaTests.Los_tres_puertos_contestan_sin_empresa_activa_y_sin_ver_lo_bloqueado",

        "MaestrosDelSeptimoApartadoTests.Dos_impuestos_distintos_conviven_en_las_mismas_fechas",
        "MaestrosDelSeptimoApartadoTests.Dos_tramos_del_mismo_impuesto_no_pueden_pisarse",
        "MaestrosDelSeptimoApartadoTests.Dos_tramos_seguidos_del_mismo_impuesto_entran_sin_problema",
        "MaestrosDelSeptimoApartadoTests.El_dinero_y_lo_que_lo_multiplica_son_numeric_y_nunca_flotantes",
        "MaestrosDelSeptimoApartadoTests.El_solape_de_un_solo_dia_tambien_se_rechaza",
        "MaestrosDelSeptimoApartadoTests.La_divisa_NO_guarda_sus_decimales_y_la_unidad_de_medida_SI",
        "MaestrosDelSeptimoApartadoTests.La_restriccion_que_impide_el_solape_existe_y_es_de_exclusion",
        "MaestrosDelSeptimoApartadoTests.Las_fechas_de_los_maestros_son_de_calendario",
        "MaestrosDelSeptimoApartadoTests.Los_seis_maestros_estan_en_el_esquema_del_modulo",
    ];

    [Fact]
    public void Los_casos_de_este_carril_son_los_declarados() =>
        CensoDeReglas.Comprobar(typeof(ElCensoDeEsteCarrilTests).Assembly, s_declarados);
}
