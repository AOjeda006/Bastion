using Bastion.BuildingBlocks.Infrastructure.Auditoria;
using Bastion.BuildingBlocks.Infrastructure.Concurrencia;
using Bastion.BuildingBlocks.Infrastructure.Entidades;
using Bastion.Catalogo.Domain.Catalogo;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bastion.Catalogo.Infrastructure.Persistencia.Configuraciones;

internal sealed class ConfiguracionDeCategoria : IEntityTypeConfiguration<Categoria>
{
    public void Configure(EntityTypeBuilder<Categoria> categoria)
    {
        ArgumentNullException.ThrowIfNull(categoria);

        // El CHECK que sí cabe en el motor: una categoría no es su propio padre. Es el ciclo de
        // longitud uno, y es el único del que una restricción de fila puede ocuparse — los de
        // longitud dos en adelante necesitan mirar otras filas, o sea un disparador recursivo, y
        // por eso viven en `ElArbolSigueSiendoUnArbol` (R12, capa de aplicación).
        //
        // Puesto aquí ADEMÁS de allí, y no en lugar de allí: la comprobación de la capa de
        // aplicación es la que devuelve un error con nombre a quien usa la API; ésta es la que
        // queda cuando no hay proceso —un `UPDATE` a mano, una carga masiva, una restauración—.
        categoria.ToTable("categorias", tabla => tabla.HasCheckConstraint(
            "ck_categorias_padre_distinto_de_si_misma",
            "padre_id IS NULL OR padre_id <> id"));

        categoria.SeAudita();
        categoria.HasKey(fila => fila.Id);

        categoria.LlevaTestigoDeConcurrencia();

        ConfiguracionDeEntidadBase.Mapear(categoria);

        categoria.Property(fila => fila.EmpresaId).IsRequired().SeAudita();

        categoria.Property(fila => fila.Codigo)
            .HasMaxLength(Categoria.LongitudMaximaDeCodigo)
            .IsRequired()
            .SeAudita();

        categoria.Property(fila => fila.Nombre)
            .HasMaxLength(Categoria.LongitudMaximaDeNombre)
            .IsRequired()
            .SeAudita();

        categoria.Property(fila => fila.PadreId).SeAudita();

        // La clave ajena a sí misma: la lista de adyacencia. Es del MISMO esquema, así que aquí sí
        // la hay — la regla 4 prohíbe cruzar esquemas, no tener integridad referencial dentro del
        // propio. `Restrict` y no `Cascade`: borrar una rama no puede llevarse en silencio todo lo
        // que colgaba de ella.
        categoria.HasOne<Categoria>()
            .WithMany()
            .HasForeignKey(fila => fila.PadreId)
            .OnDelete(DeleteBehavior.Restrict);

        // Un código, una categoría, DENTRO DE CADA EMPRESA (R8).
        categoria.HasIndex(fila => new { fila.EmpresaId, fila.Codigo }).IsUnique();

        // El ascenso por los padres es el recorrido caliente de este módulo —una consulta por
        // nivel, en cada alta y en cada modificación con padre—, y va por clave primaria. Este
        // índice es para el otro sentido: pintar el árbol pidiendo los hijos de cada nodo.
        categoria.HasIndex(fila => new { fila.EmpresaId, fila.PadreId });
    }
}
