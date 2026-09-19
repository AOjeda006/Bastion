using Bastion.BuildingBlocks.Infrastructure.Auditoria;
using Bastion.BuildingBlocks.Infrastructure.Concurrencia;
using Bastion.BuildingBlocks.Infrastructure.Entidades;
using Bastion.Inventario.Domain.Ajustes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bastion.Inventario.Infrastructure.Persistencia.Configuraciones;

internal sealed class ConfiguracionDeAjuste : IEntityTypeConfiguration<Ajuste>
{
    public void Configure(EntityTypeBuilder<Ajuste> ajuste)
    {
        ArgumentNullException.ThrowIfNull(ajuste);

        ajuste.ToTable("ajustes");
        ajuste.HasKey(documento => documento.Id);

        // EL DOCUMENTO SÍ SE AUDITA, y es la otra mitad de por qué el libro no. Aquí es donde hay
        // decisiones de una persona —qué se ajusta, por qué, cuándo se confirma— y una traza de
        // esos cambios contesta preguntas que la fila del libro no contesta.
        ajuste.SeAudita();

        // Y LLEVA TESTIGO, que es la diferencia con sus líneas y con el libro: es un agregado que
        // CAMBIA. La transición a `Confirmado` lee el estado y luego escribe, y sin testigo dos
        // confirmaciones simultáneas del mismo ajuste leerían las dos `Borrador` y escribirían las
        // dos su tanda de movimientos — el stock movido dos veces, sin error y sin rastro. No
        // cuesta una columna: el testigo es `xmin`, que PostgreSQL ya lleva en toda fila.
        ajuste.LlevaTestigoDeConcurrencia();

        ConfiguracionDeEntidadBase.Mapear(ajuste);

        ajuste.Property(documento => documento.EmpresaId).IsRequired().SeAudita();
        ajuste.Property(documento => documento.AlmacenId).IsRequired().SeAudita();

        // `date`, no `timestamptz`: es una fecha de NEGOCIO (R14), y además es la que acabará
        // decidiendo en qué partición del libro caen las filas de este documento.
        ajuste.Property(documento => documento.FechaDeOperacion).IsRequired().SeAudita();

        ajuste.Property(documento => documento.Motivo)
            .HasMaxLength(Ajuste.LargoDelMotivo)
            .IsRequired()
            .SeAudita();

        // Como TEXTO, igual que los demás enumerados del proyecto: un entero en la base obliga a
        // tener el código delante para leer una fila. Y se audita, que es lo que convierte la
        // máquina de estados en algo que se puede contar después: quién confirmó y cuándo.
        ajuste.Property(documento => documento.Estado)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired()
            .SeAudita();

        // La colección se carga y se guarda con el documento: es su agregado. Los MOVIMIENTOS no
        // están aquí a propósito —viven fuera, con su motivo en `Ajuste.Confirmar`—, y por eso
        // cargar un ajuste para confirmarlo no trae ni una fila del libro al rastreador.
        ajuste.HasMany(documento => documento.Lineas)
            .WithOne()
            .HasForeignKey(linea => linea.AjusteId)
            .OnDelete(DeleteBehavior.Cascade);

        ajuste.Navigation(documento => documento.Lineas).AutoInclude();

        ajuste.HasIndex(documento => new { documento.EmpresaId, documento.FechaDeOperacion });
    }
}
