using Bastion.BuildingBlocks.Domain.Dinero;
using Bastion.BuildingBlocks.Infrastructure.Auditoria;
using Bastion.Inventario.Domain.Movimientos;
using Bastion.Inventario.Domain.Valoraciones;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bastion.Inventario.Infrastructure.Persistencia.Configuraciones;

/// <summary>Mapeo de la valoración: una fila por empresa, artículo y almacén (ADR-0046 §3).</summary>
internal sealed class ConfiguracionDeValoracion : IEntityTypeConfiguration<Valoracion>
{
    /// <summary>La tabla, nombrada aquí porque las sentencias de la valoración la escriben en crudo.</summary>
    internal const string Tabla = "valoraciones";

    /// <summary>Lo que hay no baja de cero, igual que la existencia que suma.</summary>
    internal const string CantidadNoNegativa = "ck_valoraciones_cantidad_no_negativa";

    /// <summary>El valor no baja de cero: una salida resta como mucho lo que hay.</summary>
    internal const string ValorNoNegativo = "ck_valoraciones_valor_no_negativo";

    /// <summary>Sin cantidad no hay valor: vaciar la clave se lleva todo el valor.</summary>
    internal const string SinCantidadNoHayValor = "ck_valoraciones_sin_cantidad_no_hay_valor";

    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<Valoracion> valoracion)
    {
        ArgumentNullException.ThrowIfNull(valoracion);

        // LAS TRES GUARDAS DE LA TABLA DEL ADR-0046 §5, las mismas que `SaldoValorado`. No las
        // traduce el borde, porque ninguna la puede incumplir un documento: el dominio resta como
        // mucho lo que hay, y vaciar la clave se lleva todo el valor. Si una salta, es un defecto,
        // y un 500 es lo que tiene que contestar. La cantidad, además, es la suma de existencias
        // que ya no bajan de cero, y su sentencia va antes.
        valoracion.ToTable(
            Tabla,
            tabla =>
            {
                tabla.HasCheckConstraint(CantidadNoNegativa, "cantidad >= 0");
                tabla.HasCheckConstraint(ValorNoNegativo, "valor >= 0");
                tabla.HasCheckConstraint(SinCantidadNoHayValor, "cantidad > 0 OR valor = 0");
            });

        // NO SE AUDITA, por lo mismo que la existencia: la escriben sentencias crudas, que no pasan
        // por el rastreador, y cada cambio suyo es una fila del libro con su valor.
        valoracion.NoSeAudita(
            "la mueven en crudo las sentencias que anotan el libro, que no pasan por el rastreador " +
            "de cambios, y cada cambio suyo es una fila del libro con su valor, que ya es su traza");

        // LA CLAVE DE NEGOCIO ES LA PRIMARIA, y es la del `ON CONFLICT` que la bloquea. Ninguna de
        // las tres columnas admite nulos, así que no hace falta un identificador aparte como el de
        // la existencia, cuyo lote puede ser nulo.
        valoracion.HasKey(fila => new { fila.EmpresaId, fila.ArticuloId, fila.AlmacenId });

        // LA MISMA ESCALA QUE EL LIBRO, porque es su suma.
        valoracion.Property(fila => fila.Cantidad)
            .HasPrecision(18, MovimientoStock.DecimalesDeCantidad)
            .IsRequired();

        valoracion.Property(fila => fila.Divisa)
            .HasMaxLength(3)
            .IsRequired();

        valoracion.Ignore(fila => fila.Valor);
        valoracion.Ignore(fila => fila.Saldo);

        // LA ESCALA DEL IMPORTE (R6): el valor es la suma de importes ya redondeados, así que no
        // pierde nada. El precio medio no se guarda: se deduce de esto.
        valoracion.Property<decimal>("ValorSinDivisa")
            .HasColumnName("valor")
            .HasPrecision(18, Importe.Decimales)
            .IsRequired();
    }
}
