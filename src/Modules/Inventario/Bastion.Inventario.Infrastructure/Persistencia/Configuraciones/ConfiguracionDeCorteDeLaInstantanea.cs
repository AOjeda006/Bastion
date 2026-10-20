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

    /// <summary>Hasta donde se ha cuadrado también es un primer día de mes, si lo hay.</summary>
    internal const string CuadradoHastaEsPrimerDia = "extract(day from cuadrado_hasta) = 1";

    /// <summary>El cuadre no compara un mes que el corte no tiene (ADR-0067).</summary>
    internal const string CuadradoHastaNoPasaDelCorte = "cuadrado_hasta <= hasta_el_mes";

    public void Configure(EntityTypeBuilder<CorteDeLaInstantanea> corte)
    {
        ArgumentNullException.ThrowIfNull(corte);

        corte.ToTable(
            Tabla,
            tabla =>
            {
                tabla.HasCheckConstraint("ck_cortes_de_la_instantanea_mes_es_primer_dia", PrimerDiaDelMes);
                tabla.HasCheckConstraint(
                    "ck_cortes_de_la_instantanea_cuadrado_hasta_es_primer_dia", CuadradoHastaEsPrimerDia);
                tabla.HasCheckConstraint(
                    "ck_cortes_de_la_instantanea_cuadrado_hasta_no_pasa_del_corte", CuadradoHastaNoPasaDelCorte);
            });

        corte.NoSeAudita(
            "lo escribe en crudo el recálculo de las instantáneas, que no pasa por el rastreador, " +
            "y solo dice hasta dónde llega una copia que se puede tirar");

        // Una empresa tiene un corte y solo uno: la clave primaria es la empresa.
        corte.HasKey(fila => fila.EmpresaId);

        corte.Property(fila => fila.HastaElMes).IsRequired();

        // Nulo es «nada cuadrado todavía», que es lo que tiene un corte recién rehecho, y lo que
        // tienen los cortes de antes del 2.18: su primer cuadre empieza por el primer mes.
        corte.Property(fila => fila.CuadradoHasta);
    }
}
