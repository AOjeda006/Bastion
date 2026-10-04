using Bastion.BuildingBlocks.Infrastructure.Auditoria;
using Bastion.Inventario.Domain.Existencias;
using Bastion.Inventario.Domain.LotesYSeries;
using Bastion.Inventario.Domain.Movimientos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bastion.Inventario.Infrastructure.Persistencia.Configuraciones;

internal sealed class ConfiguracionDeExistencia : IEntityTypeConfiguration<Existencia>
{
    /// <summary>La tabla, nombrada aquí porque la sentencia que anota el libro la escribe en crudo.</summary>
    internal const string Tabla = "existencias";

    /// <summary>
    /// El índice que deja una fila por clave, con el lote y el número de serie nulos contando como un valor.
    /// </summary>
    internal const string IndiceDeLaClave = "ix_existencias_una_por_clave";

    /// <summary>El número de serie, como mucho en un sitio: el índice único parcial del ADR-0048 §3.</summary>
    internal const string NumeroDeSerieEnUnSitio = "ix_existencias_numero_de_serie_en_un_sitio";

    /// <summary>
    /// El número de serie, como mucho una unidad en su fila: el <c>CHECK</c> del ADR-0048 §3.
    /// </summary>
    internal const string NumeroDeSerieComoMuchoUna = "ck_existencias_numero_de_serie_como_mucho_una";

    /// <summary>
    /// El lote o el número de serie, nunca los dos: el <c>CHECK</c> del ADR-0050 §3. No se traduce.
    /// </summary>
    internal const string LoteONumeroDeSerie = "ck_existencias_lote_o_numero_de_serie";

    /// <summary>La resta del disponible, dicha en SQL.</summary>
    internal const string Disponible = "fisico - reservado";

    /// <summary>La restricción que impide el stock negativo, que el borde traduce por su nombre.</summary>
    internal const string FisicoNoNegativo = "ck_existencias_fisico_no_negativo";

    /// <summary>Lo que vuela no baja de cero (ADR-0053 §1). No se traduce: si salta, es un defecto.</summary>
    internal const string EnTransitoNoNegativo = "ck_existencias_en_transito_no_negativo";

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
        //
        // EL NÚMERO DE SERIE, COMO MUCHO UNA UNIDAD (ADR-0048 §3, con el nombre del ADR-0050). Solo
        // por arriba: el límite de abajo ya lo pone la del stock, y repetirlo haría saltar las dos con
        // un −1. PostgreSQL las comprueba en orden alfabético de nombre, así que el error que recibe
        // el usuario dependería de cómo se llaman.
        //
        // EL LOTE O EL NÚMERO DE SERIE, NUNCA LOS DOS, y en el libro igual (ADR-0050 §3). Lo decide
        // el dominio, y esto guarda los caminos que no pasan por él: una restauración, un `INSERT`
        // a mano, un documento nuevo que se olvide de preguntar. No se traduce: si salta, es un
        // defecto, y sale un `500`. Va entre las otras dos por orden alfabético y no cambia ningún
        // error, porque la fila nace a cero antes de sumarle nada. El día que un artículo necesite
        // las dos marcas, la migración borra este `CHECK`, que no toca la clave.
        //
        // LO QUE VUELA TAMPOCO BAJA DE CERO (ADR-0053 §1), y no se traduce: una recepción o un
        // inverso restan lo que el envío sumó, y el testigo del documento impide que lo resten dos.
        // Y UNA SERIE QUE VUELA SIGUE CONTANDO COMO SU UNIDAD (§7): el `CHECK` de la serie suma el
        // tránsito al físico, así que la misma fila no puede tenerla en la estantería y en camino.
        existencia.ToTable(
            Tabla,
            tabla =>
            {
                tabla.HasCheckConstraint(FisicoNoNegativo, "fisico >= 0");
                tabla.HasCheckConstraint(EnTransitoNoNegativo, "en_transito >= 0");
                tabla.HasCheckConstraint(
                    NumeroDeSerieComoMuchoUna, "numero_de_serie_id IS NULL OR fisico + en_transito <= 1");
                tabla.HasCheckConstraint(
                    LoteONumeroDeSerie, ConfiguracionDeMovimientoStock.LoteONumeroDeSerie);
            });

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

