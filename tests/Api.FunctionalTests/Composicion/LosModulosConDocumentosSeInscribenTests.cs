using System.Reflection;
using Bastion.Api.FunctionalTests.Salud;
using Bastion.BuildingBlocks.Domain.Documentos;
using Bastion.Organizacion.Contracts.Ejercicios;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Bastion.Api.FunctionalTests.Composicion;

/// <summary>
/// Todo módulo que tenga documentos está inscrito como <see cref="IDocumentosDeUnPeriodo"/>, y
/// ninguna inscripción nombra un módulo que no los tenga.
/// </summary>
/// <remarks>
/// <para>
/// <b>Esta regla existe por el modo de fallo del puerto, que es el silencio.</b> Cerrar un
/// ejercicio pregunta a los módulos inscritos si queda algún borrador dentro del intervalo. Un
/// módulo con documentos que no se inscriba no da error al arrancar, no da error al cerrar y no
/// aparece en ningún registro: sencillamente <b>no existe</b> para el cierre, y cerrar contesta
/// «no queda nada dentro» habiendo preguntado a menos gente de la que había. El periodo se da por
/// congelado con borradores vivos dentro, que es justo lo que la R9 viene a impedir.
/// </para>
/// <para>
/// <b>Los dos lados se descubren, ninguno se teclea.</b> Uno sale del <b>IL</b>: un documento es un
/// tipo que hereda de <see cref="DocumentoBase{TEstado}"/>, que no es una heurística por nombre
/// sino la definición que usa el proyecto —el <c>set</c> privado del estado vive ahí—. El otro sale
/// del <b>contenedor montado</b>: las implementaciones que el host resuelve de verdad. Una lista
/// escrita a mano habría que ampliarla cinco veces según lleguen los módulos que faltan, y la que
/// no se ampliara dejaría el hueco exactamente donde nadie mira.
/// </para>
/// <para>
/// <b>Y se comparan en los dos sentidos.</b> Un módulo con documentos sin inscribir falla porque
/// falta —el caso de arriba—; una inscripción cuyo módulo ya no tiene documentos falla porque
/// sobra, y esa avisa de un puerto que quedó registrado apuntando a una tabla que se fue. Las dos
/// listas se afirman además <b>no vacías</b> (ADR-0020): el día que el descubrimiento se rompa por
/// un renombrado, dos conjuntos vacíos son iguales y este barrido saldría verde sin haber mirado
/// nada.
/// </para>
/// <para>
/// <b>Sin base de datos:</b> las inscripciones se resuelven al montar el contenedor y los tipos se
/// leen del ensamblado, así que esto sale en el paso rápido de la CI y no minutos después.
/// </para>
/// </remarks>
public sealed class LosModulosConDocumentosSeInscribenTests : IDisposable
{
    private readonly ApiSinDependencias _api = new();

    public void Dispose() => _api.Dispose();

    [Fact]
    public void Todo_modulo_con_documentos_esta_inscrito_y_ninguna_inscripcion_sobra()
    {
        IReadOnlyList<string> conDocumentos = ModulosConDocumentos();
        IReadOnlyList<string> inscritos = Inscritos();

        // El arnés de la regla, por duplicado, y ya se ha cobrado una pieza: la primera versión de
        // este barrido barría antes de levantar el host y salió roja AQUÍ, porque el ensamblado del
        // único documento que hay todavía no estaba cargado. Sin esta línea habría salido verde
        // comparando dos conjuntos vacíos. Lo mismo vale para lo que venga: `DocumentoBase`
        // renombrado o movido, o la inscripción desaparecida entera, dan dos lados vacíos que
        // son iguales entre sí, y la igualdad de abajo saldría verde sin mirar un solo módulo.
        conDocumentos.ShouldNotBeEmpty(
            "ningún tipo del sistema hereda de `DocumentoBase<>`, así que este barrido no está " +
            "mirando ningún documento y su silencio no significa nada");

        inscritos.ShouldNotBeEmpty(
            "no hay ni una implementación de `IDocumentosDeUnPeriodo` en el contenedor, así que " +
            "cerrar un ejercicio no le preguntaría a nadie y diría que el periodo está limpio");

        inscritos.ShouldBe(
            conDocumentos,
            customMessage: "los módulos inscritos como `IDocumentosDeUnPeriodo` no son los que " +
            "tienen documentos. Los que tienen documentos: " + string.Join(", ", conDocumentos) +
            ". Los inscritos: " + string.Join(", ", inscritos) + ". Uno que falta es un módulo al " +
            "que el cierre NO le pregunta; uno que sobra, un puerto registrado sin tablas detrás.");
    }

