using Bastion.Pruebas.Comun;

namespace Bastion.Api.IntegrationTests;

/// <summary>
/// El censo del carril de integración de la API: sus casos, nombrados uno a uno.
/// </summary>
/// <remarks>
/// <para>
/// Este carril comprueba lo que pasa de verdad contra PostgreSQL: el filtro de empresa, la traza, la bandeja, la
/// idempotencia y las puertas de cada acción. Casi todos sus casos son <b>reglas</b>: afirmaciones sobre un
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
        "ContratoDeCatalogoTests.Colgar_una_categoria_por_debajo_del_nivel_maximo_es_409_y_lo_dice",
        "ContratoDeCatalogoTests.Crear_un_articulo_devuelve_201_con_Location_que_lleva_al_recurso",
        "ContratoDeCatalogoTests.El_codigo_del_articulo_se_normaliza_y_el_duplicado_en_minusculas_es_409",
        "ContratoDeCatalogoTests.El_listado_de_articulos_viene_paginado_con_su_total_y_filtra_por_categoria",
        "ContratoDeCatalogoTests.El_tipo_del_articulo_viaja_como_TEXTO_y_no_como_numero",
        "ContratoDeCatalogoTests.Modificar_devuelve_el_recurso_entero_y_no_hay_por_donde_tocar_el_codigo_ni_la_unidad",
        "ContratoDeCatalogoTests.Modificar_sin_tocar_el_impuesto_no_lo_vuelve_a_preguntar_pero_cambiarlo_a_uno_caducado_SI_se_rechaza",
        "ContratoDeCatalogoTests.Mover_una_categoria_debajo_de_su_propia_descendencia_es_409_con_type_categoria_ciclo",
        "ContratoDeCatalogoTests.Un_articulo_que_no_existe_es_404_con_ProblemDetails",
        "ContratoDeCatalogoTests.Un_tramo_de_impuesto_que_ya_no_rige_no_se_propone_para_un_articulo_nuevo",
        "ContratoDeCatalogoTests.Una_categoria_no_puede_colgar_de_si_misma_ni_al_crearla_ni_al_moverla",
        "ContratoDeCatalogoTests.Una_categoria_padre_que_no_existe_es_400_y_no_un_409_de_ciclo",
        "ContratoDeCatalogoTests.Una_unidad_RETIRADA_no_vale_para_un_alta_y_el_articulo_que_ya_la_usa_sigue_resolviendola",
        "ContratoDeCatalogoTests.Una_unidad_que_no_existe_es_400_y_su_type_es_DISTINGUIBLE_del_de_la_retirada",

        "ContratoDeLoQueCuelgaTests.Ascender_una_cuenta_a_preferente_baja_a_la_que_lo_era",
        "ContratoDeLoQueCuelgaTests.Colgar_algo_sin_citar_la_version_de_la_ficha_es_428",
        "ContratoDeLoQueCuelgaTests.Colgar_un_contacto_MUEVE_la_version_de_la_ficha_y_la_vieja_ya_no_vale",
        "ContratoDeLoQueCuelgaTests.Descolgar_una_cuenta_la_quita_a_ella_y_deja_en_pie_a_las_demas",
        "ContratoDeLoQueCuelgaTests.El_mismo_IBAN_dos_veces_en_la_misma_ficha_es_409_y_sin_el_numero_dentro",
        "ContratoDeLoQueCuelgaTests.Noventa_dias_de_plazo_son_400_y_el_mensaje_lleva_el_tope_y_la_norma",
        "ContratoDeLoQueCuelgaTests.Sesenta_dias_clavados_se_aceptan_porque_el_tope_es_el_maximo_y_no_un_veto",
        "ContratoDeLoQueCuelgaTests.Un_IBAN_con_el_control_mal_es_400_y_no_dice_CUAL_de_las_tres_condiciones_fallo",
        "ContratoDeLoQueCuelgaTests.Un_choque_de_verdad_deja_en_la_traza_QUE_choco_y_en_que_estado",
        "ContratoDeLoQueCuelgaTests.Un_contacto_se_cuelga_y_se_descuelga_y_la_lista_queda_vacia",
        "ContratoDeLoQueCuelgaTests.Un_limite_con_su_divisa_va_y_vuelve_y_se_lee_igual_en_el_GET",
        "ContratoDeLoQueCuelgaTests.Un_limite_sin_divisa_es_400_y_despues_NO_hay_limite_heredado_de_la_empresa",
        "ContratoDeLoQueCuelgaTests.Una_divisa_que_el_catalogo_no_conoce_sale_por_su_campo_y_no_por_un_500",
        "ContratoDeLoQueCuelgaTests.Una_divisa_sin_importe_es_400_porque_retirar_el_limite_es_vaciar_LOS_DOS",
        "ContratoDeLoQueCuelgaTests.Una_segunda_cuenta_preferente_baja_a_la_primera_y_solo_queda_UNA",

        "ContratoDeLosCrucesTests.El_listado_esconde_al_proveedor_bloqueado_y_desbloquearlo_devuelve_la_MISMA_fila",
        "ContratoDeLosCrucesTests.Inventado_ajeno_bloqueado_y_solo_cliente_contestan_el_MISMO_400_y_no_dejan_fila",
        "ContratoDeLosCrucesTests.Un_limite_en_dolares_y_una_tarifa_en_euros_conviven_porque_nadie_compara_divisas",
        "ContratoDeLosCrucesTests.Un_proveedor_de_aqui_se_anade_con_201_y_el_listado_lo_trae",
        "ContratoDeLosCrucesTests.Una_tarifa_inventada_y_una_de_otra_empresa_son_400_y_la_ajena_vale_en_la_suya",
        "ContratoDeLosCrucesTests.Una_tarifa_que_rige_hoy_se_asigna_se_lee_y_con_nulo_se_quita",
        "ContratoDeLosCrucesTests.Una_tarifa_que_ya_no_rige_y_una_que_aun_no_son_409_y_la_ficha_sigue_con_la_suya",
        "ContratoDeLosCrucesTests.Volver_a_anadir_a_un_bloqueado_que_ya_suministraba_es_el_MISMO_400_y_no_un_409",

        "ContratoDeOrganizacionTests.Borrar_una_empresa_la_bloquea_pero_no_la_borra",
        "ContratoDeOrganizacionTests.Con_la_empresa_activa_bloqueada_no_se_puede_crear_nada_y_es_409",
        "ContratoDeOrganizacionTests.Crear_una_empresa_devuelve_201_con_Location_que_lleva_al_recurso",
        "ContratoDeOrganizacionTests.Dos_empresas_con_el_mismo_NIF_es_409_y_no_una_excepcion_de_PostgreSQL",
        "ContratoDeOrganizacionTests.El_codigo_de_almacen_se_normaliza_y_el_duplicado_en_minusculas_es_409",
        "ContratoDeOrganizacionTests.El_ejercicio_se_cuelga_de_la_empresa_del_token_y_no_de_la_del_cuerpo",
        "ContratoDeOrganizacionTests.El_listado_viene_paginado_y_con_su_total",
        "ContratoDeOrganizacionTests.La_direccion_va_y_vuelve_en_los_seis_campos_de_R17",
        "ContratoDeOrganizacionTests.Las_fechas_de_un_ejercicio_van_y_vuelven_como_fechas_de_calendario",
        "ContratoDeOrganizacionTests.Lo_que_falta_en_el_cuerpo_lo_rechaza_el_enlace_de_modelo_con_400_por_campo",
        "ContratoDeOrganizacionTests.Los_enumerados_viajan_como_texto_y_no_como_numero",
        "ContratoDeOrganizacionTests.Pedir_una_pagina_gigante_no_se_lleva_la_tabla",
        "ContratoDeOrganizacionTests.Suprimir_una_serie_que_no_ha_numerado_es_204",
        "ContratoDeOrganizacionTests.Suprimir_una_serie_que_ya_ha_numerado_es_409",
        "ContratoDeOrganizacionTests.Un_NIF_con_letra_de_control_incorrecta_es_400_del_campo_nif",
        "ContratoDeOrganizacionTests.Un_almacen_fisico_sin_direccion_es_400_del_campo_direccion",
        "ContratoDeOrganizacionTests.Un_almacen_virtual_sin_direccion_se_acepta",
        "ContratoDeOrganizacionTests.Un_ejercicio_de_mas_de_doce_meses_es_400_del_campo_fechaDeFin",
        "ContratoDeOrganizacionTests.Un_ejercicio_se_cierra_y_se_reabre_por_sus_puertas",
        "ContratoDeOrganizacionTests.Un_tipo_de_documento_inventado_dice_cuales_se_admiten",
        "ContratoDeOrganizacionTests.Una_empresa_bloqueada_no_se_puede_modificar_y_da_404",
        "ContratoDeOrganizacionTests.Una_empresa_bloqueada_se_desbloquea_por_su_puerta_y_vuelve_a_estar_activa",
        "ContratoDeOrganizacionTests.Una_empresa_que_no_existe_es_404_con_ProblemDetails",
        "ContratoDeOrganizacionTests.Una_serie_colgada_del_ejercicio_de_otra_empresa_es_400_del_campo_ejercicioId",
        "ContratoDeOrganizacionTests.Varios_campos_malos_se_devuelven_todos_de_una_vez",

        "ContratoDeTarifasTests.Dos_tramos_del_mismo_codigo_que_se_pisan_los_rechaza_la_BASE",
        "ContratoDeTarifasTests.El_dia_en_que_un_tramo_ACABA_todavia_cuenta_para_el_solape",
        "ContratoDeTarifasTests.Gana_el_antepasado_MAS_CERCANO_y_la_mas_honda_de_otra_rama_no_compite",
        "ContratoDeTarifasTests.La_extension_btree_gist_la_puso_la_MIGRACION_en_la_imagen_del_compose",
        "ContratoDeTarifasTests.La_linea_del_articulo_gana_y_el_tramo_se_elige_DESPUES_con_la_frontera_arriba",
        "ContratoDeTarifasTests.Sin_linea_aplicable_hay_error_con_nombre_y_NUNCA_un_precio_cero",
        "ContratoDeTarifasTests.Una_tarifa_que_no_existe_y_una_que_no_cubre_el_dia_son_DOS_type_distintos",

        "ContratoDeTercerosTests.Crear_un_tercero_devuelve_201_con_Location_que_lleva_al_recurso",
        "ContratoDeTercerosTests.El_cursor_del_tramo_anterior_trae_el_siguiente_y_no_repite",
        "ContratoDeTercerosTests.El_domicilio_fiscal_va_y_vuelve_en_los_seis_campos_de_R17",
        "ContratoDeTercerosTests.El_estado_de_verificacion_viaja_como_TEXTO_y_no_como_numero",
        "ContratoDeTercerosTests.El_identificador_espanol_se_valida_de_verdad_y_nace_verificado",
        "ContratoDeTercerosTests.El_identificador_extranjero_nace_marcado_como_NO_verificado",
        "ContratoDeTercerosTests.El_listado_viene_paginado_con_su_total_y_filtra_por_nombre",
        "ContratoDeTercerosTests.La_busqueda_por_identificador_va_por_el_CUERPO_y_lo_lee_igual_que_el_alta",
        "ContratoDeTercerosTests.Modificar_exige_la_version_y_devuelve_el_recurso_entero_sin_tocar_el_identificador",
        "ContratoDeTercerosTests.Un_cursor_compuesto_a_mano_es_400_y_no_un_tramo_vacio",
        "ContratoDeTercerosTests.Un_identificador_espanol_con_el_control_mal_es_400_del_campo_del_formulario",
        "ContratoDeTercerosTests.Un_tercero_bloqueado_no_aparece_en_la_busqueda_por_su_identificador",
        "ContratoDeTercerosTests.Un_tercero_que_no_es_ni_cliente_ni_proveedor_es_400_y_dice_que_marque_uno",
        "ContratoDeTercerosTests.Un_tercero_que_no_existe_es_404_con_ProblemDetails",
        "ContratoDeTercerosTests.Una_busqueda_sin_ningun_criterio_es_400_y_dice_donde_esta_el_listado",

        "ElAccesoReservadoDelArticulo32Tests.El_listado_de_lo_bloqueado_no_devuelve_ninguna_llave_de_concurrencia",
        "ElAccesoReservadoDelArticulo32Tests.Lo_bloqueado_de_otra_empresa_no_asoma_por_este_camino",
        "ElAccesoReservadoDelArticulo32Tests.Un_almacen_bloqueado_desaparece_de_los_caminos_ordinarios_y_aparece_en_este",
        "ElAccesoReservadoDelArticulo32Tests.Una_supresion_del_articulo_32_si_vence_y_la_fecha_sale_en_el_listado",

        "ElAltaDeUnaEmpresaSePublicaTests.Dar_de_alta_una_empresa_deja_su_evento_en_la_cola_y_el_host_lo_publica",
        "ElAltaDeUnaEmpresaSePublicaTests.El_alta_que_hace_la_semilla_se_publica_igual_y_dice_por_que_no_tiene_empresa",

        "ElCensoDeEsteCarrilTests.Los_casos_de_este_carril_son_los_declarados",

        // Del ítem 2.4: el cerrojo de la numeración, que solo el motor puede demostrar. El
        // recorrido por tipo de documento cuenta como UN caso aquí -un `[Theory]` es un nombre,
        // por muchas filas que traiga- y su afirmación de no-vacío va al lado.
        "ElCerrojoDeLaNumeracionTests.Borrar_una_serie_a_mano_sin_su_contador_sigue_siendo_imposible",
        "ElCerrojoDeLaNumeracionTests.Deshacer_la_transaccion_devuelve_el_numero_y_el_siguiente_lo_reutiliza",
        "ElCerrojoDeLaNumeracionTests.Dos_numeraciones_simultaneas_se_llevan_numeros_distintos_y_consecutivos",
        "ElCerrojoDeLaNumeracionTests.El_recorrido_de_arriba_no_se_deja_ningun_tipo_de_documento",
        "ElCerrojoDeLaNumeracionTests.Suprimir_pierde_contra_una_numeracion_que_se_cuela_entre_la_lectura_y_el_borrado",
        "ElCerrojoDeLaNumeracionTests.Una_serie_cerrada_no_numera",
        "ElCerrojoDeLaNumeracionTests.Una_serie_de_otra_empresa_no_numera",
        "ElCerrojoDeLaNumeracionTests.Una_serie_numera_sin_huecos_sea_cual_sea_el_documento_que_numera",
        "ElCerrojoDeLaNumeracionTests.Una_serie_que_no_existe_da_el_MISMO_error_que_una_ajena",

        // Del ítem 1.6, y son el SEGUNDO caso de este ensamblado que corre también en el carril
        // rápido: traducen consultas a SQL, y traducir no abre conexión. Están aquí porque desde el
        // 1.6 el listado del art. 32 lo componen tres módulos, y este es el único proyecto de
        // pruebas que ve las tres infraestructuras; en `Organizacion.IntegrationTests`, donde vivía
        // el barrido, «entero» habría pasado a significar un tercio.
        "ElListadoDelArticulo32SeTraduceEnteroTests.La_comprobacion_puede_dispararse",
        "ElListadoDelArticulo32SeTraduceEnteroTests.Las_proyecciones_declaradas_son_las_que_implementan_el_puerto",
        "ElListadoDelArticulo32SeTraduceEnteroTests.Todo_orden_del_listado_se_traduce_en_los_tres_modulos",

        "ElConflictoQueNoRevelaTests.Bloquear_un_tercero_no_libera_su_identificador_y_por_eso_desbloquear_no_choca",
        "ElConflictoQueNoRevelaTests.El_alta_contra_uno_bloqueado_y_contra_uno_activo_contestan_lo_MISMO",
        "ElConflictoQueNoRevelaTests.El_mismo_identificador_en_otra_empresa_se_da_de_alta_sin_conflicto",
        "ElConflictoQueNoRevelaTests.La_traza_SI_dice_cual_de_los_dos_era_y_no_lleva_el_identificador_dentro",

        "ElEventoVaEnLaMismaTransaccionTests.Guardar_dos_veces_el_mismo_agregado_no_encola_el_hecho_dos_veces",
        "ElEventoVaEnLaMismaTransaccionTests.La_empresa_y_su_evento_los_escribe_LA_MISMA_transaccion",
        "ElEventoVaEnLaMismaTransaccionTests.Un_guardado_que_revienta_no_deja_ni_la_empresa_ni_su_evento",
        "ElEventoVaEnLaMismaTransaccionTests.Y_uno_que_va_bien_deja_el_evento_entero_y_pendiente",

        "ElFiltroDeEmpresaTests.El_identificador_de_empresa_que_venga_en_la_peticion_se_ignora",
        "ElFiltroDeEmpresaTests.El_padron_de_empresas_no_se_lee_desde_otra_empresa",
        "ElFiltroDeEmpresaTests.El_total_de_la_pagina_tampoco_cuenta_las_filas_de_otra_empresa",
        "ElFiltroDeEmpresaTests.Un_borrado_por_identificador_contra_una_fila_de_otra_empresa_es_404",
        "ElFiltroDeEmpresaTests.Un_listado_sin_filtro_explicito_no_devuelve_datos_de_otra_empresa",
        "ElFiltroDeEmpresaTests.Un_usuario_que_no_comparte_empresa_no_se_ve",
        "ElFiltroDeEmpresaTests.Una_escritura_por_identificador_contra_una_fila_de_otra_empresa_es_404",
        "ElFiltroDeEmpresaTests.Una_fila_de_otra_empresa_no_se_distingue_de_una_que_no_existe",

        // Del ítem 2.3, el libro de movimientos. Son los únicos casos del carril que le
        // hablan a una tabla particionada por su nombre, y los únicos que escriben con el
        // dominio de un módulo sin borde: Inventario no tiene endpoints hasta el 2.4.
        "ElLibroEstaParticionadoTests.El_conjunto_de_particiones_es_el_mes_en_curso_los_doce_siguientes_y_la_de_por_defecto",
        "ElLibroEstaParticionadoTests.La_clave_primaria_del_libro_incluye_la_clave_de_particion",
        "ElLibroEstaParticionadoTests.La_tabla_del_libro_esta_particionada_por_RANGO_sobre_la_fecha_de_operacion",
        "ElLibroEstaParticionadoTests.Las_filas_de_un_ajuste_caen_en_la_particion_de_su_mes_y_ninguna_en_la_de_por_defecto",

        // Los SEIS caminos del solo-añadido, y son seis porque la tabla está particionada:
        // UPDATE y DELETE por el padre, los dos iguales entrando por la partición, y los dos
        // TRUNCATE. Borrar cualquiera de las seis líneas deja una puerta abierta en el motor
        // sin que nada más lo note.
        "ElLibroNoSePuedeLimpiarTests.Borrar_una_fila_entrando_por_la_particion_lo_rechaza_el_motor",
        "ElLibroNoSePuedeLimpiarTests.Borrar_una_fila_por_la_tabla_padre_lo_rechaza_el_motor",
        "ElLibroNoSePuedeLimpiarTests.Modificar_una_fila_entrando_por_la_particion_lo_rechaza_el_motor",
        "ElLibroNoSePuedeLimpiarTests.Modificar_una_fila_por_la_tabla_padre_lo_rechaza_el_motor",
        "ElLibroNoSePuedeLimpiarTests.Vaciar_LA_PARTICION_lo_rechaza_el_motor_y_este_es_el_que_se_vio_abierto",
        "ElLibroNoSePuedeLimpiarTests.Vaciar_la_tabla_padre_lo_rechaza_el_motor",

        // Del ítem 2.4: el número dentro del recibo, y el 428 de la única acción de toda la API
        // que EXIGE la clave. Los dos miran la misma frontera desde los dos lados.
        "ElNumeroEntraEnElReciboTests.El_reintento_con_la_misma_clave_devuelve_el_numero_y_no_gasta_otro",
        "ElNumeroEntraEnElReciboTests.Sin_la_cabecera_la_confirmacion_es_428_y_no_toca_nada",

        // Del ítem 2.2: las tres casillas de `AptitudParaMoverExistencias`, que son tres y no
        // cuatro. Si algún día el artículo recibe su final de vida, la casilla que entre
        // necesita su línea aquí y su caso allá.
        "ElPuertoDelArticuloContraLaBaseTests.El_bien_se_ofrece_y_el_servicio_no_se_almacena",
        "ElPuertoDelArticuloContraLaBaseTests.Uno_inventado_y_uno_de_otra_empresa_NoExisten_y_el_ajeno_si_en_la_suya",

        "ElPuertoDeTercerosContraLaBaseTests.De_un_conjunto_se_tratan_los_de_aqui_no_bloqueados_hagan_el_papel_que_hagan",
        "ElPuertoDeTercerosContraLaBaseTests.El_papel_por_el_que_se_pregunta_decide_entre_Disponible_y_NoHaceEseRol",
        "ElPuertoDeTercerosContraLaBaseTests.Un_bloqueado_NoExiste_desde_fuera_y_desbloquearlo_lo_devuelve_Disponible",
        "ElPuertoDeTercerosContraLaBaseTests.Uno_inventado_y_uno_de_otra_empresa_NoExisten_y_el_ajeno_si_en_la_suya",

        // Del ítem 1.11 (ADR-0034 §4): el plazo del recibo, a cada lado del borde, y quién lo cumple.
        "ElReciboCaducaYSeBorraTests.Con_base_de_datos_el_host_arranca_el_trabajo_que_purga_cada_hora",
        "ElReciboCaducaYSeBorraTests.El_recibo_nace_con_su_caducidad_a_las_24_horas_de_reclamarse",
        "ElReciboCaducaYSeBorraTests.La_purga_deja_lo_que_no_ha_vencido_y_se_lleva_lo_vencido_de_todas_las_empresas",
        "ElReciboCaducaYSeBorraTests.Purgado_el_recibo_el_mismo_reintento_vuelve_a_hacer_el_trabajo",

        "ElSelectorDeEmpresaTests.Cambiar_a_una_empresa_bloqueada_se_rechaza_como_si_no_se_perteneciera",
        "ElSelectorDeEmpresaTests.El_selector_trae_los_nombres_aunque_no_se_tenga_permiso_para_ver_empresas",
        "ElSelectorDeEmpresaTests.La_sesion_no_se_abre_en_una_empresa_bloqueada_aunque_sea_la_primera_pertenencia",
        "ElSelectorDeEmpresaTests.Una_empresa_bloqueada_se_cae_del_selector_y_su_pertenencia_sigue_en_la_tabla",

        "ElTrabajoDeFondoVaciaLaColaTests.El_fallo_de_uno_no_es_el_fallo_de_la_vuelta",
        "ElTrabajoDeFondoVaciaLaColaTests.Lo_que_esta_pendiente_acaba_publicado",
        "ElTrabajoDeFondoVaciaLaColaTests.Un_manejador_que_falla_la_primera_vez_acaba_recibiendo_el_evento",
        "ElTrabajoDeFondoVaciaLaColaTests.Un_manejador_que_no_funciona_nunca_acaba_aparcando_su_evento",

        "EntradaHostilTests.Un_NIF_hostil_es_un_400_normal_y_corriente",
        "EntradaHostilTests.Un_correo_hostil_en_el_inicio_de_sesion_no_cuenta_nada_de_dentro",
        "EntradaHostilTests.Un_cuerpo_que_no_es_el_que_toca_es_400_y_no_dice_por_donde_ha_roto",
        "EntradaHostilTests.Un_identificador_que_no_es_un_GUID_ni_siquiera_llega_a_la_accion",
        "EntradaHostilTests.Un_tipo_de_contenido_que_no_es_JSON_es_415_y_tampoco_cuenta_nada",
        "EntradaHostilTests.Una_cadena_larguisima_no_tumba_nada_y_sale_por_el_400_de_su_campo",
        "EntradaHostilTests.Una_paginacion_imposible_es_400_y_no_una_excepcion",

        "EsquemaDeIdentidadTests.Cada_modulo_tiene_SU_historial_de_migraciones_en_SU_esquema",
        "EsquemaDeIdentidadTests.El_correo_es_unico_porque_es_con_lo_que_se_entra",
        "EsquemaDeIdentidadTests.El_refresco_se_guarda_como_resumen_y_con_su_indice_unico",
        "EsquemaDeIdentidadTests.El_usuario_se_bloquea_y_no_se_borra_asi_que_tiene_donde_apuntarlo",
        "EsquemaDeIdentidadTests.En_public_no_queda_ni_una_tabla_de_ningun_modulo",
        "EsquemaDeIdentidadTests.La_membresia_guarda_el_identificador_de_empresa_y_NO_una_clave_ajena",
        "EsquemaDeIdentidadTests.Las_tablas_del_modulo_estan_en_su_esquema_y_en_snake_case",
        "EsquemaDeIdentidadTests.Los_instantes_llevan_zona_horaria",

        "EsquemaDeTercerosTests.El_bloqueo_y_las_marcas_son_las_MISMAS_columnas_que_en_los_demas_modulos",
        "EsquemaDeTercerosTests.El_iban_no_se_repite_dentro_de_la_misma_ficha",
        "EsquemaDeTercerosTests.El_identificador_fiscal_son_TRES_columnas_y_no_una_cadena_suelta",
        "EsquemaDeTercerosTests.El_limite_de_credito_admite_nulo_porque_no_tenerlo_no_es_tenerlo_a_cero",
        "EsquemaDeTercerosTests.El_limite_de_credito_es_numeric_y_lleva_su_divisa",
        "EsquemaDeTercerosTests.El_regimen_fiscal_son_CUATRO_columnas_de_la_ficha",
        "EsquemaDeTercerosTests.El_tope_del_plazo_de_pago_es_sesenta_dias_naturales",
        "EsquemaDeTercerosTests.La_cuenta_preferente_es_UNICA_por_tercero_y_su_indice_SI_es_parcial",
        "EsquemaDeTercerosTests.La_empresa_se_guarda_como_identificador_y_NO_como_clave_ajena",
        "EsquemaDeTercerosTests.La_escala_del_limite_de_credito_es_la_de_R6",
        "EsquemaDeTercerosTests.La_tabla_del_modulo_esta_en_SU_esquema_y_en_snake_case",
        "EsquemaDeTercerosTests.La_unicidad_del_identificador_esta_EN_LA_BASE_y_abarca_tambien_lo_bloqueado",
        "EsquemaDeTercerosTests.Las_reglas_que_no_se_pueden_esquivar_estan_EN_LA_BASE",
        "EsquemaDeTercerosTests.Lo_que_cuelga_se_borra_CON_la_ficha_y_no_se_queda_huerfano",
        "EsquemaDeTercerosTests.Lo_que_toda_fila_tiene_que_llevar_es_NOT_NULL_y_sin_DEFAULT",

        // Del ítem 2.3: la regla que ata las tres cantidades de una fila del libro, y los dos
        // CHECK que la sostienen en el motor para los caminos que todavía no existen.
        "LaCantidadBaseEsLaIntroducidaPorElFactorTests.Cada_fila_de_un_ajuste_cumple_la_regla_del_factor_medida_en_la_base",
        "LaCantidadBaseEsLaIntroducidaPorElFactorTests.Una_cantidad_base_que_no_es_la_introducida_por_el_factor_la_rechaza_el_motor",
        "LaCantidadBaseEsLaIntroducidaPorElFactorTests.Una_fila_que_no_mueve_nada_la_rechaza_el_motor",

        // Del ítem 2.3: la R13 en los DOS sentidos, cada uno con su barrido y con su arnés.
        // Ninguna clave ajena puede expresar esta flecha —el origen es un par «tipo +
        // identificador»—, así que borrar una de estas dos líneas deja la mitad que quitara
        // sin nadie que la vigile.
        "LaDobleFlechaDelLibroTests.Ningun_ajuste_confirmado_se_queda_sin_una_sola_fila_del_libro",
        "LaDobleFlechaDelLibroTests.Ninguna_fila_del_libro_apunta_a_un_documento_que_no_existe",

        "LaEdadDelMasViejoSeMideTests.El_publicador_publica_la_edad_del_pendiente_mas_viejo",
        "LaEdadDelMasViejoSeMideTests.Y_con_la_cola_vacia_la_edad_vuelve_a_cero",

        "LaFilaBloqueadaSigueEnLaBaseTests.Desbloquear_por_su_puerta_devuelve_la_MISMA_fila_y_no_una_copia",
        "LaFilaBloqueadaSigueEnLaBaseTests.Suprimir_por_la_API_deja_la_fila_entera_con_su_motivo_y_su_fecha",

        // Del ítem 1.11 (ADR-0034 §5): los ficheros que no son de Excel, con los cuatro criterios del
        // sondeo —contesta, no cuenta nada de dentro, no devuelve lo recibido y sigue atendiendo—.
        "LaImportacionAguantaFicherosHostilesTests.Ningun_fichero_hostil_tumba_la_importacion_ni_cuenta_nada_ni_devuelve_lo_que_le_mandaron",

        // Del ítem 1.11 (ADR-0034 §1, §2 y §5): los dos ficheros que escribió Excel, la fila que decide, el
        // informe con su línea y los tres permisos.
        "LaImportacionDeTercerosTests.Con_el_permiso_de_importar_y_sin_el_de_dar_de_alta_se_rechaza_el_fichero_entero",
        "LaImportacionDeTercerosTests.Las_filas_malas_no_impiden_que_entren_las_buenas_y_el_informe_dice_su_linea_de_Excel",
        "LaImportacionDeTercerosTests.Lo_que_ya_existe_activo_o_bloqueado_sale_en_el_mismo_grupo_y_la_traza_no_dice_cuales",
        "LaImportacionDeTercerosTests.Los_dos_CSV_que_escribe_Excel_en_espanol_entran_enteros_y_con_cada_valor_en_su_sitio",
        "LaImportacionDeTercerosTests.Los_importes_se_leen_con_la_coma_decimal_y_se_ve_en_el_limite_guardado",
        "LaImportacionDeTercerosTests.Sin_el_permiso_de_importar_no_se_entra_aunque_se_puedan_dar_altas_de_una_en_una",
        "LaImportacionDeTercerosTests.Sin_el_permiso_del_limite_un_fichero_con_limite_se_rechaza_entero_y_sin_limite_entra",
        "LaImportacionDeTercerosTests.Una_fila_con_el_limite_mal_escrito_no_deja_el_tercero_dado_de_alta_sin_limite",

        // Del ítem 1.11 (ADR-0034 §1, §3 y §4): la importación se repite, se cae y se rechaza ENTERA.
        "LaImportacionEsUnaOperacionTests.El_peor_informe_posible_tiene_techo_y_es_el_que_se_guarda_en_el_recibo",
        "LaImportacionEsUnaOperacionTests.La_misma_clave_con_el_mismo_fichero_devuelve_el_mismo_informe_sin_volver_a_mirar_la_base",
        "LaImportacionEsUnaOperacionTests.La_misma_clave_con_otro_fichero_es_un_409_y_el_otro_fichero_no_entra",
        "LaImportacionEsUnaOperacionTests.Si_la_escritura_revienta_a_mitad_no_queda_ni_una_fila_ni_el_recibo_y_el_reintento_es_la_primera_vez",
        "LaImportacionEsUnaOperacionTests.Sin_Content_Length_el_servidor_deja_de_leer_en_cuanto_el_fichero_no_cabe",
        "LaImportacionEsUnaOperacionTests.Un_cuerpo_que_no_es_un_CSV_es_415_antes_de_leer_nada",
        "LaImportacionEsUnaOperacionTests.Un_fichero_con_una_fila_mas_que_el_tope_es_413_sin_importar_ninguna",
        "LaImportacionEsUnaOperacionTests.Un_fichero_que_declara_mas_que_el_tope_es_413_y_no_deja_nada",

        // Del ítem 1.7. Las dos mitades del ADR-0023 que solo se ven contra PostgreSQL: la
        // desigualdad de la inversa la comprueba la capa de aplicación leyendo la fila contraria
        // —una lectura, no una rama— y el resolutor falla o no según lo que haya declarado en la
        // tabla.
        "LaInversaYElResolutorTests.El_par_que_habria_que_encadenar_no_se_resuelve",
        "LaInversaYElResolutorTests.El_par_que_se_separa_exactamente_el_margen_entra_y_el_de_al_lado_no",
        "LaInversaYElResolutorTests.El_redondeo_de_la_inversa_entra_solo_si_la_escala_lo_explica",
        "LaInversaYElResolutorTests.El_sentido_contrario_no_se_deduce_invirtiendo",
        "LaInversaYElResolutorTests.La_inversa_que_no_lo_es_se_rechaza_en_el_alta",
        "LaInversaYElResolutorTests.La_modificacion_vuelve_a_comprobar_la_inversa",
        "LaInversaYElResolutorTests.Una_conversion_retirada_sigue_resolviendo_y_lo_dice",
        "LaInversaYElResolutorTests.Una_conversion_retirada_sigue_restringiendo_a_su_inversa",

        // Del ítem 1.11, y corren en el carril rápido: son la regla sobre la lista de rastros que
        // usan los sondeos de este carril, y no abren conexión.
        "LaListaDeRastrosProhibidosTests.La_regla_de_la_lista_puede_dispararse",
        "LaListaDeRastrosProhibidosTests.Ningun_rastro_prohibido_cabe_en_un_identificador_aleatorio",

        "LaMatrizDeLosPuertosDeEstadoTests.Cada_casilla_de_puerto_por_estado_esta_cubierta",
        "LaMatrizDeLosPuertosDeEstadoTests.Cada_cubrimiento_nombra_un_puerto_de_aqui_y_un_valor_de_SU_enumerado",
        "LaMatrizDeLosPuertosDeEstadoTests.Cada_cubrimiento_vive_en_un_caso_que_corre_contra_la_base",
        "LaMatrizDeLosPuertosDeEstadoTests.Cada_familia_delegada_tiene_su_matriz_en_el_otro_carril",
        "LaMatrizDeLosPuertosDeEstadoTests.La_matriz_no_esta_vacia_y_ve_los_dos_sentidos_del_cruce",

        "LaMismaClaveDevuelveElMismoRecursoTests.De_dos_peticiones_simultaneas_con_la_misma_clave_solo_una_hace_el_trabajo",
        "LaMismaClaveDevuelveElMismoRecursoTests.El_recibo_y_el_almacen_llevan_el_mismo_xmin",
        "LaMismaClaveDevuelveElMismoRecursoTests.El_reintento_con_la_misma_clave_devuelve_los_mismos_bytes_y_no_crea_otro",
        "LaMismaClaveDevuelveElMismoRecursoTests.La_cabecera_en_una_ruta_que_no_la_admite_es_400",
        "LaMismaClaveDevuelveElMismoRecursoTests.La_misma_clave_con_otro_cuerpo_es_409",
        "LaMismaClaveDevuelveElMismoRecursoTests.La_misma_clave_desde_otra_empresa_hace_su_propio_trabajo",
        "LaMismaClaveDevuelveElMismoRecursoTests.Un_alta_rechazada_deja_la_clave_libre_para_el_reintento",
        "LaMismaClaveDevuelveElMismoRecursoTests.Una_clave_que_no_identifica_nada_es_400",

        // Del ítem 2.3: la partición por defecto NO es una red de la que colgarse. Este caso
        // vive entero dentro de una transacción que se deshace, y el motivo está escrito en
        // mayúsculas en su propio fichero: una fila confirmada ahí no se puede borrar y
        // envenena el carril entero y el segundo arranque.
        "LaParticionPorDefectoSeDenunciaTests.Una_fila_de_un_mes_sin_particion_cae_en_la_de_por_defecto_y_denuncia_la_averia",

        "LaPuertaDeCadaAccionTests.Con_su_permiso_y_solo_con_el_suyo_ninguna_accion_responde_401_ni_403",
        "LaPuertaDeCadaAccionTests.Con_un_permiso_que_no_es_el_suyo_toda_accion_protegida_responde_403",
        "LaPuertaDeCadaAccionTests.Las_acciones_sin_permiso_las_puede_usar_cualquiera_que_haya_entrado",
        "LaPuertaDeCadaAccionTests.Las_tres_acciones_anonimas_se_alcanzan_sin_credenciales",
        "LaPuertaDeCadaAccionTests.Ninguna_accion_contesta_con_un_fallo_del_servidor_al_sondeo",
        "LaPuertaDeCadaAccionTests.Sin_credenciales_toda_accion_protegida_responde_401",
        "LaPuertaDeCadaAccionTests.Una_ruta_que_no_existe_es_404_para_quien_si_se_ha_identificado",

        // Y la línea que separa la retirada del bloqueo, que la decide una consulta traducida a
        // SQL y no una rama en C#.
        "LaRetiradaNoEsUnBloqueoTests.El_GET_por_identificador_sigue_devolviendo_la_fila_retirada",
        "LaRetiradaNoEsUnBloqueoTests.La_API_no_atiende_el_borrado_de_una_divisa",
        "LaRetiradaNoEsUnBloqueoTests.La_coleccion_excluye_lo_retirado_por_omision_y_lo_trae_al_pedirlo",
        "LaRetiradaNoEsUnBloqueoTests.Reincorporar_la_vuelve_a_ofrecer_y_no_crea_una_fila_nueva",

        // Del ítem 2.4: las dos puertas que mira la serie de un ajuste, y el número que
        // aparece en el documento al confirmarlo.
        "LaSerieDelAjusteTests.Cerrar_la_serie_despues_del_borrador_lo_deja_sin_poder_confirmarse",
        "LaSerieDelAjusteTests.Confirmar_pone_el_numero_en_el_documento_y_lo_sube_en_la_serie",
        "LaSerieDelAjusteTests.Una_serie_cerrada_no_deja_abrir_el_borrador",
        "LaSerieDelAjusteTests.Una_serie_que_no_existe_no_deja_abrir_el_borrador",

        "LaTrazaEsDeSoloAnadidoTests.Un_DELETE_sobre_una_fila_de_traza_lo_rechaza_el_motor",
        "LaTrazaEsDeSoloAnadidoTests.Un_INSERT_sin_empresa_y_sin_motivo_lo_rechaza_la_tabla",
        "LaTrazaEsDeSoloAnadidoTests.Un_TRUNCATE_de_la_tabla_lo_rechaza_el_motor",
        "LaTrazaEsDeSoloAnadidoTests.Un_UPDATE_sobre_una_fila_de_traza_lo_rechaza_el_motor",
        "LaTrazaEsDeSoloAnadidoTests.Y_con_empresa_Y_motivo_a_la_vez_tambien",

        "LaTrazaNoGuardaSecretosTests.Cambiar_la_contrasena_no_deja_ni_el_resumen_viejo_ni_el_nuevo",
        "LaTrazaNoGuardaSecretosTests.Ningun_valor_de_ninguna_propiedad_secreta_esta_en_ninguna_traza",

        "LaTrazaVaEnLaMismaTransaccionTests.La_fila_y_su_traza_las_escribe_LA_MISMA_transaccion",
        "LaTrazaVaEnLaMismaTransaccionTests.Un_guardado_que_revienta_no_deja_ni_la_fila_ni_su_traza",
        "LaTrazaVaEnLaMismaTransaccionTests.Y_uno_que_va_bien_deja_las_dos_cosas",

        "LaVersionViajaDeLaLecturaALaEscrituraTests.De_dos_que_leyeron_lo_mismo_solo_guarda_el_primero",
        "LaVersionViajaDeLaLecturaALaEscrituraTests.La_etiqueta_que_emite_la_lectura_es_la_que_acepta_la_escritura",
        "LaVersionViajaDeLaLecturaALaEscrituraTests.La_version_cambia_cuando_el_recurso_cambia",
        "LaVersionViajaDeLaLecturaALaEscrituraTests.Sin_la_cabecera_es_428_y_no_toca_nada",
        "LaVersionViajaDeLaLecturaALaEscrituraTests.Tras_un_412_ni_traza_ni_evento",
        "LaVersionViajaDeLaLecturaALaEscrituraTests.Una_cabecera_que_no_es_una_version_concreta_es_400",
        "LaVersionViajaDeLaLecturaALaEscrituraTests.Una_version_obsoleta_es_412_y_trae_la_actual",

        "LasMarcasDeTiempoLasPoneElRelojInyectadoTests.El_alta_no_pasa_por_el_interceptor_y_por_eso_lleva_la_hora_del_dominio",
        "LasMarcasDeTiempoLasPoneElRelojInyectadoTests.La_hora_del_cambio_sale_del_reloj_inyectado_y_no_del_de_la_base",
        "LasMarcasDeTiempoLasPoneElRelojInyectadoTests.Un_cambio_por_la_API_mueve_una_marca_y_deja_la_otra_donde_estaba",

        // Del ítem 1.12: las migraciones una a una sobre tablas con filas, el contraste sobre tablas
        // vacías y la lista de contextos comparada con los que de verdad tienen migraciones.
        "LasMigracionesSobreTablasConFilasTests.Recorre_todos_los_contextos_que_tienen_migraciones",
        "LasMigracionesSobreTablasConFilasTests.Una_a_una_y_sobre_tablas_con_filas_ninguna_falla_ni_se_lleva_una_fila",
        "LasMigracionesSobreTablasConFilasTests.Y_sobre_tablas_vacias_se_aplican_igual_que_en_la_base_de_los_tests",

        // Del ítem 1.12, la mitad de dentro del arreglo: lo que dice y deja en la traza el caso de uso
        // que llama el migrador, y la puerta que la API cierra al rol del sistema (ADR-0035).
        "ElRolDelSistemaTests.Cambiarle_la_lista_al_rol_del_sistema_es_409_y_no_toca_nada",
        "ElRolDelSistemaTests.Recortado_y_con_un_permiso_retirado_el_despliegue_lo_deja_con_el_catalogo_y_lo_dice",
        "ElRolDelSistemaTests.Renombrarlo_con_la_misma_lista_en_otro_orden_vale",
        "ElRolDelSistemaTests.Un_rol_propio_sigue_cambiando_de_permisos",

        "LoQueCuelgaNaceComoAltaTests.El_arnes_ve_el_modelo_y_distingue_un_alta_de_una_modificacion",
        "LoQueCuelgaNaceComoAltaTests.Lo_que_se_cuelga_de_una_ficha_ya_guardada_sale_como_ALTA",
        "LosPermisosQueNombraElFrontalTests.Todo_permiso_que_el_frontal_teclea_lo_sirve_la_api",

        // Del ítem 1.11: la regla que la mutación 8 del 1.10 encontró activa sin que nadie la
        // hubiera escrito. Le pregunta a la base por TODA clave ajena entre esquemas.
        "NingunaClaveAjenaCruzaDeEsquemaEnLaBaseTests.La_base_que_se_pregunta_tiene_claves_ajenas_en_mas_de_un_esquema",
        "NingunaClaveAjenaCruzaDeEsquemaEnLaBaseTests.La_pregunta_ve_una_clave_ajena_escrita_a_mano_entre_dos_esquemas",
        "NingunaClaveAjenaCruzaDeEsquemaEnLaBaseTests.Ninguna_clave_ajena_de_la_base_cruza_de_esquema",

        "NadieEscribeEnLaEmpresaDeOtroTests.Con_la_empresa_de_uno_no_estorba",
        "NadieEscribeEnLaEmpresaDeOtroTests.Un_alta_con_la_empresa_de_otro_no_llega_a_la_base",
        "NadieEscribeEnLaEmpresaDeOtroTests.Y_una_modificacion_que_cambia_la_empresa_de_una_fila_tampoco",

        "PertenenciasEntreEmpresasTests.Entrando_en_la_empresa_si_se_administra_la_que_ya_tiene_gente",
        "PertenenciasEntreEmpresasTests.Una_empresa_vacia_se_puebla_desde_fuera_y_deja_de_admitirlo_en_cuanto_tiene_a_alguien",

        "ReprocesarNoDuplicaTests.Cada_consumidor_tiene_su_turno_aunque_sea_el_mismo_evento",
        "ReprocesarNoDuplicaTests.Dos_hechos_distintos_se_atienden_los_dos",
        "ReprocesarNoDuplicaTests.El_mismo_evento_dos_veces_deja_su_efecto_una_sola",
        "ReprocesarNoDuplicaTests.Un_hecho_que_no_escucha_nadie_no_es_un_error",

        "SesionesYTokensTests.Cerrar_sesion_borra_la_cookie_y_deja_el_refresco_inservible",
        "SesionesYTokensTests.El_correo_que_no_existe_y_la_contrasena_mala_dan_la_MISMA_respuesta",
        "SesionesYTokensTests.El_refresco_viaja_en_una_cookie_httpOnly_y_no_en_el_cuerpo",
        "SesionesYTokensTests.El_token_de_acceso_lleva_dentro_la_empresa_activa_el_usuario_y_los_permisos",
        "SesionesYTokensTests.No_se_puede_pasar_a_una_empresa_a_la_que_uno_no_pertenece",
        "SesionesYTokensTests.Renovar_devuelve_otro_refresco_y_el_anterior_deja_de_valer",
        "SesionesYTokensTests.Reutilizar_un_refresco_ya_canjeado_tumba_tambien_al_que_lo_sustituyo",
        "SesionesYTokensTests.Tras_cinco_intentos_fallidos_la_cuenta_no_admite_ni_la_contrasena_buena",
        "SesionesYTokensTests.Un_token_que_no_pasa_alguna_de_las_comprobaciones_del_borde_no_entra",

        "SinLaTablaElPublicadorSeParaTests.Contra_una_base_sin_migrar_se_para_y_lo_dice_una_sola_vez",
        "SinLaTablaElPublicadorSeParaTests.Y_con_el_esquema_puesto_pero_sin_la_tabla_hace_lo_mismo",

        // Del ítem 2.3, y es el ADR-0037 cobrado POR EL EFECTO: lo que el 2.2 comprobó por lo
        // que el puerto contesta, aquí se comprueba por lo que eso provoca en el alta de un
        // ajuste y en la lectura de un movimiento ya escrito.
        "UnAlmacenBloqueadoNoAdmiteAjustesTests.Bloquear_el_almacen_cierra_el_alta_y_deja_en_pie_lo_ya_escrito",
        "UnAlmacenBloqueadoNoAdmiteAjustesTests.Un_almacen_bloqueado_y_uno_inventado_no_contestan_lo_mismo",

        "UnCambioEnUnMaestroDejaSuRastroTests.El_alta_de_un_almacen_deja_una_fila_con_quien_donde_y_que",
        "UnCambioEnUnMaestroDejaSuRastroTests.La_direccion_de_un_almacen_viaja_DENTRO_de_la_traza_de_su_dueno",
        "UnCambioEnUnMaestroDejaSuRastroTests.La_traza_de_una_entidad_global_lleva_la_empresa_DESDE_LA_QUE_se_actuo",
        "UnCambioEnUnMaestroDejaSuRastroTests.Lo_que_se_escribe_sin_empresa_lleva_el_motivo_y_no_un_hueco",
        "UnCambioEnUnMaestroDejaSuRastroTests.Todas_las_filas_de_un_mismo_guardado_comparten_correlacion",
        "UnCambioEnUnMaestroDejaSuRastroTests.Una_modificacion_deja_el_antes_y_el_despues_de_lo_que_cambio_y_solo_de_eso",
        "UnCambioEnUnMaestroDejaSuRastroTests.Una_peticion_que_no_cambia_nada_no_deja_traza",
    ];

    [Fact]
    public void Los_casos_de_este_carril_son_los_declarados() =>
        CensoDeReglas.Comprobar(typeof(ElCensoDeEsteCarrilTests).Assembly, s_declarados);
}
