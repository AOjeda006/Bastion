using Bastion.BuildingBlocks.Infrastructure.Auditoria;
using Bastion.Inventario.Domain.Existencias;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bastion.Inventario.Infrastructure.Persistencia.Configuraciones;

internal sealed class ConfiguracionDeCorteDeLaInstantanea
    : IEntityTypeConfiguration<CorteDeLaInstantanea>
{
    /// <summary>La tabla, nombrada aquí porque la leen y la escriben sentencias crudas.</summary>
    internal const string Tabla = "cortes_de_la_instantanea";

    /// <summary>El corte es siempre un primer día de mes.</summary>
    internal const string PrimerDiaDelMes = "extract(day from hasta_el_mes) = 1";

    public void Configure(EntityTypeBuilder<CorteDeLaInstantanea> corte)
    {
        ArgumentNullException.ThrowIfNull(corte);

        corte.ToTable(
            Tabla,
            tabla => tabla.HasCheckConstraint(
                "ck_cortes_de_la_instantanea_mes_es_primer_dia", PrimerDiaDelMes));

        corte.NoSeAudita(
            "lo escribe en crudo el recálculo de las instantáneas, que no pasa por el rastreador, " +
            "y solo dice hasta dónde llega una copia que se puede tirar");

        // Una empresa tiene un corte y solo uno: la clave primaria es la empresa.
        corte.HasKey(fila => fila.EmpresaId);

        corte.Property(fila => fila.HastaElMes).IsRequired();
    }
}
