using Bastion.BuildingBlocks.Domain.Dinero;
using Bastion.BuildingBlocks.Domain.Multiempresa;

namespace Bastion.Inventario.Domain.Valoraciones;

/// <summary>
/// Lo que vale lo que hay de un artículo en un almacén: la cantidad y el valor total, de los que se
/// deduce el precio medio (ADR-0046 §5).
/// </summary>
/// <remarks>
/// <para>
/// <b>Es una proyección del libro, como la existencia.</b> La cantidad es la suma de sus filas, y el
/// valor, la suma de su columna <c>valor</c>. Que las dos cosas se cumplan no se da por supuesto: lo
/// comprueban el cuadre y la propiedad.
/// </para>
/// <para>
/// <b>Aquí no hay forma de moverla</b>, por lo mismo que la existencia. La escriben tres sentencias
/// crudas, en la transacción que anota el libro: una la bloquea, otra la lee y la última suma sobre
/// lo bloqueado (ADR-0046 §2). Si se leyera por el ORM para escribirla, dos confirmaciones del mismo
/// artículo valorarían contra el mismo saldo.
/// </para>
/// <para>
/// <b>La clave es la empresa, el artículo y el almacén</b>, sin ubicación ni lote (ADR-0046 §3). Es
/// la clave primaria, porque ninguna de las tres columnas puede ser nula, al contrario que el lote
/// de la existencia.
/// </para>
/// </remarks>
public sealed class Valoracion : IDeInquilino
{
    /// <summary>Constructor de materialización: EF Core rellena la fila.</summary>
    private Valoracion()
    {
    }

    /// <inheritdoc/>
    public Guid EmpresaId { get; private set; }

    /// <summary>El artículo. Vive en el esquema de Catálogo.</summary>
    public Guid ArticuloId { get; private set; }

    /// <summary>El almacén. Vive en el esquema de Organización.</summary>
    public Guid AlmacenId { get; private set; }

    /// <summary>Lo que hay en el almacén, en la unidad base: la suma de todas sus ubicaciones.</summary>
    public decimal Cantidad { get; private set; }

    /// <summary>La divisa del valor.</summary>
    /// <remarks>
    /// Es la del documento que la escribió por última vez. Un documento en otra divisa no la mezcla:
    /// se rechaza con <c>ajuste-valoracion-en-otra-divisa</c> mientras quede cantidad, y empieza de
    /// nuevo si no queda nada (ADR-0046 §7).
    /// </remarks>
    public string Divisa { get; private set; } = string.Empty;

    /// <summary>
    /// La fecha de operación más alta de las filas del libro de la clave (ADR-0047). La pone la
    /// sentencia que suma, que se niega a moverla hacia atrás.
    /// </summary>
    /// <remarks>Es nula en la clave que el cerrojo acaba de crear y que todavía no se ha movido.</remarks>
    public DateOnly? UltimaFecha { get; private set; }

    /// <summary>El valor total, que es la verdad: el precio medio se deduce de él.</summary>
    public Importe Valor => Importe.De(ValorSinDivisa, Divisa);

    /// <summary>La cantidad, el valor y el último movimiento juntos, con el precio medio deducido.</summary>
    public SaldoValorado Saldo => new(Cantidad, Valor, UltimaFecha);

    /// <summary>La columna del valor, sin divisa: lo que EF Core lee y escribe.</summary>
    private decimal ValorSinDivisa { get; set; }
}
