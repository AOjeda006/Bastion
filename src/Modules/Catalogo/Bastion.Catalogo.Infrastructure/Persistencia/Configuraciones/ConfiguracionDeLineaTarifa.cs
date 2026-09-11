using Bastion.BuildingBlocks.Domain.Dinero;
using Bastion.BuildingBlocks.Infrastructure.Auditoria;
using Bastion.BuildingBlocks.Infrastructure.Concurrencia;
using Bastion.BuildingBlocks.Infrastructure.Entidades;
using Bastion.Catalogo.Domain.Catalogo;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bastion.Catalogo.Infrastructure.Persistencia.Configuraciones;

/// <summary>
/// Mapeo de la línea de tarifa.
/// </summary>
/// <remarks>
/// <para>
/// <b>Las claves ajenas a <c>articulos</c> y a <c>categorias</c> son del MISMO esquema y son
/// legítimas.</b> Se dice así, con estas palabras, para que no se lean como un descuido frente a la
/// regla 4 del §5: lo que esa regla prohíbe es cruzar de esquema —de <c>catalogo</c> a
/// <c>organizacion</c>—, no tener integridad referencial dentro del propio. Tarifa y LineaTarifa
/// viven en Catálogo precisamente porque sus dos destinos viven aquí; ponerlas en otro módulo
/// habría convertido estas dos claves ajenas legítimas en dos columnas sueltas con un puerto
/// detrás, que es más máquina para menos garantía.
/// </para>
/// <para>
/// <b>Los dos CHECK de exclusividad valen los dos casos, no uno.</b> Cada uno está escrito como
/// «uno no nulo y el otro nulo, o al revés», así que rechaza los dos puestos <b>y</b> ninguno
/// puesto. La segunda mitad es la que se olvida y es la que produce el precio cero por la puerta de
/// atrás: una fila sin precio y sin descuento casa con el artículo, gana la precedencia y devuelve
/// un importe que nadie escribió.
/// </para>
/// </remarks>
internal sealed class ConfiguracionDeLineaTarifa : IEntityTypeConfiguration<LineaTarifa>
{
    public void Configure(EntityTypeBuilder<LineaTarifa> linea)
    {
        ArgumentNullException.ThrowIfNull(linea);

        linea.ToTable("lineas_tarifa", tabla =>
        {
            tabla.HasCheckConstraint(
                "ck_lineas_tarifa_articulo_o_categoria",
                "(articulo_id IS NOT NULL AND categoria_id IS NULL) " +
                "OR (articulo_id IS NULL AND categoria_id IS NOT NULL)");

            tabla.HasCheckConstraint(
                "ck_lineas_tarifa_precio_o_descuento",
                "(precio IS NOT NULL AND descuento_porcentaje IS NULL) " +
                "OR (precio IS NULL AND descuento_porcentaje IS NOT NULL)");

            // El cero SÍ es un precio válido —una muestra comercial se factura a cero a propósito—,
            // así que lo que se prohíbe es el negativo y no el cero.
            tabla.HasCheckConstraint(
                "ck_lineas_tarifa_precio_no_negativo",
                "precio IS NULL OR precio >= 0");

            tabla.HasCheckConstraint(
                "ck_lineas_tarifa_descuento_en_rango",
                "descuento_porcentaje IS NULL " +
                "OR (descuento_porcentaje >= 0 AND descuento_porcentaje <= 100)");

            // El tramo empieza en cero o más arriba. Que el PRIMER tramo de cada destino empiece
            // exactamente en cero no cabe en una restricción de fila —hay que mirar las demás filas
            // del destino— y por eso vive en `CrearLineaTarifa`.
            tabla.HasCheckConstraint(
                "ck_lineas_tarifa_cantidad_desde_no_negativa",
                "cantidad_desde >= 0");
        });

        linea.SeAudita();
        linea.HasKey(fila => fila.Id);

        linea.LlevaTestigoDeConcurrencia();

        ConfiguracionDeEntidadBase.Mapear(linea);

        // Columna propia aunque la tarifa ya la tenga, como en `Ubicacion` respecto de su almacén:
        // el filtro de la R8 se escribe por entidad y se evalúa sobre las columnas de la fila. Sin
        // ella habría que salir a buscar la tarifa en cada lectura, y bastaría una consulta que
        // empezara por las líneas para que salieran las de otra empresa.
        linea.Property(fila => fila.EmpresaId).IsRequired().SeAudita();

        linea.Property(fila => fila.TarifaId).IsRequired().SeAudita();
        linea.Property(fila => fila.ArticuloId).SeAudita();
        linea.Property(fila => fila.CategoriaId).SeAudita();

        // La escala de CANTIDAD, no la de dinero: es cuántas unidades hay que llevarse para entrar
        // en el tramo. Seis decimales, como el precio unitario, porque hay unidades que se venden
        // fraccionadas —metros, kilos, horas— y un tramo que empezara en «10» sobre una columna
        // entera redondearía la frontera sin decirlo.
        linea.Property(fila => fila.CantidadDesde)
            .HasPrecision(18, PrecioUnitario.Decimales)
            .IsRequired()
            .SeAudita();

        // EL OBJETO DE VALOR, COMO TIPO COMPLEJO Y NO COMO TIPO POSEÍDO. Es la lección del 0.10 y
        // del ADR-0016: un tipo poseído tiene identidad sintetizada, sale en `GetEntityTypes()` y
        // el modelo acabaría diciendo del precio algo que el dominio niega. Dos columnas en esta
        // misma tabla, y las dos nulables por separado — nunca las dos a la vez, que es lo que
        // sostiene el CHECK de arriba.
        linea.ComplexProperty(fila => fila.PrecioODescuento, precio =>
        {
            precio.Property(campo => campo.Precio)
                .HasColumnName("precio")
                .HasPrecision(18, PrecioUnitario.Decimales)
                .SeAudita();

            precio.Property(campo => campo.DescuentoPorcentaje)
                .HasColumnName("descuento_porcentaje")
                .HasPrecision(5, PrecioODescuento.DecimalesDelDescuento)
                .SeAudita();
        });

        // Del mismo esquema, así que clave ajena de verdad. `Restrict` y no `Cascade`, como en todo
        // el módulo: borrar no puede llevarse por delante en silencio lo que colgaba.
        linea.HasOne<Tarifa>()
            .WithMany()
            .HasForeignKey(fila => fila.TarifaId)
            .OnDelete(DeleteBehavior.Restrict);

        linea.HasOne<Articulo>()
            .WithMany()
            .HasForeignKey(fila => fila.ArticuloId)
            .OnDelete(DeleteBehavior.Restrict);

        linea.HasOne<Categoria>()
            .WithMany()
            .HasForeignKey(fila => fila.CategoriaId)
            .OnDelete(DeleteBehavior.Restrict);

        // LOS DOS ÍNDICES QUE HACEN IMPOSIBLE EL SOLAPE DE TRAMOS, uno por clase de destino.
        //
        // Son dos y parciales, y no uno sobre las cuatro columnas, porque en PostgreSQL los nulos
        // de un índice único son DISTINTOS entre sí: un único índice sobre
        // (tarifa, articulo, categoria, cantidad) dejaría entrar dos líneas de la misma categoría y
        // la misma cantidad, porque su `articulo_id` nulo no choca con el otro nulo. El síntoma no
        // sería un error: serían dos tramos que empiezan en el mismo sitio y un precio que cambia
        // según el orden en que salgan las filas.
        //
        // Son además los índices del camino caliente: la resolución de un precio busca por
        // (tarifa, artículo) y por (tarifa, categoría de la ascendencia).
        linea.HasIndex(fila => new { fila.TarifaId, fila.ArticuloId, fila.CantidadDesde })
            .IsUnique()
            .HasFilter("articulo_id IS NOT NULL");

        linea.HasIndex(fila => new { fila.TarifaId, fila.CategoriaId, fila.CantidadDesde })
            .IsUnique()
            .HasFilter("categoria_id IS NOT NULL");

        // El listado de las líneas de una tarifa, por empresa (R8).
        linea.HasIndex(fila => new { fila.EmpresaId, fila.TarifaId });
    }
}
