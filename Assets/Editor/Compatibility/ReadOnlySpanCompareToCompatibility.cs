#if UNITY_EDITOR
using System;

/// <summary>
/// Compatibilidade para versões experimentais do Unity/.NET que expõem apenas a
/// sobrecarga de comparação de ReadOnlySpan com StringComparison obrigatório.
/// Permite que código legado usando span.CompareTo(outroSpan) continue compilando
/// com comparação ordinal determinística.
/// </summary>
internal static class ReadOnlySpanCompareToCompatibility
{
    public static int CompareTo(this ReadOnlySpan<char> left, ReadOnlySpan<char> right)
    {
        return MemoryExtensions.CompareTo(left, right, StringComparison.Ordinal);
    }
}
#endif
