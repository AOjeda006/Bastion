using System.Globalization;
using Bastion.BuildingBlocks.Infrastructure.Auditoria;
using Bastion.BuildingBlocks.Infrastructure.Concurrencia;
using Bastion.BuildingBlocks.Infrastructure.Entidades;
using Bastion.Catalogo.Domain.Catalogo;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bastion.Catalogo.Infrastructure.Persistencia.Configuraciones;

/// <summary>
/// Mapeo del código de barras: el GTIN que lleva un artículo, en qué nivel y con cuántas unidades
/// base (ADR-0051 §2 y §3, con los nombres del ADR-0052).
/// </summary>
/// <remarks>
/// <para>
/// <b>El índice único <c>(empresa_id, gtin)</c> es quien decide que un GTIN sea de un solo
/// artículo</b> (ADR-0051 §5). La comprobación previa del alta es cortesía: entre ella y la
/// escritura cabe otra transacción. El índice lleva nombre propio porque el borde lo traduce por su
/// nombre a un <c>409</c>, y la declaración está en <c>ModuloDeCatalogo</c>. La empresa va delante:
/// es el filtro que toda consulta lleva (R8), y el mismo GTIN entra en otra empresa.
/// </para>
/// <para>
/// <b>Los tres <c>CHECK</c> repiten en el motor lo que el dominio ya garantiza</b>: catorce cifras,
/// un nivel del enumerado, y una unidad en la base y dos o más en una agrupación. Son la red de lo que
/// no pasa por el dominio, como una migración de datos o una fila escrita a mano.
/// </para>
/// </remarks>
internal sealed class ConfiguracionDeCodigoBarras : IEntityTypeConfiguration<CodigoBarras>
{
    /// <summary>Un GTIN es de un solo artículo en cada empresa.</summary>
    public const string GtinUnoPorEmpresa = "ix_codigos_barras_gtin_uno_por_empresa";

    /// <summary>El GTIN se guarda en su forma de catorce cifras ASCII.</summary>
    public const string GtinDeCatorceCifras = "ck_codigos_barras_gtin_catorce_cifras";

    /// <summary>El nivel es uno de los del enumerado.</summary>
    public const string NivelAdmitido = "ck_codigos_barras_nivel";

    /// <summary>La base lleva una unidad, y una agrupación, dos o más.</summary>
    public const string UnidadesSegunElNivel = "ck_codigos_barras_unidades_segun_el_nivel";

    public void Configure(EntityTypeBuilder<CodigoBarras> codigo)
    {
        ArgumentNullException.ThrowIfNull(codigo);

        codigo.ToTable("codigos_barras", tabla =>
        {
            // `[0-9]` y no `\d`: en PostgreSQL, `\d` es la clase `[[:digit:]]`, y lo que entra en una
            // clase lo decide la configuración regional de la base. El dominio solo admite ASCII.
            tabla.HasCheckConstraint(
                GtinDeCatorceCifras,
                "gtin ~ '^[0-9]{" + Gtin.Longitud.ToString(CultureInfo.InvariantCulture) + "}$'");

            tabla.HasCheckConstraint(
                NivelAdmitido,
                "nivel IN ('" + string.Join("', '", Enum.GetNames<NivelDeGtin>()) + "')");

            tabla.HasCheckConstraint(
                UnidadesSegunElNivel,
                "(nivel = '" + nameof(NivelDeGtin.Base) + "' AND unidades = " +
                CodigoBarras.UnidadesDeLaBase.ToString(CultureInfo.InvariantCulture) + ")" +
                " OR (nivel <> '" + nameof(NivelDeGtin.Base) + "' AND unidades >= " +
                CodigoBarras.UnidadesMinimasDeUnaAgrupacion.ToString(CultureInfo.InvariantCulture) + ")");
        });

        codigo.SeAudita();
        codigo.HasKey(fila => fila.Id);

        // La fila no cambia nunca, pero la baja exige su `If-Match`: nadie borra lo que no ha visto
        // (ADR-0051 §6).
        codigo.LlevaTestigoDeConcurrencia();

        ConfiguracionDeEntidadBase.Mapear(codigo);

        // Columna propia, como en el suministro: el filtro de la R8 se evalúa sobre las columnas de la
        // fila, y el índice único va por empresa.
        codigo.Property(fila => fila.EmpresaId).IsRequired().SeAudita();

        codigo.Property(fila => fila.ArticuloId).IsRequired().SeAudita();

        // Se lee con `Gtin.De`, que vuelve a comprobarlo todo, como el IBAN: una fila manipulada a
        // mano revienta al materializarse en vez de circular como si fuera buena. Por eso endurecer la
        // tabla de prefijos exige mirar antes las filas guardadas, y relajarla no exige nada.
        codigo.Property(fila => fila.Gtin)
            .HasConversion(gtin => gtin.Valor, valor => Gtin.De(valor))
            .HasMaxLength(Gtin.Longitud)
            .IsRequired()
            .SeAudita();

        // Como texto y no como entero, igual que el tipo del artículo: un entero dejaría de
        // significar nada en cuanto alguien reordenara el enumerado.
        codigo.Property(fila => fila.Nivel)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired()
            .SeAudita();

        codigo.Property(fila => fila.Unidades).IsRequired().SeAudita();

        // Del mismo esquema, así que clave ajena de verdad, y `Restrict`, como en todo el módulo.
        codigo.HasOne<Articulo>()
            .WithMany()
            .HasForeignKey(fila => fila.ArticuloId)
            .OnDelete(DeleteBehavior.Restrict);

        codigo.HasIndex(fila => new { fila.EmpresaId, fila.Gtin })
            .IsUnique()
            .HasDatabaseName(GtinUnoPorEmpresa);
    }
}
