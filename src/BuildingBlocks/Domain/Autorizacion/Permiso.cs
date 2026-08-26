using System.Diagnostics.CodeAnalysis;

namespace Bastion.BuildingBlocks.Domain.Autorizacion;

/// <summary>
/// Un permiso por acción, con la forma <c>modulo.recurso.accion</c> que fija el §11 del plan
/// maestro (<c>ventas.pedido.confirmar</c>, <c>contabilidad.asiento.contabilizar</c>).
/// </summary>
/// <remarks>
/// <para>
/// <b>Por qué es un tipo y no un <c>string</c>.</b> La autorización es una cadena de eslabones
/// que tienen que casar exactamente: lo que el endpoint declara, lo que la política compara y lo
/// que el rol concede. Con cadenas sueltas, <c>organizacion.empresa.crear</c> y
/// <c>organizacion.empresas.crear</c> conviven sin que nada proteste, y el fallo no es un error:
/// es una puerta que deja de cerrarse. El tipo obliga a que la forma sea válida y hace que el
/// conjunto de permisos concedidos se pueda comparar por valor.
/// </para>
/// <para>
/// <b>Tres partes, ni dos ni cuatro.</b> El módulo dice de quién es la puerta, el recurso qué
/// tipo se toca y la acción qué se le hace. Que la acción esté separada del recurso es lo que
/// permite que <c>crear</c> y <c>modificar</c> sean permisos distintos aunque los escriba el
/// mismo código: autorizar una operación no autoriza lo que esa operación escribe. Y es lo que
/// hace expresable la segregación de funciones del §11 —quien crea un pedido de compra no lo
/// aprueba— sin un rol «administrador» que todo lo pueda.
/// </para>
/// <para>
/// Vive en el bloque común de dominio, no en Identidad: lo declaran TODOS los módulos y lo
/// consume la política central de autorización. Si viviera en Identidad, cualquier módulo que
/// quisiera declarar sus permisos tendría que referenciarla, y la frontera del §4 se rompería
/// por el sitio más tonto.
/// </para>
/// <para>
/// Dos puertas, como <c>Nif</c> (ADR-0004): <see cref="Intentar"/> para el borde y
/// <see cref="De"/> para cuando el valor ya viene comprobado.
/// </para>
/// </remarks>
public sealed record Permiso
{
    /// <summary>Separador de las tres partes.</summary>
    public const char Separador = '.';

    /// <summary>Cuántas partes tiene un permiso. No es una estimación.</summary>
    public const int Partes = 3;

    private Permiso(string valor, string modulo, string recurso, string accion) =>
        (Valor, Modulo, Recurso, Accion) = (valor, modulo, recurso, accion);

    /// <summary>El permiso entero, tal como viaja en el <i>claim</i> y en el atributo.</summary>
    public string Valor { get; }

    /// <summary>De qué módulo es la puerta.</summary>
    public string Modulo { get; }

    /// <summary>Sobre qué tipo se opera.</summary>
    public string Recurso { get; }

    /// <summary>Qué se le hace.</summary>
    public string Accion { get; }

    /// <summary>Construye el permiso, o lanza si la forma no es válida.</summary>
    /// <param name="valor">Texto del permiso.</param>
    public static Permiso De(string valor)
    {
        if (!Intentar(valor, out Permiso? permiso))
        {
            throw new ArgumentException(
                $"«{valor}» no tiene la forma modulo.recurso.accion en minúsculas y con guiones " +
                "(como ventas.pedido.confirmar).",
                nameof(valor));
        }

        return permiso;
    }

    /// <summary>Intenta construir el permiso sin lanzar.</summary>
    /// <param name="valor">Texto del permiso.</param>
    /// <param name="permiso">El permiso, si el texto era válido.</param>
    public static bool Intentar(string? valor, [NotNullWhen(true)] out Permiso? permiso)
    {
        permiso = null;

        if (string.IsNullOrWhiteSpace(valor))
        {
            return false;
        }

        string[] partes = valor.Split(Separador);

        if (partes.Length != Partes || Array.Exists(partes, parte => !EsRanuraEstable(parte)))
        {
            return false;
        }

        permiso = new Permiso(valor, partes[0], partes[1], partes[2]);
        return true;
    }

    /// <summary>El permiso entero, para cuando hace falta como texto.</summary>
    public override string ToString() => Valor;

    // Misma gramática que el código de `ErrorDeOperacion`: minúsculas ASCII, dígitos y guiones
    // sencillos, sin empezar ni acabar en guion. Se repite la regla en vez de compartirla porque
    // son dos contratos distintos que casualmente coinciden hoy; unirlos ataría el vocabulario
    // de los permisos al de los errores, y el día que uno cambie el otro cambiaría con él.
    private static bool EsRanuraEstable(string parte)
    {
        if (parte.Length == 0 || parte[0] == '-' || parte[^1] == '-')
        {
            return false;
        }

        bool guionPrevio = false;

        foreach (char caracter in parte)
        {
            if (caracter == '-')
            {
                if (guionPrevio)
                {
                    return false;
                }

                guionPrevio = true;
                continue;
            }

            if (!char.IsAsciiLetterLower(caracter) && !char.IsAsciiDigit(caracter))
            {
                return false;
            }

            guionPrevio = false;
        }

        return true;
    }
}
