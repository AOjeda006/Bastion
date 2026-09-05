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
        "EsquemaDeTercerosTests.El_identificador_fiscal_son_TRES_columnas_y_no_una_cadena_suelta",
        "EsquemaDeTercerosTests.La_empresa_se_guarda_como_identificador_y_NO_como_clave_ajena",
        "EsquemaDeTercerosTests.La_tabla_del_modulo_esta_en_SU_esquema_y_en_snake_case",
        "EsquemaDeTercerosTests.La_unicidad_del_identificador_esta_EN_LA_BASE_y_abarca_tambien_lo_bloqueado",

        "LaEdadDelMasViejoSeMideTests.El_publicador_publica_la_edad_del_pendiente_mas_viejo",
        "LaEdadDelMasViejoSeMideTests.Y_con_la_cola_vacia_la_edad_vuelve_a_cero",

        "LaFilaBloqueadaSigueEnLaBaseTests.Desbloquear_por_su_puerta_devuelve_la_MISMA_fila_y_no_una_copia",
        "LaFilaBloqueadaSigueEnLaBaseTests.Suprimir_por_la_API_deja_la_fila_entera_con_su_motivo_y_su_fecha",

        "LaMismaClaveDevuelveElMismoRecursoTests.De_dos_peticiones_simultaneas_con_la_misma_clave_solo_una_hace_el_trabajo",
        "LaMismaClaveDevuelveElMismoRecursoTests.El_recibo_y_el_almacen_llevan_el_mismo_xmin",
        "LaMismaClaveDevuelveElMismoRecursoTests.El_reintento_con_la_misma_clave_devuelve_los_mismos_bytes_y_no_crea_otro",
        "LaMismaClaveDevuelveElMismoRecursoTests.La_cabecera_en_una_ruta_que_no_la_admite_es_400",
        "LaMismaClaveDevuelveElMismoRecursoTests.La_misma_clave_con_otro_cuerpo_es_409",
        "LaMismaClaveDevuelveElMismoRecursoTests.La_misma_clave_desde_otra_empresa_hace_su_propio_trabajo",
        "LaMismaClaveDevuelveElMismoRecursoTests.Un_alta_rechazada_deja_la_clave_libre_para_el_reintento",
        "LaMismaClaveDevuelveElMismoRecursoTests.Una_clave_que_no_identifica_nada_es_400",

        "LaPuertaDeCadaAccionTests.Con_su_permiso_y_solo_con_el_suyo_ninguna_accion_responde_401_ni_403",
        "LaPuertaDeCadaAccionTests.Con_un_permiso_que_no_es_el_suyo_toda_accion_protegida_responde_403",
        "LaPuertaDeCadaAccionTests.Las_acciones_sin_permiso_las_puede_usar_cualquiera_que_haya_entrado",
        "LaPuertaDeCadaAccionTests.Las_tres_acciones_anonimas_se_alcanzan_sin_credenciales",
        "LaPuertaDeCadaAccionTests.Ninguna_accion_contesta_con_un_fallo_del_servidor_al_sondeo",
        "LaPuertaDeCadaAccionTests.Sin_credenciales_toda_accion_protegida_responde_401",
        "LaPuertaDeCadaAccionTests.Una_ruta_que_no_existe_es_404_para_quien_si_se_ha_identificado",

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

        "LosPermisosQueNombraElFrontalTests.Todo_permiso_que_el_frontal_teclea_lo_sirve_la_api",

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
