namespace Bastion.BuildingBlocks.Domain.Entidades;

/// <summary>
/// Lo que toda entidad que es un recurso por sí misma lleva encima: cuándo nació y cuándo se
/// tocó por última vez.
/// </summary>
/// <remarks>
/// <para>
/// <b>Qué no aporta, y a propósito.</b> No aporta identidad ni igualdad: cada entidad declara su
/// <c>Id</c> y lo genera en su fábrica, que es como estaba antes del 0.10 y no era lo que se
/// había escrito tres veces. Tampoco aporta el bloqueo: bloquearse no le pasa a todo el mundo
/// —un ejercicio se cierra, no se bloquea—, así que eso es <see cref="Bloqueos.IBloqueable"/> y
/// no un miembro heredado que la mitad de las entidades tendrían que ignorar.
/// </para>
/// <para>
/// <b>Las dos marcas son instantes, no fechas de negocio.</b> Van en <see cref="DateTimeOffset"/>
/// y aterrizan en <c>timestamptz</c>. La distinción es de R14 y no es cosmética: una fecha de
/// negocio —el devengo de una factura, el inicio de un ejercicio— es un día del calendario y no
/// tiene hora ni zona; un instante sí. Guardar un instante en <c>date</c> pierde información que
/// no se recupera, y guardar una fecha de negocio en <c>timestamptz</c> la mueve de día en cuanto
/// alguien la lee desde otra zona horaria.
/// </para>
/// <para>
/// <b>De dónde sale la hora.</b> <see cref="CreadoEn"/> la pone el dominio: la fábrica de cada
/// entidad recibe el instante y la entidad nace con él, así que nunca existe una entidad válida
/// sin fecha de creación, ni siquiera en una prueba unitaria que no ve una base de datos.
/// <see cref="ModificadoEn"/> la pone el interceptor de marcas de tiempo al guardar, leyendo el
/// mismo <c>TimeProvider</c> inyectado. Son dos mecanismos distintos porque los dos problemas lo
/// son: la creación ocurre en un solo sitio por entidad y se puede sostener a mano; la
/// modificación ocurre en todos los métodos que cambian algo —presentes y futuros—, y sostenerla
/// a mano significa que el día que alguien escriba un método nuevo y no se acuerde, la marca deja
/// de moverse <b>sin que nada falle</b>.
/// </para>
/// <para>
/// <b>Lo que ninguno de los dos es:</b> un <c>DEFAULT now()</c>. Eso ataría las dos columnas al
/// reloj del servidor de base de datos, que es justo el que una prueba no puede adelantar, y
/// además metería una sexta forma de valor generado por el servidor en un modelo donde lo único
/// que genera el servidor son los testigos de concurrencia (ADR-0015).
/// </para>
/// </remarks>
public abstract class EntidadBase
{
    /// <summary>Crea la entidad con sus dos marcas puestas en el mismo instante.</summary>
    /// <param name="momento">Ahora, de quien tenga el <c>TimeProvider</c>.</param>
    protected EntidadBase(DateTimeOffset momento)
    {
        CreadoEn = momento;
        ModificadoEn = momento;
    }

    /// <summary>Constructor de materialización: EF Core rellena las dos marcas desde la fila.</summary>
    protected EntidadBase()
    {
    }

    /// <summary>Instante en el que se creó. No cambia nunca.</summary>
    public DateTimeOffset CreadoEn { get; private set; }

    /// <summary>Instante del último cambio guardado.</summary>
    /// <remarks>
    /// Al nacer vale lo mismo que <see cref="CreadoEn"/>, y no <c>null</c>: una entidad recién
    /// creada sí tiene una última modificación —su creación—, así que la columna es obligatoria y
    /// ordenar por ella no necesita decidir dónde van los huecos. «Nunca se ha tocado» se lee
    /// comparando las dos, que es una pregunta que casi nadie hace y que no vale una columna
    /// anulable en todas las tablas.
    /// </remarks>
    public DateTimeOffset ModificadoEn { get; private set; }
}
