#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Auditor não destrutivo da organização de scripts.
///
/// A versão anterior movia, renomeava e removia arquivos/cenas automaticamente e foi
/// responsável por quebrar compra de terrenos, minimapa e diagnósticos. Esta versão
/// apenas gera relatório. Nenhum arquivo, componente, cena ou prefab é alterado.
/// </summary>
public static class ScriptProjectOrganizer
{
    private const string ScriptsRoot = "Assets/Scripts";
    private const string ReportPath = "Relatorios/AUDITORIA_SCRIPTS.md";

    [MenuItem("Tools/Project Maintenance/Generate Safe Script Organization Audit", priority = 1)]
    public static void GenerateAudit()
    {
        try
        {
            List<string> scripts = Directory.Exists(ScriptsRoot)
                ? Directory.GetFiles(ScriptsRoot, "*.cs", SearchOption.AllDirectories)
                    .Select(Normalize)
                    .Where(path => path.IndexOf("Brick Project Studio", StringComparison.OrdinalIgnoreCase) < 0)
                    .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                    .ToList()
                : new List<string>();

            List<string> legacyPrefix = scripts
                .Where(path => Path.GetFileNameWithoutExtension(path).StartsWith("MiniMarket", StringComparison.Ordinal))
                .ToList();

            List<string> rootScripts = scripts
                .Where(path => string.Equals(Normalize(Path.GetDirectoryName(path)), ScriptsRoot, StringComparison.OrdinalIgnoreCase))
                .ToList();

            Dictionary<string, List<string>> duplicateNames = scripts
                .GroupBy(path => Path.GetFileNameWithoutExtension(path), StringComparer.OrdinalIgnoreCase)
                .Where(group => group.Count() > 1)
                .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);

            StringBuilder report = new StringBuilder();
            report.AppendLine("# Auditoria segura de scripts");
            report.AppendLine();
            report.AppendLine("Gerado em: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            report.AppendLine();
            report.AppendLine("Esta auditoria não move, renomeia ou apaga arquivos.");
            report.AppendLine();
            report.AppendLine("## Resumo");
            report.AppendLine();
            report.AppendLine("- Scripts analisados: " + scripts.Count);
            report.AppendLine("- Arquivos na raiz de Assets/Scripts: " + rootScripts.Count);
            report.AppendLine("- Arquivos com prefixo MiniMarket: " + legacyPrefix.Count);
            report.AppendLine("- Nomes de arquivo duplicados: " + duplicateNames.Count);
            report.AppendLine();

            AppendList(report, "Scripts na raiz", rootScripts);
            AppendList(report, "Prefixo MiniMarket", legacyPrefix);

            report.AppendLine("## Nomes duplicados");
            report.AppendLine();
            if (duplicateNames.Count == 0)
            {
                report.AppendLine("- Nenhum.");
            }
            else
            {
                foreach (KeyValuePair<string, List<string>> pair in duplicateNames)
                {
                    report.AppendLine("### " + pair.Key);
                    for (int i = 0; i < pair.Value.Count; i++)
                        report.AppendLine("- `" + pair.Value[i] + "`");
                    report.AppendLine();
                }
            }

            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath) ?? "Relatorios");
            File.WriteAllText(ReportPath, report.ToString(), new UTF8Encoding(false));
            AssetDatabase.Refresh();

            Debug.Log("[ScriptProjectOrganizer] Auditoria segura concluída: " + ReportPath);
        }
        catch (Exception exception)
        {
            Debug.LogError("[ScriptProjectOrganizer] Falha ao gerar auditoria: " + exception);
        }
    }

    [MenuItem("Tools/Project Maintenance/Run Script Cleanup and Organization", priority = 2)]
    public static void LegacyMenuRedirect()
    {
        Debug.LogWarning(
            "[ScriptProjectOrganizer] A limpeza automática destrutiva foi desativada. " +
            "Use 'Generate Safe Script Organization Audit'."
        );
        GenerateAudit();
    }

    private static void AppendList(StringBuilder report, string title, List<string> items)
    {
        report.AppendLine("## " + title);
        report.AppendLine();

        if (items.Count == 0)
        {
            report.AppendLine("- Nenhum.");
        }
        else
        {
            for (int i = 0; i < items.Count; i++)
                report.AppendLine("- `" + items[i] + "`");
        }

        report.AppendLine();
    }

    private static string Normalize(string path)
    {
        return string.IsNullOrEmpty(path) ? string.Empty : path.Replace('\\', '/');
    }
}
#endif