    [Fact]
    public void Cada_inscripcion_dice_el_modulo_en_el_que_vive()
    {
        IReadOnlyList<IDocumentosDeUnPeriodo> implementaciones = Implementaciones();

        implementaciones.ShouldNotBeEmpty(
            "no hay ni una implementación de `IDocumentosDeUnPeriodo` que comprobar");

        // Sin esto, la igualdad del caso anterior se puede satisfacer MINTIENDO: `Modulo` es una
        // cadena escrita a mano, y una implementación de Ventas que devolviera «Inventario»
        // dejaría los dos conjuntos iguales. Y esa cadena no es decoración: es lo que el error del
        // cierre usa para decir DÓNDE están los borradores que impiden cerrar. Con seis módulos
        // inscritos, un nombre equivocado manda a buscarlos al sitio que no es.
        List<string> mentirosas =
        [
            .. from implementacion in implementaciones
               let vive = Modulo(implementacion.GetType().Assembly)
               where !string.Equals(implementacion.Modulo, vive, StringComparison.Ordinal)
               select $"{implementacion.GetType().FullName} dice ser de «{implementacion.Modulo}» " +
                   $"y vive en «{vive}»",
        ];

        mentirosas.ShouldBeEmpty(
            "estas implementaciones no se llaman como el módulo en el que viven: " +
            string.Join("; ", mentirosas));
    }

    /// <summary>Los módulos que tienen algún documento, leídos del IL y ordenados.</summary>
    /// <remarks>
    /// <b>Levantar el host es parte de la regla, no un preliminar.</b> Lo que se barre son los
    /// ensamblados <b>cargados</b>, y el CLR no carga uno hasta que alguien lo necesita: sin esta
    /// línea, <c>Bastion.Inventario.Domain</c> todavía no está en el dominio y el descubrimiento
    /// contesta «aquí no hay documentos». Se probó: la primera versión de este barrido salió roja
    /// por su propia afirmación de conjunto no vacío, no por el código que vigila. Dejarlo al orden
    /// en que xUnit ejecute los casos sería dejarlo al azar.
    /// </remarks>
    private IReadOnlyList<string> ModulosConDocumentos()
    {
        _ = _api.Services;

        return
        [
            .. DeBastion()
                .SelectMany(ensamblado => ensamblado.GetTypes())
                .Where(EsUnDocumento)
                .Select(tipo => Modulo(tipo.Assembly))
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal),
        ];
    }

    /// <summary>Los módulos inscritos en el contenedor, por lo que dice cada implementación.</summary>
    private IReadOnlyList<string> Inscritos() =>
    [
        .. Implementaciones()
            .Select(implementacion => implementacion.Modulo)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal),
    ];

    // `scoped`, como el contexto del que cuelgan, así que se resuelven dentro de un ámbito: desde
    // la raíz ni siquiera se construirían.
    private IReadOnlyList<IDocumentosDeUnPeriodo> Implementaciones()
    {
        using IServiceScope alcance = _api.Services.CreateScope();

        return [.. alcance.ServiceProvider.GetServices<IDocumentosDeUnPeriodo>()];
    }

    /// <summary>Si un tipo hereda de <see cref="DocumentoBase{TEstado}"/>, sea cual sea su estado.</summary>
    private static bool EsUnDocumento(Type tipo)
    {
        for (Type? actual = tipo.BaseType; actual is not null; actual = actual.BaseType)
        {
            if (actual.IsGenericType && actual.GetGenericTypeDefinition() == typeof(DocumentoBase<>))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>El módulo de un ensamblado: <c>Bastion.Inventario.Domain</c> → <c>Inventario</c>.</summary>
    private static string Modulo(Assembly ensamblado) =>
        ensamblado.GetName().Name!.Split('.')[1];

    // Los ensamblados del sistema, ya cargados porque el contenedor está montado. Se barren todos
    // y no solo los `Domain`: un documento declarado donde no toca seguiría siendo un documento, y
    // lo que aquí se defiende es que ninguno se quede sin quien conteste por él.
    private static IEnumerable<Assembly> DeBastion() => AppDomain.CurrentDomain
        .GetAssemblies()
        .Where(ensamblado => ensamblado.GetName().Name is string nombre
            && nombre.StartsWith("Bastion.", StringComparison.Ordinal)
            && !nombre.EndsWith("Tests", StringComparison.Ordinal));
}
