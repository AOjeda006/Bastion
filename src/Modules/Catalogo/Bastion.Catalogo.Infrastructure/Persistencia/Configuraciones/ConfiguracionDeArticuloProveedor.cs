using Bastion.BuildingBlocks.Infrastructure.Auditoria;
using Bastion.BuildingBlocks.Infrastructure.Concurrencia;
using Bastion.BuildingBlocks.Infrastructure.Entidades;
using Bastion.Catalogo.Domain.Catalogo;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bastion.Catalogo.Infrastructure.Persistencia.Configuraciones;

/// <summary>
/// Mapeo del suministro: qué tercero provee qué artículo.
/// </summary>
/// <remarks>
/// <para>
/// <b>Clave ajena a <c>articulos</c> sí; a <c>terceros.terceros</c> NO, y la asimetría es la regla
/// 4 del §5.</b> El artículo vive en este esquema y por eso su integridad la sostiene el motor. El
/// tercero vive en <c>terceros</c>, y una clave ajena entre esquemas ataría los dos módulos por
/// debajo: el orden de las migraciones pasaría a importar, un módulo no se podría desplegar sin el
/// otro, y la frontera que el compilador vigila arriba quedaría abierta por abajo. Lo que ocupa su
/// sitio es <c>IConsultaDeTerceros</c>, preguntado antes de escribir (ADR-0024).
/// </para>
/// <para>
/// <b>Y el índice único es por (empresa, artículo, tercero).</b> Un proveedor suministra un
/// artículo o no lo suministra; dos filas iguales no dicen nada más y convertirían el listado en
/// una lista con repetidos que nadie sabría desempatar. La empresa va delante porque es el filtro
/// que toda consulta lleva (R8) y porque el mismo tercero puede suministrar el mismo artículo en
/// dos empresas distintas de la instalación, que son dos hechos distintos.
/// </para>
/// </remarks>
internal sealed class ConfiguracionDeArticuloProveedor : IEntityTypeConfiguration<ArticuloProveedor>
{
    public void Configure(EntityTypeBuilder<ArticuloProveedor> suministro)
    {
        ArgumentNullException.ThrowIfNull(suministro);

        suministro.ToTable("articulos_proveedor");

        suministro.SeAudita();
        suministro.HasKey(fila => fila.Id);

        suministro.LlevaTestigoDeConcurrencia();

        ConfiguracionDeEntidadBase.Mapear(suministro);

        // Columna propia, como en la línea de tarifa: el filtro de la R8 se evalúa sobre las
        // columnas de la fila, y sin ella una consulta que empezara por aquí traería las de otra
        // empresa.
        suministro.Property(fila => fila.EmpresaId).IsRequired().SeAudita();

        suministro.Property(fila => fila.ArticuloId).IsRequired().SeAudita();

        // El identificador ajeno, declarado en el inventario de arquitectura con su puerto. Sin
        // clave ajena: cruza esquemas.
        suministro.Property(fila => fila.TerceroId).IsRequired().SeAudita();

        suministro.Property(fila => fila.ReferenciaDelProveedor)
            .HasMaxLength(ArticuloProveedor.LongitudMaximaDeReferencia)
            .SeAudita();

        // Del mismo esquema, así que clave ajena de verdad. `Restrict` y no `Cascade`, como en todo
        // el módulo: borrar no puede llevarse por delante en silencio lo que colgaba.
        suministro.HasOne<Articulo>()
            .WithMany()
            .HasForeignKey(fila => fila.ArticuloId)
            .OnDelete(DeleteBehavior.Restrict);

        suministro.HasIndex(fila => new { fila.EmpresaId, fila.ArticuloId, fila.TerceroId })
            .IsUnique();

        // El camino de vuelta: qué artículos suministra un tercero. Lo pide el día que se le da de
        // baja —para saber qué queda colgando— y lo pide la compra de la fase 3.
        suministro.HasIndex(fila => new { fila.EmpresaId, fila.TerceroId });
    }
}
