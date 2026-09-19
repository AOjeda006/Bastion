using System.Globalization;
using Bastion.BuildingBlocks.Domain.Dinero;
using Bastion.BuildingBlocks.Infrastructure.Auditoria;
using Bastion.Inventario.Domain.Movimientos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bastion.Inventario.Infrastructure.Persistencia.Configuraciones;

internal sealed class ConfiguracionDeMovimientoStock : IEntityTypeConfiguration<MovimientoStock>
{
    /// <summary>La igualdad de las tres cantidades, dicha en SQL.</summary>
    /// <remarks>
    /// El <c>round</c> de PostgreSQL sobre <c>numeric</c> redondea alejándose del cero, que es el
    /// mismo modo que impone <c>MovimientoStock.EnUnidadBase</c>. Si alguno de los dos cambiara de
    /// modo, esta igualdad empezaría a fallar en el medio punto — que es lo que se quiere.
    /// </remarks>
    internal static readonly string CantidadPorFactor =
        "cantidad_en_unidad_base = round(cantidad_introducida * factor_a_unidad_base, " +
        MovimientoStock.DecimalesDeCantidad.ToString(CultureInfo.InvariantCulture) + ")";

    /// <summary>Ni la cantidad introducida ni la base pueden ser cero.</summary>
    internal const string CantidadNoNula =
        "cantidad_introducida <> 0 AND cantidad_en_unidad_base <> 0";

    public void Configure(EntityTypeBuilder<MovimientoStock> movimiento)
    {
        ArgumentNullException.ThrowIfNull(movimiento);

        // LAS DOS RESTRICCIONES DEL LIBRO, EN EL MOTOR.
        //
        // La primera es la regla que une las tres cantidades. Está también en el dominio, y las
        // dos hacen falta: el dominio protege lo que pasa por su fábrica, y a esta tabla se llega
        // además desde una restauración de copia y desde cualquier `INSERT` a mano. La segunda
        // rechaza la fila que no mueve nada: una línea de cero deja constancia de un movimiento
        // que no ocurrió y la suma no la distingue de no haberla escrito.
        movimiento.ToTable(
            "movimiento_stock",
            tabla =>
            {
                tabla.HasCheckConstraint("ck_movimiento_stock_cantidad_por_factor", CantidadPorFactor);
                tabla.HasCheckConstraint("ck_movimiento_stock_cantidad_no_nula", CantidadNoNula);
            });

        // EL LIBRO NO SE AUDITA, Y ESTE ES EL SITIO DONDE HAY QUE DECIRLO.
        //
        // Por R2 la fila no cambia nunca —lo sostienen los disparadores de solo añadido—, así que
        // auditar sus cambios duplicaría la tabla más grande del sistema para registrar cero
        // cambios. El libro es su propia traza: cada fila dice quién, cuándo, qué y contra qué
        // documento. Lo que sí se audita es el DOCUMENTO, que es donde hay decisiones de una
        // persona.
        movimiento.NoSeAudita(
            "por R2 no cambia nunca, y cada fila ya dice quién, cuándo, qué y contra qué documento: " +
            "la traza de esta tabla registraría cero cambios y pesaría lo que la tabla");

        // LA CLAVE PRIMARIA LLEVA LA CLAVE DE PARTICIÓN, y no es una decisión de diseño: PostgreSQL
        // exige que toda restricción única de una tabla particionada contenga la clave de
        // partición. Sin la fecha dentro, el `CREATE TABLE … PARTITION BY RANGE` no se acepta.
        movimiento.HasKey(fila => new { fila.Id, fila.FechaDeOperacion });

        movimiento.Property(fila => fila.EmpresaId).IsRequired();

        // `date`, no `timestamptz`: es una fecha de NEGOCIO (R14). Y además es la clave de
        // partición, así que el tipo decide en qué partición cae cada fila.
        movimiento.Property(fila => fila.FechaDeOperacion).IsRequired();

        movimiento.Property(fila => fila.AlmacenId).IsRequired();
        movimiento.Property(fila => fila.UbicacionId).IsRequired();
        movimiento.Property(fila => fila.ArticuloId).IsRequired();
        movimiento.Property(fila => fila.UnidadIntroducidaId).IsRequired();

        // Las tres cantidades a la misma escala. `numeric(18,6)`: nunca coma flotante —0,1 kg tres
        // veces no son 0,3 kg— y nunca el `money` del motor, que lleva pegada una configuración
        // regional.
        movimiento.Property(fila => fila.CantidadEnUnidadBase)
            .HasPrecision(18, MovimientoStock.DecimalesDeCantidad)
            .IsRequired();

        movimiento.Property(fila => fila.CantidadIntroducida)
            .HasPrecision(18, MovimientoStock.DecimalesDeCantidad)
            .IsRequired();

        movimiento.Property(fila => fila.FactorAUnidadBase)
            .HasPrecision(18, MovimientoStock.DecimalesDeCantidad)
            .IsRequired();

        // EL COSTE: DOS COLUMNAS, Y LA DIVISA ES UNA DE ELLAS (R6). Obligatorio, a diferencia del
        // límite de crédito de un tercero: un movimiento sin coste no se puede valorar, y valorar
        // existencias es la mitad de para qué existe el libro.
        movimiento.ComplexProperty(fila => fila.CosteUnitario, coste =>
        {
            coste.IsRequired();

            coste.Property(campo => campo.Cantidad)
                .HasColumnName("coste_unitario_cantidad")
                .HasPrecision(18, Importe.Decimales);

            coste.Property(campo => campo.Divisa)
                .HasColumnName("coste_unitario_divisa")
                .HasMaxLength(3);
        });

        // Como TEXTO, igual que los demás enumerados del proyecto: un entero en la base obliga a
        // tener el código delante para leer una fila.
        movimiento.Property(fila => fila.DocumentoOrigenTipo)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        movimiento.Property(fila => fila.DocumentoOrigenId).IsRequired();

        // LAS DOS MARCAS A MANO, Y NO CON `ConfiguracionDeEntidadBase.Mapear`.
        //
        // El ayudante común marca `CreadoEn` como auditada, y esta entidad NO se audita: una marca
        // de auditoría sobre una entidad que no se audita es una marca huérfana —dice «esto va a
        // la traza» sobre algo que no va— y `CadaEntidadDeclaraSuAuditoriaTests` la pone roja por
        // su nombre. Así que las dos columnas se mapean aquí, con el mismo tipo y sin clasificar.
        //
        // `ModificadoEn` nace valiendo lo mismo que `CreadoEn` y no se mueve nunca, porque nada
        // actualiza esta tabla. Se queda para no inventarle una forma distinta a una entidad que
        // por lo demás es como todas.
        movimiento.Property(fila => fila.CreadoEn).IsRequired();
        movimiento.Property(fila => fila.ModificadoEn).IsRequired();

        // R13, la mitad «documento → sus movimientos»: sin este índice, pedirle a un ajuste sus
        // líneas recorrería el libro entero, que es la tabla que más crece del sistema.
        movimiento.HasIndex(fila => new { fila.DocumentoOrigenTipo, fila.DocumentoOrigenId });

        // El saldo del 2.7 se pregunta por artículo dentro de una ubicación, y siempre dentro de
        // una empresa (R8). La fecha va al final porque es la clave de partición: el motor ya
        // descarta particiones enteras antes de mirar el índice.
        movimiento.HasIndex(fila => new
        {
            fila.EmpresaId,
            fila.ArticuloId,
            fila.UbicacionId,
            fila.FechaDeOperacion,
        });
    }
}
