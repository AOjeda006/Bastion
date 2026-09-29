using Bastion.BuildingBlocks.Infrastructure.Auditoria;
using Bastion.BuildingBlocks.Infrastructure.Concurrencia;
using Bastion.BuildingBlocks.Infrastructure.Entidades;
using Bastion.Catalogo.Domain.Catalogo;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bastion.Catalogo.Infrastructure.Persistencia.Configuraciones;

internal sealed class ConfiguracionDeArticulo : IEntityTypeConfiguration<Articulo>
{
    /// <summary>La lista cerrada de la marca, en el motor.</summary>
    public const string TrazabilidadAdmitida = "ck_articulos_trazabilidad";

    /// <summary>Un servicio no lleva lote ni número de serie.</summary>
    public const string ServicioSinTrazabilidad = "ck_articulos_servicio_sin_trazabilidad";

    public void Configure(EntityTypeBuilder<Articulo> articulo)
    {
        ArgumentNullException.ThrowIfNull(articulo);

        // La lista cerrada del tipo, en el motor. La columna guarda el enumerado como texto, y sin
        // CHECK un `UPDATE` a mano podría dejar «bien» en minúscula o «Mercancia», y el valor
        // dejaría de convertirse al leerlo. Los nombres se sacan de `Enum.GetNames`, no copiados:
        // añadir un tercer tipo y olvidar el CHECK sería exactamente el fallo que este CHECK
        // existe para no tener.
        //
        // La marca de trazabilidad lleva la suya por lo mismo (ADR-0048 §1), y otra más: un
        // servicio no tiene existencias, así que no lleva lote ni serie. Lo impiden el dominio y el
        // caso de uso, y esta es la guarda que no se salta un `UPDATE` a mano.
        articulo.ToTable("articulos", tabla =>
        {
            tabla.HasCheckConstraint(
                "ck_articulos_tipo",
                "tipo IN ('" + string.Join("', '", Enum.GetNames<TipoDeArticulo>()) + "')");

            tabla.HasCheckConstraint(
                TrazabilidadAdmitida,
                "trazabilidad IN ('" + string.Join("', '", Enum.GetNames<Trazabilidad>()) + "')");

            tabla.HasCheckConstraint(
                ServicioSinTrazabilidad,
                "tipo <> '" + nameof(TipoDeArticulo.Servicio) + "'" +
                " OR trazabilidad = '" + nameof(Trazabilidad.Ninguna) + "'");
        });

        articulo.SeAudita();
        articulo.HasKey(fila => fila.Id);

        articulo.LlevaTestigoDeConcurrencia();

        ConfiguracionDeEntidadBase.Mapear(articulo);

        articulo.Property(fila => fila.EmpresaId).IsRequired().SeAudita();

        articulo.Property(fila => fila.Codigo)
            .HasMaxLength(Articulo.LongitudMaximaDeCodigo)
            .IsRequired()
            .SeAudita();

        articulo.Property(fila => fila.Descripcion)
            .HasMaxLength(Articulo.LongitudMaximaDeDescripcion)
            .IsRequired()
            .SeAudita();

        // Como TEXTO, igual que los demás enumerados del sistema: guardado por su valor entero
        // dejaría de significar nada en cuanto alguien reordenara el enumerado.
        articulo.Property(fila => fila.Tipo)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired()
            .SeAudita();

        // Igual que el tipo. Y se audita: cambiarla es de las pocas cosas de la ficha que decide
        // cómo se lee el libro de existencias.
        articulo.Property(fila => fila.Trazabilidad)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired()
            .SeAudita();

        // LOS DOS IDENTIFICADORES AJENOS, Y LO QUE AQUÍ NO HAY.
        //
        // No hay `HasOne(...).HasForeignKey(...)` hacia la unidad ni hacia el tramo de impuesto, y
        // no puede haberlo: los dos viven en `organizacion` y este esquema es `catalogo`. Ninguna
        // consulta cruza esquemas y no hay claves ajenas entre ellos (§5, regla 4). Lo que impide
        // que aquí acabe un `uuid` inventado son `IConsultaDeUnidadesDeMedida` e
        // `IConsultaDeImpuestos`, que `CrearArticulo` pregunta antes de construir la ficha
        // (ADR-0024). Que no exista ninguna clave ajena entre esquemas no se deja a la vista de
        // quien lea este fichero: lo barre `NingunaClaveForaneaCruzaDeEsquemaTests` sobre el
        // modelo entero de todos los módulos.
        articulo.Property(fila => fila.UnidadBaseId).IsRequired().SeAudita();
        articulo.Property(fila => fila.ImpuestoPorDefectoId).IsRequired().SeAudita();

        // La categoría SÍ es del mismo esquema, así que aquí sí hay clave ajena — y es la manera
        // de decir que un artículo no puede quedarse apuntando a una categoría que ya no está.
        // `Restrict` y no `Cascade`: borrar una categoría no puede llevarse por delante los
        // artículos que clasificaba. Como el módulo no publica ningún borrado de categoría, esto
        // es lo que queda para un `DELETE` a mano.
        articulo.Property(fila => fila.CategoriaId).SeAudita();

        articulo.HasOne<Categoria>()
            .WithMany()
            .HasForeignKey(fila => fila.CategoriaId)
            .OnDelete(DeleteBehavior.Restrict);

        // Un código, un artículo, DENTRO DE CADA EMPRESA (R8). Sin la empresa en el índice, la
        // segunda empresa que diera de alta su «TORN-8» chocaría contra el de la primera, que ni
        // siquiera puede ver.
        articulo.HasIndex(fila => new { fila.EmpresaId, fila.Codigo }).IsUnique();

        // El listado filtrado por categoría es el consumidor real que decidió el modelo del árbol,
        // así que su columna va indexada por empresa: sin esto, acotar por categoría es un
        // recorrido de la tabla de artículos de todas las empresas.
        articulo.HasIndex(fila => new { fila.EmpresaId, fila.CategoriaId });
    }
}
