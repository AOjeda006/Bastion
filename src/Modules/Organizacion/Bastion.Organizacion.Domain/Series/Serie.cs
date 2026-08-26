using Bastion.BuildingBlocks.Domain.Multiempresa;
namespace Bastion.Organizacion.Domain.Series;

/// <summary>
/// Serie documental: la que numera los documentos de una empresa en un ejercicio (R5).
/// </summary>
/// <remarks>
/// <para>
/// <b>El contador es una columna de esta fila, no una secuencia de PostgreSQL.</b> Es una
/// decisión de esquema, y de las que no tienen segunda oportunidad: <c>nextval</c> NO se
/// revierte al deshacer la transacción, así que una confirmación que falla dejaría un hueco
/// permanente en la numeración. R5 dice «correlativa y sin huecos», y eso descarta la secuencia.
/// </para>
/// <para>
/// La asignación del número —bloquear esta fila dentro de la transacción de confirmación,
/// incrementar y componer— es del módulo de Facturación (fase 5). Aquí vive el dato, no el
/// procedimiento; lo único que el dominio impide desde hoy es que el contador salte.
/// </para>
/// </remarks>
public sealed class Serie : IDeInquilino
{
    /// <summary>
    /// Tope del código. El <c>NumSerieFactura</c> de Veri*factu admite 60 caracteres para
    /// serie MÁS número; este tope deja sitio al número, al año y al separador.
    /// </summary>
    public const int LongitudMaximaDeCodigo = 20;

    private Serie(
        Guid id,
        Guid empresaId,
        Guid ejercicioId,
        TipoDeDocumento tipoDeDocumento,
        string codigo,
        string formato)
    {
        Id = id;
        EmpresaId = empresaId;
        EjercicioId = ejercicioId;
        TipoDeDocumento = tipoDeDocumento;
        Codigo = codigo;
        Formato = formato;
        Contador = 0;
        Estado = EstadoDeSerie.Activa;
    }

    private Serie()
    {
        Codigo = null!;
        Formato = null!;
    }

    /// <summary>Identificador de la serie.</summary>
    public Guid Id { get; private set; }

    /// <summary>Empresa a la que pertenece (R8).</summary>
    public Guid EmpresaId { get; private set; }

    /// <summary>Ejercicio al que pertenece: R5 numera por serie Y ejercicio.</summary>
    public Guid EjercicioId { get; private set; }

    /// <summary>Clase de documento que numera.</summary>
    public TipoDeDocumento TipoDeDocumento { get; private set; }

    /// <summary>Código de la serie, en mayúsculas.</summary>
    public string Codigo { get; private set; }

    /// <summary>Plantilla con la que se compone el número completo.</summary>
    public string Formato { get; private set; }

    /// <summary>Último número asignado. Cero mientras no haya numerado nada.</summary>
    public long Contador { get; private set; }

    /// <summary>Activa o cerrada.</summary>
    public EstadoDeSerie Estado { get; private set; }

    /// <summary>
    /// Una serie solo se puede suprimir mientras no haya numerado nada. Después, borrarla
    /// dejaría documentos legales apuntando a una serie inexistente.
    /// </summary>
    public bool SePuedeSuprimir => Contador == 0;

    /// <summary>Crea una serie activa con el contador a cero.</summary>
    public static Serie Crear(
        Guid empresaId,
        Guid ejercicioId,
        TipoDeDocumento tipoDeDocumento,
        string codigo,
        string formato)
    {
        if (empresaId == Guid.Empty)
        {
            throw new ArgumentException(
                "Una serie pertenece siempre a una empresa (R8).", nameof(empresaId));
        }

        if (ejercicioId == Guid.Empty)
        {
            throw new ArgumentException(
                "Una serie numera por serie Y ejercicio (R5): sin ejercicio no hay correlatividad " +
                "que garantizar.", nameof(ejercicioId));
        }

        return new Serie(
            Guid.CreateVersion7(),
            empresaId,
            ejercicioId,
            tipoDeDocumento,
            CodigoValido(codigo),
            FormatoValido(formato));
    }

    /// <summary>Cambia el formato de composición del número.</summary>
    /// <remarks>
    /// El código NO se puede cambiar: aparece en los documentos ya emitidos.
    /// </remarks>
    public void Modificar(string formato)
    {
        ExigirQueEsteActiva();
        Formato = FormatoValido(formato);
    }

    /// <summary>
    /// Anota que la serie ha entregado un número. Lo llama Facturación, dentro de la misma
    /// transacción en la que confirma el documento.
    /// </summary>
    public void RegistrarNumeroAsignado(long numero)
    {
        ExigirQueEsteActiva();

        // Última defensa antes de un libro registro inválido: aunque quien llame se equivoque,
        // el dominio no deja pasar un salto. Un hueco en la numeración no se arregla después.
        if (numero != Contador + 1)
        {
            throw new InvalidOperationException(
                $"La serie {Codigo} va por el {Contador} y se le pide anotar el {numero}: " +
                "R5 exige numeración correlativa y sin huecos.");
        }

        Contador = numero;
    }

    /// <summary>Cierra la serie: deja de numerar, y conserva el contador donde está.</summary>
    public void Cerrar() => Estado = EstadoDeSerie.Cerrada;

    /// <summary>Reabre la serie.</summary>
    public void Reabrir() => Estado = EstadoDeSerie.Activa;

    private void ExigirQueEsteActiva()
    {
        if (Estado == EstadoDeSerie.Cerrada)
        {
            throw new InvalidOperationException(
                $"La serie {Codigo} está cerrada y no admite más operaciones.");
        }
    }

    /// <summary>Deja el código en la forma exacta en la que se guarda.</summary>
    /// <remarks>
    /// Pública a propósito: sobre esta forma hay un índice único, y quien comprueba si el código ya
    /// existe ANTES de insertar tiene que preguntar por ella. Preguntando por lo que escribió el
    /// usuario, «fac» pasaría el filtro, chocaría contra el índice y saldría como un 500 en vez de
    /// como un 409 con explicación. No valida nada —de la longitud se encarga la creación—: una
    /// pregunta no tiene por qué reventar.
    /// </remarks>
    /// <param name="codigo">Código tal como lo escribieron.</param>
    public static string NormalizarCodigo(string codigo)
    {
        ArgumentNullException.ThrowIfNull(codigo);

        return codigo.Trim().ToUpperInvariant();
    }

    private static string CodigoValido(string codigo)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(codigo);

        string normalizado = NormalizarCodigo(codigo);

        return normalizado.Length <= LongitudMaximaDeCodigo
            ? normalizado
            : throw new ArgumentException(
                $"El código de serie admite {LongitudMaximaDeCodigo} caracteres como máximo: el " +
                $"NumSerieFactura de Veri*factu tiene 60 para serie y número juntos.", nameof(codigo));
    }

    private static string FormatoValido(string formato)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(formato);
        return formato.Trim();
    }
}
