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
/// <b>Los tipos complejos entran por su camino.</b> Una entidad poseída sale en
/// <c>GetEntityTypes()</c> y por eso se le puede exigir que no repita la clasificación de su
/// dueño; un tipo complejo no sale, y tampoco tiene dónde guardarla —el lector
/// <c>Auditoria()</c> de nivel de tipo pide un <c>IReadOnlyEntityType</c>, que un
/// <c>IComplexType</c> no es—, así que ahí no hace falta una prueba: no compila. Lo que sí hacía
/// falta es que sus propiedades no se escapen, y de eso se ocupan
/// <c>PropiedadesConCamino()</c> y el caso que fija cuáles son.
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
    // Los tipos complejos del modelo y cuántas propiedades escalares aporta cada uno. La lista
    // ENTERA y en los dos sentidos, como la de los testigos del ADR-0015: es lo único que impide
    // que este fichero se quede verde por estar mirando donde ya no hay nada.
    private static readonly string[] s_tiposComplejos =
    [
        "Almacen.Bloqueo: 3",
        "Almacen.Direccion: 6",
        "Empresa.Bloqueo: 3",
        "Empresa.DomicilioFiscal: 6",
        "Ubicacion.Bloqueo: 3",
        "Usuario.Bloqueo: 3",
    ];

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
                .Where(par => par.Propiedad.Auditoria().Que == ClasificacionDeAuditoria.SinClasificar)
                .Select(par => $"{tipo.ShortName()}.{par.Camino}"))];

        mudas.ShouldBeEmpty(
            "cada propiedad de una entidad auditada dice `SeAudita()`, `NoSeAudita(motivo)` o " +
            "`EsSecreta(motivo)`. Es una lista de permitidos a propósito: una de prohibidos se " +
            "olvida de la propiedad que alguien añada el año que viene, y el fallo es silencioso.");
    }

    [Fact]
    public void Toda_propiedad_que_queda_fuera_o_es_secreta_lleva_su_motivo()
    {
        List<string> sinMotivo = [.. Entidades()
            .SelectMany(tipo => tipo.PropiedadesConCamino()
                .Where(par => par.Propiedad.Auditoria() is
                    { Que: ClasificacionDeAuditoria.NoAuditada or ClasificacionDeAuditoria.Secreta, Motivo.Length: 0 })
                .Select(par => $"{tipo.ShortName()}.{par.Camino}"))];

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
            .SelectMany(tipo => tipo.PropiedadesConCamino()
                .Where(par => par.Propiedad.Auditoria().Que != ClasificacionDeAuditoria.SinClasificar)
                .Select(par => $"{tipo.ShortName()}.{par.Camino}"))];

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
        // Desde el 0.10 recorre un conjunto VACÍO: la dirección era el único tipo poseído del
        // modelo y ahora es complejo. Se queda porque la regla no ha caducado —un poseído nuevo
        // volvería a caer aquí—, pero se dice que hoy no puede fallar, que es lo que distingue
        // una red de una que parece una red.
        repetidas.ShouldBeEmpty("hereda de su dueño; no se clasifica aparte");
    }

    [Fact]
    public void Las_propiedades_de_un_tipo_complejo_entran_en_este_barrido()
    {
        // El caso que hace que los de arriba signifiquen algo. Un tipo COMPLEJO no es una entidad:
        // no sale en `GetEntityTypes()` y `GetProperties()` NO devuelve sus propiedades. Medido en
        // el 0.10 antes de escribir esto: con la direccion mapeada como tipo complejo y los
        // barridos sin ampliar, DOCE propiedades salieron de la clasificación —152 escalares a
        // 138— y los catorce casos siguieron en VERDE. Un barrido que mira donde no hay nada no
        // avisa de nada, y este es el que se pone rojo cuando eso pasa.
        //
        // Se deriva de `PropiedadesConCamino()`, que es el mismo recorrido que usan los casos de
        // arriba: así esto no comprueba una lista paralela, sino el recorrido de verdad. Y por eso
        // vale también para un tipo complejo anidado dentro de otro, que aparecería con su camino
        // entero.
        List<string> complejos = [.. Entidades()
            .SelectMany(tipo => tipo.PropiedadesConCamino()
                .Where(par => par.Camino.Contains('.', StringComparison.Ordinal))
                .Select(par => $"{tipo.ShortName()}.{par.Camino[..par.Camino.LastIndexOf('.')]}"))
            .GroupBy(camino => camino, StringComparer.Ordinal)
            .Select(grupo => $"{grupo.Key}: {grupo.Count()}")];

        complejos.Sort(StringComparer.Ordinal);

        string.Join(", ", complejos).ShouldBe(
            string.Join(", ", s_tiposComplejos),
            "un tipo complejo que desaparece del modelo, o que cambia de forma, se ve aquí. Si " +
            "esto se pone rojo, comprueba primero que las propiedades que faltan no se hayan " +
            "quedado sin clasificar en silencio.");
    }

    // Las que hay que clasificar: las escalares que no son la clave. La clave no se clasifica
    // porque no es un valor que cambie, es la fila de la que se habla, y va en su propia columna
    // de la traza (`entidad_id`).
    private static IEnumerable<(string Camino, IReadOnlyProperty Propiedad)> Clasificables(IEntityType tipo) =>
        tipo.PropiedadesConCamino().Where(par => !par.Propiedad.IsPrimaryKey());

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