        // EL LOTE Y EL NÚMERO DE SERIE, EN DOS COLUMNAS Y CON CLAVE AJENA (ADR-0048 §1 y §2). Dentro
        // del esquema, así que la clave ajena no cruza ninguna frontera; y las filas de lote y de
        // número de serie no se borran nunca, así que `Restrict` no le quita nada a nadie.
        existencia.Property(fila => fila.LoteId);
        existencia.Property(fila => fila.NumeroDeSerieId);

        existencia.HasOne<Lote>()
            .WithMany()
            .HasForeignKey(fila => fila.LoteId)
            .OnDelete(DeleteBehavior.Restrict);

        existencia.HasOne<NumeroDeSerie>()
            .WithMany()
            .HasForeignKey(fila => fila.NumeroDeSerieId)
            .OnDelete(DeleteBehavior.Restrict);

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

        // LO QUE VUELA HACIA ESTA FILA (ADR-0053 §1), sin valor por omisión por lo mismo que lo
        // reservado. Fuera del disponible, que sigue siendo lo que hay en la estantería: lo que no
        // ha llegado no se puede comprometer.
        existencia.Property(fila => fila.EnTransito)
            .HasPrecision(18, MovimientoStock.DecimalesDeCantidad)
            .IsRequired();

        // EL DISPONIBLE LO CALCULA EL MOTOR, guardado y no al vuelo. Así vale igual para el ORM, para
        // una consulta cruda y para un informe, y no se puede escribir por error: PostgreSQL rechaza
        // un `INSERT` o un `UPDATE` que lo nombre.
        existencia.Property(fila => fila.Disponible)
            .HasPrecision(18, MovimientoStock.DecimalesDeCantidad)
            .HasComputedColumnSql(Disponible, stored: true);

        // UNA FILA POR CLAVE, CON EL LOTE Y EL NÚMERO DE SERIE NULOS COMO UN VALOR MÁS. Sin `NULLS NOT
        // DISTINCT`, dos filas con el lote nulo no chocarían nunca, y el `ON CONFLICT` de la sentencia
        // que anota el libro no encontraría nunca la fila de antes: cada movimiento crearía una nueva.
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
            fila.NumeroDeSerieId,
        })
            .IsUnique()
            .AreNullsDistinct(false)
            .HasDatabaseName(IndiceDeLaClave);

        // UN NÚMERO DE SERIE NO ESTÁ EN DOS SITIOS (ADR-0048 §3), y un `CHECK` de fila no ve las demás
        // filas. Solo cuentan las filas con existencias: la unidad que salió deja su fila a cero, y la
        // siguiente entrada puede ser en otra estantería. El `numero_de_serie_id IS NOT NULL` no
        // cambia lo que guarda -con los nulos distintos, dos filas sin número de serie no chocarían-
        // y deja fuera del índice todo el stock que no es por número de serie, que es casi todo.
        //
        // Se comprueba fila a fila y no al final de la sentencia, y por eso una serie sale una sola
        // vez por documento: moverla de una estantería a otra en el mismo `UPDATE` chocaría o no
        // según el orden en que el motor recorriera las filas.
        //
        // LA FILA QUE LA ESPERA TAMBIÉN CUENTA (ADR-0053 §7): mientras vuela, la serie está en la
        // existencia del destino, y un ajuste que la diera de alta en otro sitio chocaría aquí. Por
        // eso las patas van de restar a sumar: la salida del origen deja su fila a cero antes de que
        // el tránsito entre en el índice.
        existencia.HasIndex(fila => new { fila.EmpresaId, fila.ArticuloId, fila.NumeroDeSerieId })
            .IsUnique()
            .HasFilter("(fisico > 0 OR en_transito > 0) AND numero_de_serie_id IS NOT NULL")
            .HasDatabaseName(NumeroDeSerieEnUnSitio);
    }
}
