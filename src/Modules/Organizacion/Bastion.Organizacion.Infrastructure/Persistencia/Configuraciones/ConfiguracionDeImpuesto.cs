using Bastion.BuildingBlocks.Infrastructure.Auditoria;
using Bastion.BuildingBlocks.Infrastructure.Concurrencia;
using Bastion.BuildingBlocks.Infrastructure.Entidades;
using Bastion.Organizacion.Domain.Impuestos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bastion.Organizacion.Infrastructure.Persistencia.Configuraciones;

internal sealed class ConfiguracionDeImpuesto : IEntityTypeConfiguration<Impuesto>
{
    /// <summary>Nombre de la restricción que impide que dos tramos del mismo código se pisen.</summary>
    /// <remarks>
    /// Se declara aquí, junto a la tabla, y la migración la crea con SQL en bruto porque EF Core
    /// no sabe escribir un <c>EXCLUDE</c>. Que el nombre viva en un solo sitio es lo que permite
    /// que el test que la comprueba pregunte por él en vez de repetirlo.
    /// </remarks>
    public const string RestriccionDeSolape = "impuestos_sin_tramos_solapados";

    public void Configure(EntityTypeBuilder<Impuesto> impuesto)
    {
        ArgumentNullException.ThrowIfNull(impuesto);

        impuesto.ToTable("impuestos");

        // Maestro fiscal: cambiar el nombre de un tipo impositivo o su cuenta contable tiene
        // consecuencias en la liquidación, y quién lo hizo es exactamente lo que se pregunta
        // después.
        impuesto.SeAudita();
        impuesto.HasKey(fila => fila.Id);

        impuesto.LlevaTestigoDeConcurrencia();

        ConfiguracionDeEntidadBase.Mapear(impuesto);

        impuesto.Property(fila => fila.Codigo)
            .HasMaxLength(Impuesto.LongitudMaximaDeCodigo)
            .IsRequired()
            .SeAudita();

        impuesto.Property(fila => fila.Nombre)
            .HasMaxLength(Impuesto.LongitudMaximaDeNombre)
            .IsRequired()
            .SeAudita();

        impuesto.Property(fila => fila.Tipo)
            .HasConversion<string>()
            .IsRequired()
            .SeAudita();

        // `numeric(5,2)`: cabe del 0,00 al 100,00 y ni un dígito más. R6 — nunca `double`, que
        // no puede representar 0,1 y arrastra el error hasta la casilla de un modelo 303.
        impuesto.Property(fila => fila.Porcentaje)
            .HasPrecision(5, 2)
            .IsRequired()
            .SeAudita();

        // `date`, no `timestamptz`: la vigencia de un impuesto es un día del calendario, y el 1 de
        // septiembre de 2012 lo fue en Madrid y en Canarias a la vez (R14).
        impuesto.Property(fila => fila.VigenteDesde).IsRequired().SeAudita();
        impuesto.Property(fila => fila.VigenteHasta).SeAudita();

        impuesto.Property(fila => fila.CuentaRepercutido)
            .HasMaxLength(Impuesto.LongitudMaximaDeCuenta)
            .SeAudita();

        impuesto.Property(fila => fila.CuentaSoportado)
            .HasMaxLength(Impuesto.LongitudMaximaDeCuenta)
            .SeAudita();

        // ÍNDICE, no restricción de unicidad: el código se repite a propósito, una fila por
        // tramo. Lo que no puede repetirse es el TRAMO, y eso no lo sabe expresar un índice
        // único; lo expresa el `EXCLUDE` que crea la migración, con este mismo par de columnas.
        impuesto.HasIndex(fila => new { fila.Codigo, fila.VigenteDesde });
    }
}
