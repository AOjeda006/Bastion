using Bastion.BuildingBlocks.Domain.Identificacion;
using Bastion.BuildingBlocks.Infrastructure.Auditoria;
using Bastion.BuildingBlocks.Infrastructure.Entidades;
using Bastion.Terceros.Domain.Terceros;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bastion.Terceros.Infrastructure.Persistencia.Configuraciones;

internal sealed class ConfiguracionDeCuentaBancaria : IEntityTypeConfiguration<CuentaBancaria>
{
    public void Configure(EntityTypeBuilder<CuentaBancaria> cuenta)
    {
        ArgumentNullException.ThrowIfNull(cuenta);

        cuenta.ToTable("cuentas_bancarias");

        // Se audita entera, y esta con más motivo que ninguna: cambiar el IBAN de un proveedor es
        // exactamente lo que hace el fraude del falso cambio de cuenta. Quién lo cambió y cuándo
        // tiene que constar.
        cuenta.SeAudita();
        cuenta.HasKey(fila => fila.Id);

        cuenta.Property(fila => fila.TerceroId).IsRequired().SeAudita();

        // Una columna, con la conversión del objeto de valor: `Iban.De` vuelve a validar al leer,
        // así que una fila manipulada a mano en la base revienta al materializarse en vez de
        // circular como si fuera buena.
        cuenta.Property(fila => fila.Iban)
            .HasConversion(iban => iban.Valor, valor => Iban.De(valor))
            .HasMaxLength(Iban.LongitudMaxima)
            .IsRequired()
            .SeAudita();

        cuenta.Property(fila => fila.Bic)
            .HasMaxLength(CuentaBancaria.LongitudMaximaDeBic)
            .SeAudita();

        cuenta.Property(fila => fila.Alias)
            .HasMaxLength(CuentaBancaria.LongitudMaximaDeAlias)
            .SeAudita();

        cuenta.Property(fila => fila.EsPreferente).IsRequired().SeAudita();

        ConfiguracionDeEntidadBase.Mapear(cuenta);

        // El mismo IBAN dos veces en la misma ficha son dos filas que el fichero de adeudos no
        // sabe distinguir. El agregado ya lo rechaza; esto es lo que lo sostiene cuando llegan dos
        // peticiones a la vez, que es cuando el agregado no puede verlo.
        cuenta.HasIndex(fila => new { fila.TerceroId, fila.Iban }).IsUnique();

        // LA PREFERENTE ÚNICA ES UN ÍNDICE PARCIAL, Y HAY QUE LEER POR QUÉ NO ES OTRA COSA.
        //
        // `(tercero_id) WHERE es_preferente`. Un `HasIndex(TerceroId, EsPreferente).IsUnique()`
        // sería una restricción distinta y equivocada: prohibiría dos cuentas NO preferentes del
        // mismo tercero, que es justo el caso normal.
        //
        // Y SOBRE SI VE LAS FILAS BLOQUEADAS: es la MISMA decisión del ítem 1.5, no una nueva. El
        // índice no lleva predicado de bloqueo, así que abarca también lo bloqueado. Aquí además
        // no podría ser de otro modo por la forma del modelo: el bloqueo vive en el TERCERO, no en
        // la cuenta, de manera que las cuentas que compiten por ser preferentes son siempre del
        // mismo tercero y están siempre en el mismo estado. La pregunta que en 1.5 sí tenía dos
        // respuestas posibles —¿una ficha bloqueada libera su identificador?— aquí no las tiene.
        cuenta.HasIndex(fila => fila.TerceroId)
            .IsUnique()
            .HasFilter("es_preferente")
            .HasDatabaseName("ix_cuentas_bancarias_una_preferente_por_tercero");
    }
}
