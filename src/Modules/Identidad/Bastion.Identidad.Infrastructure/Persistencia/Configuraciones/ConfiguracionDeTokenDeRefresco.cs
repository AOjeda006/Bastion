using Bastion.BuildingBlocks.Infrastructure.Auditoria;
using Bastion.Identidad.Domain.Sesiones;
using Bastion.Identidad.Domain.Usuarios;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bastion.Identidad.Infrastructure.Persistencia.Configuraciones;

internal sealed class ConfiguracionDeTokenDeRefresco : IEntityTypeConfiguration<TokenDeRefresco>
{
    public void Configure(EntityTypeBuilder<TokenDeRefresco> token)
    {
        ArgumentNullException.ThrowIfNull(token);

        token.ToTable("tokens_de_refresco");

        // El «no» de la lista, y por dos motivos que se suman. Uno: rota constantemente —una fila
        // por acceso y otra por cada renovacion, cada quince minutos—, asi que auditarla llenaria
        // de ruido una tabla que por diseño no se puede limpiar. Dos: lleva `Hash`, que es un
        // resumen de credencial. Lo que de esta tabla interesa a una auditoria es «quien entro y
        // cuando», y eso ya deja traza en `Usuario.UltimoAccesoEn`.
        token.NoSeAudita(
            "emision de refresco: rota cada quince minutos y lleva un resumen de credencial. " +
            "El acceso deja traza en Usuario.UltimoAccesoEn, que es el dato que interesa.");
        token.HasKey(fila => fila.Id);

        token.Property(fila => fila.UsuarioId).IsRequired();
        token.Property(fila => fila.FamiliaId).IsRequired();
        token.Property(fila => fila.EmpresaActivaId).IsRequired();

        // Lo que se guarda es el RESUMEN, nunca el token. Quien lea esta tabla —una copia de
        // seguridad, un volcado de soporte, una consulta de un administrador— no se lleva ninguna
        // sesión: con el SHA-256 no se puede renovar nada.
        //
        // 64 caracteres exactos, que es lo que mide un SHA-256 en hexadecimal. Fijo y no variable:
        // si un día llegara aquí otra cosa, la base lo rechaza en vez de guardarla.
        token.Property(fila => fila.Hash)
            .HasMaxLength(64)
            .IsRequired();

        // Índice único sobre el resumen: es por donde entra CADA renovación, y es único porque
        // dos emisiones con el mismo resumen serían el mismo token. Sin este índice la búsqueda
        // del camino más caliente sería un recorrido de tabla entera.
        token.HasIndex(fila => fila.Hash).IsUnique();

        // Por familia se busca al detectar una reutilización, que es cuando hay que revocarlas
        // todas de golpe.
        token.HasIndex(fila => fila.FamiliaId);
        token.HasIndex(fila => fila.UsuarioId);

        token.Property(fila => fila.CreadoEn).IsRequired();
        token.Property(fila => fila.ExpiraEn).IsRequired();

        // Los tres nulos son la historia de la emisión, y por eso se guardan en vez de borrar la
        // fila: `CanjeadoEn` es lo que convierte «este token no vale» en «este token YA SE USÓ»,
        // que es la única señal que delata una reutilización. Borrar la fila al canjearla haría
        // que un token robado y ya usado se viera igual que uno inventado.
        token.Property(fila => fila.CanjeadoEn);
        token.Property(fila => fila.SustituidoPorId);
        token.Property(fila => fila.RevocadoEn);

        token.Property(fila => fila.Motivo)
            .HasConversion<string>();

        // Aquí la clave ajena SÍ va: usuario y token viven en el mismo esquema. Lo que el §4
        // prohíbe es cruzar esquemas, no tener integridad referencial dentro de uno.
        token.HasOne<Usuario>()
            .WithMany()
            .HasForeignKey(fila => fila.UsuarioId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
