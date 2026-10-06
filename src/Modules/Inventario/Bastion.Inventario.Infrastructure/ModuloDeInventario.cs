using Bastion.BuildingBlocks.Infrastructure.Auditoria;
using Bastion.BuildingBlocks.Infrastructure.BandejaDeSalida;
using Bastion.BuildingBlocks.Infrastructure.Entidades;
using Bastion.BuildingBlocks.Infrastructure.Errores;
using Bastion.BuildingBlocks.Infrastructure.Idempotencia;
using Bastion.Catalogo.Contracts.Catalogo;
using Bastion.Inventario.Application;
using Bastion.Inventario.Application.Ajustes;
using Bastion.Inventario.Application.Transferencias;
using Bastion.Inventario.Contracts.Ajustes;
using Bastion.Inventario.Contracts.Movimientos;
using Bastion.Inventario.Contracts.Recuentos;
using Bastion.Inventario.Contracts.Transferencias;
using Bastion.Inventario.Infrastructure.Persistencia;
using Bastion.Inventario.Infrastructure.Persistencia.Configuraciones;
using Bastion.Inventario.Infrastructure.Persistencia.Existencias;
using Bastion.Inventario.Infrastructure.Persistencia.Repositorios;
using Bastion.Organizacion.Contracts.Ejercicios;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bastion.Inventario.Infrastructure;

