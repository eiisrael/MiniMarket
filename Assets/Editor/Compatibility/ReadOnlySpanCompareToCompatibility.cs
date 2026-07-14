#if UNITY_EDITOR
using System;
using UnityEngine.SceneManagement;

/// <summary>
/// Compatibilidade para versões experimentais do Unity/.NET.
/// Adiciona comparações determinísticas para tipos que não implementam CompareTo
/// diretamente nas versões alpha do Unity 6.
/// </summary>
internal static class UnityAlphaCompareToCompatibility
{
    public static int CompareTo(this ReadOnlySpan<char> left, ReadOnlySpan<char> right)
    {
        return MemoryExtensions.CompareTo(left, right, StringComparison.Ordinal);
    }

    public static int CompareTo(this SceneHandle left, SceneHandle right)
    {
        return left.GetRawData().CompareTo(right.GetRawData());
    }
}
#endif
