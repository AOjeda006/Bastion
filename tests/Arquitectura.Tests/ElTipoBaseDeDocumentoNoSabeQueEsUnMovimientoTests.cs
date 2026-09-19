using System.Reflection;
using NetArchTest.Rules;
using Shouldly;

namespace Bastion.Arquitectura.Tests;

/// <summary>
/// El tipo base de los documentos no sabe qué es un movimiento de existencias, y el bloque común
/// entero tampoco.
/// </summary>
/// <remarks>
/// <para>
/// <b>Lo nombra el propio <c>DocumentoBase</c> en su documentación</b> —«este tipo no sabe qué es
/// un movimiento, ni va a saberlo… lo comprueba
/// <c>ElTipoBaseDeDocumentoNoSabeQueEsUnMovimientoTests</c> sobre el ensamblado compilado»— y este
/// es ese fichero. Una promesa escrita en un <c>remarks</c> que no tiene detrás ningún caso es una
/// frase: se cumple mientras nadie la pruebe.
/// </para>
/// <para>
/// <b>Y lo que protege es estrecho y concreto.</b> El bloque común lo ven los dieciséis módulos.
/// El día que <c>DocumentoBase</c> supiera devolver movimientos —porque el ajuste los devuelve y
/// «subirlo» parece una simplificación—, la factura, el albarán y el asiento contable heredarían
/// esa idea sin pedirla, y el libro de existencias pasaría a ser un concepto del bloque común en
/// vez de una tabla del módulo Inventario. La dirección correcta es la contraria: el tipo base
/// sabe transitar estados y nada más; <b>qué</b> escribe cada transición lo sabe el documento
/// concreto, que es quien vive en su módulo.
/// </para>
/// <para>
/// <b>Las tres afirmaciones de este carril</b> (<see cref="Barrido"/>) valen aquí como en las
/// demás, y a la tercera se le añade la que la hace significar algo: la prohibición
/// <b>se dispara</b> en el sitio donde nombrar el libro sí está permitido. Sin eso, un espacio de
/// nombres con una letra de menos daría verde para siempre.
/// </para>
/// </remarks>
public sealed class ElTipoBaseDeDocumentoNoSabeQueEsUnMovimientoTests
{
    /// <summary>El espacio de nombres del libro de existencias.</summary>
    /// <remarks>
    /// Está tecleado y no derivado, así que es exactamente la clase de cadena que se puede
    /// estropear en silencio: por eso existe
    /// <see cref="La_prohibicion_del_libro_puede_dispararse"/>.
    /// </remarks>
    private const string ElLibro = "Bastion.Inventario.Domain.Movimientos";

    /// <summary>Dónde nombrar el libro SÍ está permitido: en su propio módulo.</summary>
    private static readonly string[] s_dondeSiSePuede = ["Inventario.Domain"];

    /// <summary>Ningún tipo del bloque común nombra el libro de existencias.</summary>
    /// <remarks>
    /// El alcance son las cuatro capas comunes y no solo el <c>Domain</c>. El
    /// <c>DocumentoBase</c> vive en el dominio, pero la fuga que hay que impedir no tiene por qué
    /// entrar por ahí: un interceptor de la infraestructura común que supiera qué es un movimiento
    /// dejaría el mismo acoplamiento heredado por los dieciséis módulos, y el nombre de este
    /// fichero seguiría siendo verdad.
    /// </remarks>
    [Fact]
    public void El_bloque_comun_no_nombra_el_libro_de_existencias() =>
        Barrido.Exige(
            "el bloque común no sabe qué es un movimiento de existencias: el libro es una tabla " +
            "del módulo Inventario, no un concepto que hereden los dieciséis",
            [.. Inventario.ComunesConTipos.Order(StringComparer.Ordinal)],
            tipos => tipos.Should().NotHaveDependencyOnAny(ElLibro));

    /// <summary>La prohibición se dispara donde nombrar el libro sí está permitido.</summary>
    /// <remarks>
    /// Es el contraejemplo del <see cref="Barrido"/>, y aquí no es ceremonia: si
    /// <c>Bastion.Inventario.Domain.Movimentos</c> —con una letra de menos— fuera lo que está
    /// escrito arriba, la regla no casaría con nada, el bloque común saldría limpio y el informe
    /// contaría una regla más entre las comprobadas.
    /// </remarks>
    [Fact]
    public void La_prohibicion_del_libro_puede_dispararse() =>
        Barrido.Dependen(s_dondeSiSePuede, ElLibro).ShouldBeGreaterThan(
            0,
            $"«{ElLibro}» no aparece ni en {s_dondeSiSePuede[0]}, que es donde el libro se " +
            "escribe. O la cadena está mal tecleada, o el módulo ya no tiene libro: en los dos " +
            "casos la prohibición de arriba está saliendo verde sin mirar nada");

