using Bastion.Api.FunctionalTests.Salud;
using Bastion.BuildingBlocks.Infrastructure.Auditoria;
using Bastion.Identidad.Infrastructure.Persistencia;
using Bastion.Organizacion.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Bastion.Api.FunctionalTests.Auditoria;

/// <summary>
/// La red que hace que la auditoría no se olvide de nada: recorre el modelo <b>ya construido</b> y
/// exige que cada entidad y cada propiedad diga qué se hace con ella cuando cambia.
/// </summary>
/// <remarks>
/// <para>
/// Es lo único de este ítem que escala a los dieciséis módulos del §5. Un módulo nuevo entra en
/// este barrido el día que registra su contexto, sin que nadie tenga que acordarse de venir aquí;
/// una entidad nueva sin clasificar pone la CI en rojo antes de que llegue a producción, y una
/// propiedad nueva también.
/// </para>
/// <para>
/// <b>Falla cerrado en los dos sentidos.</b> Sin clasificar no se audita —que es la dirección
/// segura para un secreto— y además se ve. La alternativa, «se audita todo salvo lo que alguien
/// marque», deja que un resumen de credencial añadido el año que viene entre en una tabla que por
/// diseño no se puede limpiar, y nadie se entera.
/// </para>
/// </remarks>
public sealed class CadaEntidadDeclaraSuAuditoriaTests : IDisposable
{
    private readonly ApiSinDependencias _api = new();

    public void Dispose() => _api.Dispose();

    [Fact]
    public void Ninguna_entidad_del_modelo_se_queda_sin_decir_si_se_audita()
    {
        List<string> mudas = [.. Entidades()
            .Where(tipo => !tipo.IsOwned())
            .Where(tipo => tipo.Auditoria().Que == ClasificacionDeAuditoria.SinClasificar)
            .Select(tipo => tipo.ShortName())];

        mudas.ShouldBeEmpty(
            "cada entidad dice `SeAudita()` o `NoSeAudita(motivo)` en su configuración. Sin decirlo, " +
            "no se audita y nadie se entera: el hueco no da error, da una traza incompleta.");
    }

    [Fact]
    public void Toda_entidad_que_queda_fuera_lleva_su_motivo_escrito()
    {
        List<string> sinMotivo = [.. Entidades()
            .Where(tipo => tipo.Auditoria() is { Que: ClasificacionDeAuditoria.NoAuditada, Motivo.Length: 0 })
            .Select(tipo => tipo.ShortName())];

        sinMotivo.ShouldBeEmpty("dejar algo fuera de la auditoría se explica, no se declara");
    }

    [Fact]
    public void Ninguna_propiedad_de_una_entidad_auditada_se_queda_sin_clasificar()
    {
        List<string> mudas = [.. Entidades()
            .Where(SeAudita)
            .SelectMany(tipo => Clasificables(tipo)
                .Where(propiedad => propiedad.Auditoria().Que == ClasificacionDeAuditoria.SinClasificar)
                .Select(propiedad => $"{tipo.ShortName()}.{propiedad.Name}"))];

        mudas.ShouldBeEmpty(
            "cada propiedad de una entidad auditada dice `SeAudita()`, `NoSeAudita(motivo)` o " +
            "`EsSecreta(motivo)`. Es una lista de permitidos a propósito: una de prohibidos se " +
            "olvida de la propiedad que alguien añada el año que viene, y el fallo es silencioso.");
    }

    [Fact]
    public void Toda_propiedad_que_queda_fuera_o_es_secreta_lleva_su_motivo()
    {
        List<string> sinMotivo = [.. Entidades()
            .SelectMany(tipo => tipo.GetProperties()
                .Where(propiedad => propiedad.Auditoria() is
                    { Que: ClasificacionDeAuditoria.NoAuditada or ClasificacionDeAuditoria.Secreta, Motivo.Length: 0 })
                .Select(propiedad => $"{tipo.ShortName()}.{propiedad.Name}"))];

        sinMotivo.ShouldBeEmpty("un secreto se nombra; un hueco se explica");
    }

    [Fact]
    public void La_clasificacion_de_una_entidad_que_no_se_audita_no_se_queda_por_ahi()
    {
        // Simétrico del de arriba, y por el mismo motivo que en el 0.6: una lista que solo crece
        // deja de describir el sistema. Si una entidad pasa a `NoSeAudita`, las marcas de sus
        // propiedades quedan mintiendo —dicen «esto va a la traza» sobre algo que no va— y la
        // siguiente persona las lee como si fueran verdad.
        List<string> huerfanas = [.. Entidades()
            .Where(tipo => !SeAudita(tipo))
            .SelectMany(tipo => tipo.GetProperties()
                .Where(propiedad => propiedad.Auditoria().Que != ClasificacionDeAuditoria.SinClasificar)
                .Select(propiedad => $"{tipo.ShortName()}.{propiedad.Name}"))];

        huerfanas.ShouldBeEmpty("su entidad no se audita, así que esta marca no la lee nadie");
    }

    [Fact]
    public void Una_entidad_propiedad_de_otra_hereda_la_decision_de_su_dueno_y_no_la_repite()
    {
        List<string> repetidas = [.. Entidades()
            .Where(tipo => tipo.IsOwned())
            .Where(tipo => tipo.Auditoria().Que != ClasificacionDeAuditoria.SinClasificar)
            .Select(tipo => tipo.DisplayName())];

        // Una `Direccion` no cambia por su cuenta: cambia porque cambia la empresa o el almacén de
        // la que cuelga. Que dijera lo suyo abriría la puerta a que dijese lo CONTRARIO que su
        // dueño, y entonces habría que decidir cuál gana — una pregunta que es mejor no tener.
        repetidas.ShouldBeEmpty("hereda de su dueño; no se clasifica aparte");
    }

    // Las que hay que clasificar: las escalares que no son la clave. La clave no se clasifica
    // porque no es un valor que cambie, es la fila de la que se habla, y va en su propia columna
    // de la traza (`entidad_id`).
    private static IEnumerable<IProperty> Clasificables(IEntityType tipo) =>
        tipo.GetProperties().Where(propiedad => !propiedad.IsPrimaryKey());

    private static bool SeAudita(IEntityType tipo)
    {
        IEntityType? dueno = tipo.IsOwned() ? tipo.FindOwnership()?.PrincipalEntityType : null;

        return (dueno ?? tipo).Auditoria().Que == ClasificacionDeAuditoria.Auditada;
    }

    private IEnumerable<IEntityType> Entidades()
    {
        using IServiceScope alcance = _api.Services.CreateScope();

        return
        [
            .. alcance.ServiceProvider.GetRequiredService<OrganizacionDbContext>().Model.GetEntityTypes(),
            .. alcance.ServiceProvider.GetRequiredService<IdentidadDbContext>().Model.GetEntityTypes(),
        ];
    }
}
