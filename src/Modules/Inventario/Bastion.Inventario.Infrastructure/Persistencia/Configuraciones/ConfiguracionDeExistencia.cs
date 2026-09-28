using Bastion.BuildingBlocks.Infrastructure.Auditoria;
using Bastion.Inventario.Domain.Existencias;
using Bastion.Inventario.Domain.Movimientos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bastion.Inventario.Infrastructure.Persistencia.Configuraciones;

internal sealed class ConfiguracionDeExistencia : IEntityTypeConfiguration<Existencia>
{
    /// <summary>La tabla, nombrada aquí porque la sentencia que anota el libro la escribe en crudo.</summary>
    internal const string Tabla = "existencias";

    /// <summary>El índice que deja una fila por clave, con el lote nulo contando como un valor.</summary>
    internal const string IndiceDeLaClave = "ix_existencias_una_por_clave";

    /// <summary>La resta del disponible, dicha en SQL.</summary>
    internal const string Disponible = "fisico - reservado";

    /// <summary>La restricción que impide el stock negativo, que el borde traduce por su nombre.</summary>
    internal const string FisicoNoNegativo = "ck_existencias_fisico_no_negativo";

    public void Configure(EntityTypeBuilder<Existencia> existencia)
    {
        ArgumentNullException.ThrowIfNull(existencia);

        // EL STOCK NO BAJA DE CERO, Y LO GUARDA EL MOTOR (ADR-0046 §4). Sobre la fila viva, que
        // es por ubicación: sacar de una estantería vacía lo que está en la de al lado también se
        // rechaza, que es lo que pasa en el almacén. No hay comprobación previa en el dominio,
        // porque dos salidas simultáneas la pasarían juntas; esto se evalúa con la fila ya
        // bloqueada y la cantidad ya sumada. El nombre es contrato con el borde, que lo traduce a
        // `422` `stock-insuficiente`, y la declaración está en `ModuloDeInventario`.
        //
        // Las instantáneas NO llevan la suya: un movimiento con fecha atrasada puede dejar un mes
        // pasado por debajo de cero sin que el saldo de hoy lo esté, y eso ya lo admitía el 2.7.
        existencia.ToTable(
            Tabla,
            tabla => tabla.HasCheckConstraint(FisicoNoNegativo, "fisico >= 0"));

        // NO SE AUDITA, y por el mismo motivo que el contador de una serie: la escribe una sentencia
        // cruda, que no pasa por el rastreador de cambios. Un `SeAudita()` prometería una traza que
        // el mecanismo no puede escribir. Y aunque pudiera, no diría nada nuevo: cada cambio de esta
        // fila es una fila del libro, que ya dice quién, cuándo y contra qué documento.
        existencia.NoSeAudita(
            "la mueve en crudo la sentencia que anota el libro, que no pasa por el rastreador de " +
            "cambios, y cada cambio suyo es una fila del libro, que ya es su traza");

        existencia.HasKey(fila => fila.Id);

        existencia.Property(fila => fila.EmpresaId).IsRequired();
        existencia.Property(fila => fila.ArticuloId).IsRequired();
        existencia.Property(fila => fila.AlmacenId).IsRequired();
        existencia.Property(fila => fila.UbicacionId).IsRequired();

        // Nullable y SIN clave ajena: el lote lo trae el 2.9, con su tabla.
        existencia.Property(fila => fila.LoteId);

        // LA MISMA ESCALA QUE EL LIBRO, porque es su suma: con menos decimales, la copia diría otra
        // cosa que el original en cuanto una conversión dejara el sexto.
        existencia.Property(fila => fila.Fisico)
            .HasPrecision(18, MovimientoStock.DecimalesDeCantidad)
            .IsRequired();

        // SIN valor por omisión: la sentencia que crea la fila escribe el cero ella misma. Un
        // `DEFAULT` sería otra cosa que genera el servidor, y el censo de lo que genera el servidor
        // lo cuenta todo.
        existencia.Property(fila => fila.Reservado)
            .HasPrecision(18, MovimientoStock.DecimalesDeCantidad)
            .IsRequired();

        // EL DISPONIBLE LO CALCULA EL MOTOR, guardado y no al vuelo. Así vale igual para el ORM, para
        // una consulta cruda y para un informe, y no se puede escribir por error: PostgreSQL rechaza
        // un `INSERT` o un `UPDATE` que lo nombre.
        existencia.Property(fila => fila.Disponible)
            .HasPrecision(18, MovimientoStock.DecimalesDeCantidad)
            .HasComputedColumnSql(Disponible, stored: true);

        // UNA FILA POR CLAVE, CON EL LOTE NULO COMO UN VALOR MÁS. Sin `NULLS NOT DISTINCT`, dos filas
        // con el lote nulo no chocarían nunca, y el `ON CONFLICT` de la sentencia que anota el libro
        // no encontraría nunca la fila de antes: cada movimiento crearía una nueva.
        //
        // El orden de las columnas es el de la pregunta más frecuente: qué hay de un artículo en una
        // empresa.
        existencia.HasIndex(fila => new
        {
            fila.EmpresaId,
            fila.ArticuloId,
            fila.AlmacenId,
            fila.UbicacionId,
            fila.LoteId,
        })
            .IsUnique()
            .AreNullsDistinct(false)
            .HasDatabaseName(IndiceDeLaClave);
    }
}
