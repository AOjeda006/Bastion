using Bastion.Pruebas.Comun;

namespace Bastion.Api.FunctionalTests;

/// <summary>
/// El censo del carril funcional: sus casos, nombrados uno a uno.
/// </summary>
/// <remarks>
/// <para>
/// Este carril comprueba los tipos que la API declara, sus rutas, sus permisos y sus cabeceras, con el host
/// en pie y sin una sola dependencia externa. Casi todos sus casos son <b>reglas</b>: afirmaciones sobre un
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
        "CadaAccionDeclaraSuPermisoTests.Cada_permiso_es_del_modulo_por_cuya_ruta_se_entra",
        "CadaAccionDeclaraSuPermisoTests.Escribir_y_modificar_no_comparten_permiso_aunque_los_escriba_el_mismo_codigo",
        "CadaAccionDeclaraSuPermisoTests.Ninguna_accion_autoriza_por_rol_ni_por_una_politica_inventada",
        "CadaAccionDeclaraSuPermisoTests.Toda_accion_o_exige_un_permiso_o_esta_en_la_lista_de_excepciones",
        "CadaAccionDeclaraSuPermisoTests.Todo_permiso_del_catalogo_lo_exige_alguna_accion",
        "CadaAccionDeclaraSuPermisoTests.Todo_permiso_exigido_existe_en_el_catalogo_que_registra_el_host",

        "CadaEntidadDeclaraSuAuditoriaTests.La_clasificacion_de_una_entidad_que_no_se_audita_no_se_queda_por_ahi",
        "CadaEntidadDeclaraSuAuditoriaTests.Las_propiedades_de_un_tipo_complejo_entran_en_este_barrido",
        "CadaEntidadDeclaraSuAuditoriaTests.Ninguna_entidad_del_modelo_se_queda_sin_decir_si_se_audita",
        "CadaEntidadDeclaraSuAuditoriaTests.Ninguna_propiedad_de_una_entidad_auditada_se_queda_sin_clasificar",
        "CadaEntidadDeclaraSuAuditoriaTests.Toda_entidad_que_queda_fuera_lleva_su_motivo_escrito",
        "CadaEntidadDeclaraSuAuditoriaTests.Toda_propiedad_que_queda_fuera_o_es_secreta_lleva_su_motivo",
        "CadaEntidadDeclaraSuAuditoriaTests.Una_entidad_propiedad_de_otra_hereda_la_decision_de_su_dueno_y_no_la_repite",

        "CadaEntidadDeclaraSuInquilinatoTests.La_lista_de_globales_no_nombra_entidades_que_ya_no_estan_o_que_si_filtran",
        "CadaEntidadDeclaraSuInquilinatoTests.Ninguna_entidad_del_modelo_se_queda_sin_filtro_y_sin_motivo",
        "CadaEntidadDeclaraSuInquilinatoTests.Toda_entidad_marcada_como_de_inquilino_filtra",
        "CadaEntidadDeclaraSuInquilinatoTests.Toda_entidad_que_filtra_sin_ser_de_inquilino_esta_documentada",

        "CadaEventoEstaDeclaradoTests.Ningun_evento_de_integracion_se_queda_sin_declarar",
        "CadaEventoEstaDeclaradoTests.Ninguna_declaracion_nombra_un_evento_que_ya_no_existe",
        "CadaEventoEstaDeclaradoTests.Todos_los_nombres_tienen_la_forma_acordada",

        "ElCensoDeEsteCarrilTests.Los_casos_de_este_carril_son_los_declarados",

        "ElCuerpoQueNoEncajaTests.El_400_del_enlace_de_modelo_sale_por_la_politica_central_con_su_traza",
        "ElCuerpoQueNoEncajaTests.Los_mensajes_del_contrato_siguen_saliendo_enteros",
        "ElCuerpoQueNoEncajaTests.Un_cuerpo_con_otra_forma_es_400_y_no_nombra_ni_un_tipo_de_C",

        "ElFiltroNoSeSaltaPorAhiTests.Cada_motivo_para_ver_lo_bloqueado_tiene_su_sitio_y_cada_sitio_su_motivo",
        "ElFiltroNoSeSaltaPorAhiTests.El_ambito_que_ve_lo_bloqueado_solo_se_abre_donde_esta_declarado",
        "ElFiltroNoSeSaltaPorAhiTests.El_ambito_sin_inquilino_solo_se_abre_donde_esta_declarado",
        "ElFiltroNoSeSaltaPorAhiTests.La_lista_de_saltos_permitidos_no_nombra_sitios_que_ya_no_existen",
        "ElFiltroNoSeSaltaPorAhiTests.Los_filtros_globales_se_definen_solo_en_los_contextos_de_modulo",
        "ElFiltroNoSeSaltaPorAhiTests.Ningun_camino_que_ve_lo_bloqueado_emite_un_testigo_de_version",
        "ElFiltroNoSeSaltaPorAhiTests.Ninguna_llamada_de_las_que_rodean_el_filtro_aparece_en_el_codigo",

        // Del ítem 1.4: la promesa de `AgregarInquilinato` convertida en algo que se construye.
        "ElInquilinatoSeConstruyeSoloTests.Todo_lo_que_registra_el_inquilinato_se_puede_construir_sin_nada_mas",
        "ElInquilinatoSeConstruyeSoloTests.Y_el_acceso_a_lo_bloqueado_se_construye_nombrandolo",
        "ElInquilinatoSeConstruyeSoloTests.Y_llamarlo_dos_veces_no_registra_nada_dos_veces",

        "ElFiltroSeLeeEnCadaConsultaTests.Dos_consultas_seguidas_con_dos_empresas_llevan_dos_filtros_distintos",
        "ElFiltroSeLeeEnCadaConsultaTests.Y_el_mismo_contexto_reutilizado_tampoco_se_queda_con_la_primera",
        "ElFiltroSeLeeEnCadaConsultaTests.Y_el_mismo_orden_al_reves_da_el_mismo_resultado",

        "ElResumenDeContrasenasTests.Dos_resumenes_de_la_MISMA_contrasena_son_distintos",
        "ElResumenDeContrasenasTests.El_hasher_es_uno_solo_para_todo_el_proceso",
        "ElResumenDeContrasenasTests.El_resumen_comprueba_la_contrasena_buena_y_rechaza_cualquier_otra",
        "ElResumenDeContrasenasTests.El_resumen_de_relleno_cuesta_lo_mismo_que_uno_de_verdad_y_no_lo_abre_nadie",
        "ElResumenDeContrasenasTests.El_resumen_lleva_dentro_los_parametros_que_declara_el_ADR_0008",

        "LaBandejaSeMideYNoSeSondeaTests.El_medidor_publica_la_edad_del_mas_viejo_en_segundos",
        "LaBandejaSeMideYNoSeSondeaTests.La_bandeja_no_esta_en_ninguna_sonda",
        "LaBandejaSeMideYNoSeSondeaTests.Y_cuenta_lo_publicado_y_lo_aparcado_por_separado",

        "LaClaveDeIdempotenciaEsLaTuplaEnteraTests.El_objetivo_del_conflicto_es_la_clave_primaria_entera",
        "LaClaveDeIdempotenciaEsLaTuplaEnteraTests.Hay_un_hueco_por_columna_y_estan_numerados_en_orden",
        "LaClaveDeIdempotenciaEsLaTuplaEnteraTests.La_clave_primaria_es_la_tupla_entera",
        "LaClaveDeIdempotenciaEsLaTuplaEnteraTests.La_empresa_esta_en_las_columnas_y_en_el_objetivo_del_conflicto",
        "LaClaveDeIdempotenciaEsLaTuplaEnteraTests.La_sentencia_escribe_la_identidad_mas_la_huella_y_el_instante",
        "LaClaveDeIdempotenciaEsLaTuplaEnteraTests.La_sentencia_nombra_la_tabla_del_modelo",
        "LaClaveDeIdempotenciaEsLaTuplaEnteraTests.La_sentencia_rellena_todas_las_columnas_obligatorias",
        "LaClaveDeIdempotenciaEsLaTuplaEnteraTests.Ninguna_columna_de_la_sentencia_se_ha_inventado",

        "LaColaSeDefiendeSolaTests.Dos_eventos_no_pueden_llamarse_igual_ni_uno_llamarse_de_dos_maneras",
        "LaColaSeDefiendeSolaTests.El_error_se_recorta_para_que_una_excepcion_enorme_no_reviente_el_guardado",
        "LaColaSeDefiendeSolaTests.Publicar_limpia_el_error_del_intento_anterior",
        "LaColaSeDefiendeSolaTests.Un_evento_que_no_sale_se_aparca_al_quinto_intento_y_no_antes",
        "LaColaSeDefiendeSolaTests.Un_evento_sin_declarar_lo_dice_al_volcarlo_y_no_al_leerlo",
        "LaColaSeDefiendeSolaTests.Una_fila_lleva_empresa_o_lleva_el_motivo_por_el_que_no_la_lleva",

        "LasClavesSeConocenAntesDeGuardarTests.Las_entidades_del_tipo_base_y_las_que_llevan_testigo_son_las_MISMAS",
        "LasClavesSeConocenAntesDeGuardarTests.Lo_unico_que_genera_el_servidor_son_los_testigos_de_concurrencia",
        "LasClavesSeConocenAntesDeGuardarTests.Ninguna_propiedad_auditada_la_pone_la_base_de_datos",
        "LasClavesSeConocenAntesDeGuardarTests.Toda_entidad_tiene_su_clave_completa_antes_de_guardar",
        "LasClavesSeConocenAntesDeGuardarTests.Todo_lo_que_genera_el_servidor_es_de_verdad_un_testigo_de_concurrencia",

        "LasFechasDicenDeQueTipoSonTests.El_barrido_encuentra_fechas_de_las_dos_clases",
        "LasFechasDicenDeQueTipoSonTests.No_hay_ni_una_fecha_que_no_diga_si_lleva_zona",
        "LasFechasDicenDeQueTipoSonTests.Toda_fecha_de_calendario_se_guarda_sin_zona_horaria",
        "LasFechasDicenDeQueTipoSonTests.Todo_instante_se_guarda_con_zona_horaria",

        "LasPertenenciasNuevasSeInsertanTests.Con_el_usuario_recien_creado_la_pertenencia_ya_salia_bien",
        "LasPertenenciasNuevasSeInsertanTests.Sin_registrarla_EF_Core_la_daria_por_existente",
        "LasPertenenciasNuevasSeInsertanTests.Un_rol_nuevo_sobre_una_pertenencia_que_ya_existia_tambien_sale_como_alta",
        "LasPertenenciasNuevasSeInsertanTests.Una_pertenencia_concedida_a_un_usuario_ya_guardado_sale_como_alta",

        "LasSemillasLleganDondeSeCarganTests.Las_del_repositorio_y_las_publicadas_son_las_mismas",
        "LasSemillasLleganDondeSeCarganTests.Las_semillas_estan_donde_el_cargador_las_busca",
        "LasSemillasLleganDondeSeCarganTests.Las_unidades_sembradas_son_unidades_validas",
        "LasSemillasLleganDondeSeCarganTests.Los_comentarios_del_fichero_no_estorban",
        "LasSemillasLleganDondeSeCarganTests.Los_impuestos_sembrados_son_impuestos_validos",
        "LasSemillasLleganDondeSeCarganTests.Ningun_impuesto_sembrado_pisa_a_otro_tramo_suyo",
        "LasSemillasLleganDondeSeCarganTests.Sin_carpeta_no_se_da_por_cargado",
        "LasSemillasLleganDondeSeCarganTests.Un_campo_que_falta_no_se_rellena_solo",
        "LasSemillasLleganDondeSeCarganTests.Un_fichero_que_falta_se_dice_por_su_nombre",
        "LasSemillasLleganDondeSeCarganTests.Un_fichero_que_nadie_carga_tambien_se_dice",
        "LasSemillasLleganDondeSeCarganTests.Un_fichero_vacio_no_pasa_por_cargado",
        "LasSemillasLleganDondeSeCarganTests.Una_clave_con_una_errata_no_se_ignora",

        "LosLimitesSeLeenEnCulturaInvarianteTests.Ningun_atributo_de_validacion_revienta_al_validar_en_la_cultura_de_la_aplicacion",
        "LosLimitesSeLeenEnCulturaInvarianteTests.Todo_limite_escrito_como_texto_se_lee_en_cultura_invariante",
        "LosLimitesSeLeenEnCulturaInvarianteTests.Todos_los_atributos_encontrados_se_pueden_sondear",

        "NingunCriterioSensibleViajaEnLaUrlTests.El_barrido_ve_los_listados_y_sus_parametros",
        "NingunCriterioSensibleViajaEnLaUrlTests.Ningun_listado_recibe_un_criterio_sensible_por_la_url",

        "NingunaLecturaEntregaTestigoDeVersionTests.El_barrido_ve_los_cuerpos_y_reconoce_un_testigo",
        "NingunaLecturaEntregaTestigoDeVersionTests.Ninguna_respuesta_de_la_api_lleva_testigo_de_version_en_el_cuerpo",

        "NingunaPeticionNombraLaEmpresaTests.La_lista_de_excepciones_no_nombra_acciones_que_ya_no_la_reciben",
        "NingunaPeticionNombraLaEmpresaTests.Ninguna_accion_recibe_la_empresa_por_la_peticion",

        "PoliticaDeErroresTests.CadaClaseDeError_SeTraduceASuCodigoDeEstadoYASuTypeEstable",
        "PoliticaDeErroresTests.ElDetalleInterno_ViveEnElRegistroYNoEnLaRespuesta",
        "PoliticaDeErroresTests.ElTraceIdDeLaRespuesta_EsElMismoQueElArrobaTrDelRegistro",
        "PoliticaDeErroresTests.LaTeoriaDeArriba_TieneUnaFilaPorClaseDeError",
        "PoliticaDeErroresTests.TodaClaseDeError_TieneCodigoDeEstadoYTitulo",
        "PoliticaDeErroresTests.UnErrorDeNegocio_LlevaLosCamposDelRfc9457",
        "PoliticaDeErroresTests.UnaExcepcionNoControlada_RespondeQuinientosSinNadaDelInterior",
        "PoliticaDeErroresTests.UnaPeticionMalFormada_RespondeCuatrocientosSinNadaDelInterior",
        "PoliticaDeErroresTests.UnaRutaQueNoExiste_LeResponde401AlAnonimoYTambienEnProblemDetails",

        "SinEmpresaNoSeConsultaTests.Con_claim_devuelve_la_empresa_del_claim_y_no_otra",
        "SinEmpresaNoSeConsultaTests.Con_un_ambito_abierto_a_proposito_devuelve_nulo_y_no_lanza",
        "SinEmpresaNoSeConsultaTests.Dos_ambitos_anidados_se_cierran_por_orden_y_el_de_fuera_sigue_abierto",
        "SinEmpresaNoSeConsultaTests.Sin_claim_y_sin_ambito_la_empresa_del_filtro_lanza",

        "SondasDeSaludTests.SondaDeDisponibilidad_SinBaseDeDatos_DiceCualDependenciaFalla",
        "SondasDeSaludTests.SondaDeDisponibilidad_SinBaseDeDatos_RespondeServicioNoDisponible",
        "SondasDeSaludTests.SondaDeVida_SinBaseDeDatos_RespondeCorrecto",

        "TodaEscrituraDiceComoSeProtegeTests.Cada_accion_que_admite_idempotencia_tiene_almacen_en_su_modulo",
        "TodaEscrituraDiceComoSeProtegeTests.El_barrido_encuentra_el_inventario_entero",
        "TodaEscrituraDiceComoSeProtegeTests.El_universo_cubre_a_todos_los_modulos_montados",
        "TodaEscrituraDiceComoSeProtegeTests.La_lista_de_exentas_no_nombra_acciones_que_ya_no_lo_estan",
        "TodaEscrituraDiceComoSeProtegeTests.Ninguna_accion_pide_los_dos_mecanismos_a_la_vez",
        "TodaEscrituraDiceComoSeProtegeTests.Ninguna_accion_que_admite_idempotencia_es_anonima",
        "TodaEscrituraDiceComoSeProtegeTests.Toda_accion_que_cambia_estado_dice_como_se_protege",

        "UnidadDeTrabajoPorModuloTests.CadaModulo_DeclaraSuPropiaUnidadDeTrabajo",
        "UnidadDeTrabajoPorModuloTests.NingunCasoDeUso_PideLaUnidadDeTrabajoComun",
    ];

    [Fact]
    public void Los_casos_de_este_carril_son_los_declarados() =>
        CensoDeReglas.Comprobar(typeof(ElCensoDeEsteCarrilTests).Assembly, s_declarados);
}
