using Bastion.BuildingBlocks.Infrastructure.Auditoria;
using Bastion.BuildingBlocks.Infrastructure.Concurrencia;
using Bastion.BuildingBlocks.Infrastructure.Entidades;
using Bastion.Catalogo.Domain.Catalogo;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bastion.Catalogo.Infrastructure.Persistencia.Configuraciones;

/// <summary>
/// Mapeo del tramo de tarifa.
/// </summary>
/// <remarks>
/// <b>Lo que NO está aquí es la restricción de exclusión</b>, y hay que saber dónde mirarla: el
/// <c>EXCLUDE USING gist</c> que impide que dos tramos del mismo código se pisen no tiene forma en
/// el modelo de EF Core, así que va escrito en la migración —igual que el de los tramos de impuesto
/// del 0.15— junto con la extensión <c>btree_gist</c> que necesita. Que la extensión se cree en una
/// migración y no a mano en la base de desarrollo es lo que hace que la imagen del humo la tenga.
/// Desde el 3.3 hay una segunda, <c>tarifas_un_tipo_por_codigo</c>, en la migración que trajo el
/// tipo, y por lo mismo.
/// </remarks>
internal sealed class ConfiguracionDeTarifa : IEntityTypeConfiguration<Tarifa>
{
    public void Configure(EntityTypeBuilder<Tarifa> tarifa)
    {
        ArgumentNullException.ThrowIfNull(tarifa);

        // El CHECK que sí cabe en una restricción de fila: un tramo no deja de regir antes de
        // empezar. Puesto aquí ADEMÁS de en `Tarifa.Crear` y en el caso de uso, por lo mismo que el
        // del padre de la categoría: la comprobación de la aplicación devuelve un error con nombre
        // a quien usa la API, y ésta es la que queda cuando no hay proceso delante.
        //
        // Y el tipo, con su lista cerrada, como el del artículo: un tercer tipo que se añadiera sin
        // tocar el CHECK es justo el fallo que el CHECK existe para no tener.
        tarifa.ToTable("tarifas", tabla =>
        {
            tabla.HasCheckConstraint(
                "ck_tarifas_vigencia_no_invertida",
                "vigente_hasta IS NULL OR vigente_hasta >= vigente_desde");

            tabla.HasCheckConstraint(
                "ck_tarifas_tipo",
                "tipo IN ('" + string.Join("', '", Enum.GetNames<TipoDeTarifa>()) + "')");
        });

        tarifa.SeAudita();
        tarifa.HasKey(fila => fila.Id);

        tarifa.LlevaTestigoDeConcurrencia();

        ConfiguracionDeEntidadBase.Mapear(tarifa);

        tarifa.Property(fila => fila.EmpresaId).IsRequired().SeAudita();

        tarifa.Property(fila => fila.Codigo)
            .HasMaxLength(Tarifa.LongitudMaximaDeCodigo)
            .IsRequired()
            .SeAudita();

        tarifa.Property(fila => fila.Nombre)
            .HasMaxLength(Tarifa.LongitudMaximaDeNombre)
            .IsRequired()
            .SeAudita();

        // Como TEXTO, igual que los demás enumerados del sistema.
        tarifa.Property(fila => fila.Tipo)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired()
            .SeAudita();

        // LA DIVISA, CON SU CÓDIGO ISO 4217 desde el 3.3 (ADR-0076), como el límite de crédito del
        // tercero y la divisa base de la empresa: tres letras, sin clave ajena y sin poder tenerla,
        // porque la divisa vive en `organizacion` y este esquema es `catalogo` (§5, regla 4). Lo
        // que impide que aquí acabe un código que Organización no tiene es `IConsultaDeDivisas`,
        // que `CrearTarifa` pregunta antes de construir la fila (ADR-0024).
        tarifa.Property(fila => fila.Divisa)
            .HasMaxLength(3)
            .IsRequired()
            .SeAudita();

        // `date` y no `timestamptz`, que es lo que hace `DateOnly` con Npgsql. La vigencia de una
        // tarifa es una fecha de NEGOCIO (R14): tiene día pero no hora ni zona, y guardarla como
        // instante convertiría «desde el 1 de enero» en algo que empieza a una hora distinta según
        // dónde esté quien lo mira. Es además el tipo sobre el que se construye el `daterange` de
        // la restricción de exclusión: con `timestamptz` el rango sería de instantes y el día de la
        // frontera dependería del huso.
        tarifa.Property(fila => fila.VigenteDesde).IsRequired().SeAudita();
        tarifa.Property(fila => fila.VigenteHasta).SeAudita();

        // El código NO es único: una tarifa son varios tramos con el mismo código. Lo que no puede
        // haber es solape, y eso lo impide la restricción de exclusión de la migración. Este índice
        // es el de las dos consultas que de verdad se hacen: «el tramo de PVP que rige el día D» y
        // «todos los tramos de PVP», las dos por empresa (R8).
        tarifa.HasIndex(fila => new { fila.EmpresaId, fila.Codigo, fila.VigenteDesde });
    }
}