    /// <summary>
    /// El tipo base no nombra un solo tipo de ningún módulo en sus miembros, y el documento
    /// concreto que lo hereda sí nombra el libro.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Esto mira las FIRMAS, que es lo que la regla de arriba no distingue.</b> El barrido de
    /// NetArchTest lee dependencias del ensamblado entero; aquí se mira miembro a miembro el tipo
    /// que da nombre al fichero, que es de lo que habla su documentación.
    /// </para>
    /// <para>
    /// <b>Y las dos mitades van juntas porque cada una es el arnés de la otra.</b> «El tipo base
    /// no nombra ningún tipo de módulo» saldría verde igual si la inspección no supiera leer
    /// firmas; que el documento concreto SÍ aparezca nombrando el libro —por la misma inspección,
    /// en la misma llamada— es lo que demuestra que sabe verlo cuando lo hay.
    /// </para>
    /// </remarks>
    [Fact]
    public void El_tipo_base_no_nombra_ningun_tipo_de_modulo_y_el_documento_concreto_si()
    {
        Type baseDeDocumentos = Ensamblados.Todos["BuildingBlocks.Domain"]
            .GetTypes()
            .SingleOrDefault(tipo => tipo.Name == "DocumentoBase`1")
            ?? throw new InvalidOperationException(
                "No está `DocumentoBase<TEstado>` en `BuildingBlocks.Domain`. Si se ha movido o " +
                "renombrado, este caso deja de mirar lo que dice mirar.");

        IReadOnlyList<Type> documentos =
        [
            .. from ensamblado in Ensamblados.Modulares.Values
               from tipo in ensamblado.GetTypes()
               where HeredaDe(tipo, baseDeDocumentos)
               orderby tipo.FullName, StringComparer.Ordinal
               select tipo,
        ];

        documentos.ShouldNotBeEmpty(
            "ningún módulo tiene todavía un documento con máquina de estados, así que «el tipo " +
            "base no sabe lo que saben sus hijos» no está comparando nada (ADR-0020)");

        // El arnés: la inspección encuentra el libro en el documento que lo devuelve.
        documentos
            .Where(documento => NombraElLibro(documento))
            .Select(documento => documento.Name)
            .ShouldNotBeEmpty(
                "ningún documento concreto nombra el libro en la firma de un miembro, así que " +
                "esta inspección no ha demostrado que sepa verlo. Hoy lo hace `Ajuste.Confirmar`, " +
                "que devuelve una fila del libro por línea");

        // Y la regla: el tipo base, con la MISMA inspección, no nombra ni un tipo de módulo.
        NombresDeModuloEnLasFirmas(baseDeDocumentos).ShouldBeEmpty(
            "`DocumentoBase<TEstado>` ha aprendido lo que es un tipo de módulo. Lo ven los " +
            "dieciséis: lo que sepa el tipo base lo heredan la factura y el asiento contable sin " +
            "pedirlo");
    }

    private static bool HeredaDe(Type tipo, Type baseGenerica)
    {
        for (Type? actual = tipo.BaseType; actual is not null; actual = actual.BaseType)
        {
            if (actual.IsGenericType && actual.GetGenericTypeDefinition() == baseGenerica)
            {
                return true;
            }
        }

        return false;
    }

    private static bool NombraElLibro(Type tipo) =>
        TiposDeLasFirmas(tipo).Any(nombrado =>
            nombrado.Namespace?.StartsWith(ElLibro, StringComparison.Ordinal) == true);

    private static IReadOnlyList<string> NombresDeModuloEnLasFirmas(Type tipo) =>
    [
        .. (from nombrado in TiposDeLasFirmas(tipo)
            let ensamblado = nombrado.Assembly.GetName().Name
            where ensamblado is not null
               && Ensamblados.Modulares.Any(par =>
                   string.Equals(
                       ensamblado,
                       Inventario.Raiz + "." + par.Key,
                       StringComparison.Ordinal))
            select nombrado.FullName ?? nombrado.Name)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal),
    ];

    /// <summary>
    /// Los tipos que aparecen en las firmas declaradas por un tipo: lo que devuelve, lo que
    /// recibe y lo que guarda.
    /// </summary>
    /// <remarks>
    /// <c>DeclaredOnly</c> a propósito: lo que herede de <c>RaizAgregado</c> es de
    /// <c>RaizAgregado</c>, y mezclarlo haría que este caso se pusiera rojo por algo que no ha
    /// escrito el tipo que da nombre al fichero. Y se desenvuelven los genéricos, porque un
    /// <c>IReadOnlyList&lt;MovimientoStock&gt;</c> es <c>IReadOnlyList</c> por fuera y el libro
    /// por dentro — que es exactamente la forma que tiene hoy `Ajuste.Confirmar`.
    /// </remarks>
    private static IEnumerable<Type> TiposDeLasFirmas(Type tipo)
    {
        const BindingFlags Todos = BindingFlags.Public
            | BindingFlags.NonPublic
            | BindingFlags.Instance
            | BindingFlags.Static
            | BindingFlags.DeclaredOnly;

        foreach (MethodInfo metodo in tipo.GetMethods(Todos))
        {
            foreach (Type nombrado in Desenvolver(metodo.ReturnType))
            {
                yield return nombrado;
            }

            foreach (Type nombrado in metodo.GetParameters().SelectMany(p => Desenvolver(p.ParameterType)))
            {
                yield return nombrado;
            }
        }

        foreach (ConstructorInfo constructor in tipo.GetConstructors(Todos))
        {
            foreach (Type nombrado in
                constructor.GetParameters().SelectMany(p => Desenvolver(p.ParameterType)))
            {
                yield return nombrado;
            }
        }

        foreach (FieldInfo campo in tipo.GetFields(Todos))
        {
            foreach (Type nombrado in Desenvolver(campo.FieldType))
            {
                yield return nombrado;
            }
        }
    }

    private static IEnumerable<Type> Desenvolver(Type tipo)
    {
        yield return tipo;

        if (tipo.HasElementType && tipo.GetElementType() is Type elemento)
        {
            foreach (Type dentro in Desenvolver(elemento))
            {
                yield return dentro;
            }
        }

        foreach (Type argumento in tipo.GetGenericArguments())
        {
            foreach (Type dentro in Desenvolver(argumento))
            {
                yield return dentro;
            }
        }
    }
}