/// <summary>
/// Registro del módulo Inventario en el contenedor. Lo llama el <i>composition root</i>
/// (<c>src/Api</c>), que es el único proyecto autorizado a ver esta capa.
/// </summary>
/// <remarks>
/// Un módulo no se registra a sí mismo ni alcanza la infraestructura de otro: la construcción del
/// sistema está separada de su uso (`principios/clean-architecture.md`).
/// </remarks>
public static class ModuloDeInventario
{
    /// <summary>Registra el contexto, el repositorio y los casos de uso del módulo.</summary>
    /// <param name="servicios">Colección de servicios del <i>composition root</i>.</param>
    /// <param name="cadenaDeConexion">Cadena de conexión a PostgreSQL.</param>
    /// <returns>La misma colección, para encadenar.</returns>
    public static IServiceCollection AgregarModuloDeInventario(
        this IServiceCollection servicios,
        string cadenaDeConexion)
    {
        ArgumentNullException.ThrowIfNull(servicios);

        servicios.AddDbContext<InventarioDbContext>((alcance, opciones) =>
        {
            InventarioDbContext.Configurar(opciones, cadenaDeConexion);

            // Los tres interceptores, como en los demás módulos: la traza (ADR-0012), la marca de
            // última modificación (R14) y los eventos (R12, ADR-0013) entran en el MISMO
            // `SaveChanges` que el cambio.
            //
            // Los dos primeros no tienen nada que hacer con el libro —no se audita y sus filas no
            // se modifican nunca—, y aun así se registran: el contexto también escribe la traza,
            // el evento y el recibo de idempotencia del DOCUMENTO, que son tablas compartidas y sí
            // pasan por ellos. Quitarlos «porque el libro no los usa» apagaría la auditoría del
            // ajuste sin que nada fallara.
            opciones.AddInterceptors(alcance.GetRequiredService<InterceptorDeAuditoria>());
            opciones.AddInterceptors(alcance.GetRequiredService<InterceptorDeMarcasDeTiempo>());
            opciones.AddInterceptors(alcance.GetRequiredService<InterceptorDeLaBandeja>());
        });

        // Bajo el tipo del MÓDULO, no bajo `IUnidadTrabajo` a secas, por lo mismo que en los otros
        // cuatro: con el tipo común la última inscripción gana, y confirmar sobre el contexto ajeno
        // devuelve cero sin excepción y sin rastro.
        servicios.AddScoped<IUnidadTrabajoDeInventario, UnidadDeTrabajoDeInventario>();

        // UNO para los dos agregados —el documento y el libro—, y el porqué está en su interfaz:
        // se guardan en la misma transacción, así que dos repositorios con dos unidades de trabajo
        // sugerirían que pueden guardarse por separado.
        servicios.AddScoped<IRepositorioDeAjustes, RepositorioDeAjustes>();

        // Y uno para la transferencia, por lo mismo: el documento, el libro y lo que vuela se
        // guardan en la misma transacción (ADR-0053 §1).
        servicios.AddScoped<IRepositorioDeTransferencias, RepositorioDeTransferencias>();

        // INVENTARIO TIENE DOCUMENTOS, y aquí es donde lo dice. Organización no sabe qué módulos
        // los tienen: pregunta a los que se hayan inscrito, y un módulo que no se inscriba
        // sencillamente NO EXISTE para el cierre —por eso el caso de uso afirma antes que la
        // colección no está vacía (ADR-0020), y por eso hay un barrido que compara los inscritos
        // con los declarados en los dos sentidos.
        //
        // Va con `AddScoped` y NO con `TryAdd`: la colección es de VARIOS, uno por módulo, y un
        // `TryAdd` dejaría fuera a todos menos al primero sin decir nada.
        servicios.AddScoped<IDocumentosDeUnPeriodo, LosDocumentosDeInventarioEnUnPeriodo>();

        // Y EL PUERTO DEL EJERCICIO, contestado desde AQUI y no desde Organizacion. No es una
        // rareza del cableado: la respuesta trae un cerrojo compartido sobre la fila del
        // ejercicio, y un cerrojo solo sirve si vive en la MISMA transaccion que el documento que
        // se esta confirmando. Contestarlo desde `OrganizacionDbContext` seria otra conexion, y el
        // cerrojo se soltaria antes de que el documento llegara a escribirse.
        servicios.AddScoped<IConsultaDeEjercicios, LosEjerciciosDesdeInventario>();

        // Y EL DE LA MARCA DEL ARTÍCULO, por lo mismo (ADR-0048 §4): al confirmar se lee con
        // `FOR SHARE`, y el cerrojo tiene que vivir en la transacción del documento. Lo publica
        // Catálogo y se contesta aquí, con SQL crudo sobre `catalogo.articulos`.
        servicios.AddScoped<IConsultaDeTrazabilidad, LaTrazabilidadDesdeInventario>();

        // LO QUE ESTE MÓDULO EXPONE A LOS DEMÁS, bajo el tipo de su `Contracts`, y la mitad de
        // vuelta del segundo cruce mutuo (ADR-0048 §7): Catálogo pregunta por aquí si un artículo
        // tiene movimientos antes de cambiarle la marca. Sin esta línea todo compila, y lo que
        // falla es modificar CUALQUIER artículo, porque el caso de uso lo recibe por constructor.
        servicios.AddScoped<IMovimientosDeArticulos, LosMovimientosDeUnArticulo>();

        // LOS EVENTOS DE LOS DOCUMENTOS, con su nombre escrito a mano: el catálogo no lo saca del
        // tipo a propósito, porque renombrar la clase rompería las filas que ya están en la cola.
        // Uno POR DOCUMENTO y paso, y no por movimiento: uno por fila convertiría la bandeja en una
        // segunda copia de la tabla que más crece del sistema. La transferencia tiene tres porque
        // escribe en el libro en dos momentos (ADR-0053).
        servicios.DeclararEvento<AjusteConfirmado>(AjusteConfirmado.Nombre);
        servicios.DeclararEvento<AjusteAnulado>(AjusteAnulado.Nombre);
        servicios.DeclararEvento<TransferenciaEnviada>(TransferenciaEnviada.Nombre);
        servicios.DeclararEvento<TransferenciaRecibida>(TransferenciaRecibida.Nombre);
        servicios.DeclararEvento<TransferenciaAnulada>(TransferenciaAnulada.Nombre);

        // LOS TRES DEL RECUENTO, que no escribe en el libro: cuentan sus transiciones, porque la R1
        // no deja transitar sin evento, y ninguno lleva importes. El que va al asiento es el
        // `AjusteConfirmado` de su ajuste (ADR-0055 §1).
        servicios.DeclararEvento<RecuentoConfirmado>(RecuentoConfirmado.Nombre);
        servicios.DeclararEvento<RecuentoAnulado>(RecuentoAnulado.Nombre);
        servicios.DeclararEvento<RecuentoDescartado>(RecuentoDescartado.Nombre);

        // El almacén de claves de idempotencia (R10), con la clave del módulo: el filtro del borde
        // resuelve el suyo por el segmento de la ruta, para que la clave y el trabajo caigan en la
        // transacción del MISMO contexto.
        //
        // Entra en el ítem 2.4 y ni un día antes, cuando el módulo estrena borde. Y aquí hace más
        // que guardar respuestas: la única acción que publica el borde EXIGE la cabecera, así que
        // esta inscripción es también la que pone la transacción dentro de la cual el contador y
        // el documento se escriben a la vez.
        servicios.AgregarAlmacenDeIdempotencia<AlmacenDeIdempotenciaDeInventario>(
            InventarioDbContext.Esquema);

        // EL NUMERADOR no lo pide el borde, lo pide el caso de uso que confirma. Va bajo el tipo
        // del MÓDULO por lo mismo que la unidad de trabajo: su sentencia corre en la transacción
        // de ESTE contexto, y con el tipo común la última inscripción ganaría y el número
        // saldría de una transacción que no es la del documento.
        servicios.AddScoped<INumeradorDeSeriesDeInventario, NumeradorDeSeriesDeInventario>();

        // Sin `IConsultaDeLoBloqueado` y sin cargador de semillas, por el mismo motivo que Catálogo:
        // el libro no guarda datos de ninguna persona -no es bloqueable, y su configuración lo dice
        // con su porqué-, y qué existencias tiene una empresa no es un maestro de la instalación.
        // EL ÍNDICE ÚNICO DEL INVERSO CONTESTA POR EL TESTIGO cuando gana la carrera, y esto es
        // lo que impide que esa victoria se pague con un 500. Se declara AQUÍ, en el módulo que
        // es dueño del índice, y no en la política de errores: la política fija el orden de los
        // manejadores y el mecanismo; que `ix_ajustes_anula_a_id` sea una carrera perdida y no un
        // defecto es una afirmación sobre este agregado y de nadie más.
        servicios.Configure<IndicesQueDelatanUnaCarreraPerdida>(indices => indices.Declarar(
            "ix_ajustes_anula_a_id",
            "anular escribe DOS filas —inserta el inverso y cambia el estado del original— y el " +
            "ORM decide cuál va antes. Cuando va antes el INSERT, quien pierde la carrera choca " +
            "contra este índice en vez de contra el testigo del original, y las dos cosas " +
            "significan exactamente lo mismo: otra anulación legítima llegó primero. No hay " +
            "ningún otro desenlace posible, porque un `anula_a_id` repetido solo se escribe " +
            "anulando dos veces el mismo documento"));

        // Y EL DE LA TRANSFERENCIA, por lo mismo y con el mismo argumento (ADR-0053 §5): anular
        // también inserta el inverso y cambia el original.
        servicios.Configure<IndicesQueDelatanUnaCarreraPerdida>(indices => indices.Declarar(
            ConfiguracionDeTransferencia.IndiceDelInverso,
            "anular una transferencia escribe DOS filas —inserta el inverso y cambia el estado del " +
            "original—, como anular un ajuste, y el ORM decide cuál va antes. Quien pierde la " +
            "carrera choca contra este índice o contra el testigo del original, y las dos cosas " +
            "significan lo mismo: otra anulación legítima llegó primero. Un `anula_a_id` repetido " +
            "solo se escribe anulando dos veces la misma transferencia"));

        // Y LA RESTRICCIÓN QUE GUARDA EL STOCK contesta con su regla, y no con un 500. Se declara
        // aquí por lo mismo que el índice: que un físico por debajo de cero sea «no hay bastante
        // stock» y no un defecto es una afirmación sobre la existencia, y es de este módulo.
        //
        // Y LAS DOS DEL NÚMERO DE SERIE, al mismo error (ADR-0048 §3): el CHECK salta cuando la
        // unidad entra otra vez en la misma ubicación, y el índice, cuando entra en otra. El índice es un 23505,
        // y NO va a la lista de la carrera perdida: quien llega segundo no tiene nada que recargar.
        servicios.Configure<RestriccionesQueGuardanUnaRegla>(restricciones => restricciones
            .Declarar(
                ConfiguracionDeExistencia.FisicoNoNegativo,
                ClaseDeRestriccion.Comprobacion,
                ErroresDeExistencias.StockInsuficiente(),
                "leer el saldo, compararlo y escribir deja una ventana que dos salidas simultáneas " +
                "cruzan juntas, y las dos pasarían la comprobación. El CHECK se evalúa con la fila " +
                "ya bloqueada y la cantidad ya sumada, así que es la única guarda que no se saltan; " +
                "y lo único que puede significar que salte es que no había bastante stock " +
                "(ADR-0046 §4)")
            .Declarar(
                ConfiguracionDeExistencia.NumeroDeSerieComoMuchoUna,
                ClaseDeRestriccion.Comprobacion,
                ErroresDeExistencias.NumeroDeSerieEnExistencias(),
                "una serie que entra otra vez en la ubicación donde ya está deja su fila en dos. " +
                "Mirar antes que la fila esté a cero deja la ventana de siempre, y dos entradas " +
                "simultáneas la pasarían juntas. El CHECK se evalúa con la fila ya bloqueada y la " +
                "cantidad ya sumada, y lo único que puede significar que salte es que esa unidad ya " +
                "estaba dentro (ADR-0048 §3)")
            .Declarar(
                ConfiguracionDeExistencia.NumeroDeSerieEnUnSitio,
                ClaseDeRestriccion.Unicidad,
                ErroresDeExistencias.NumeroDeSerieEnExistencias(),
                "una serie que entra en otra ubicación, o en otro almacén, mientras sigue en la " +
                "primera. Un CHECK de fila no ve las demás filas, y leer antes deja la ventana que " +
                "cruzan dos confirmaciones a la vez. El índice único parcial se comprueba con cada " +
                "fila que se escribe y espera a la transacción que tenga la otra, y lo único que " +
                "puede significar que salte es que esa unidad ya está en otro sitio. Reintentar da " +
                "lo mismo, así que no es una carrera perdida (ADR-0048 §3)"));

        servicios.AgregarCasosDeUsoDeInventario();

        return servicios;
    }
}
