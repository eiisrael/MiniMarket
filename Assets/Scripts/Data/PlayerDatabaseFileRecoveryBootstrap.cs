using System;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// Valida o arquivo do banco antes de qualquer Awake da cena.
/// Arquivos vazios, JSON inválido ou envelopes MMDB quebrados são movidos para
/// um backup e o MiniMarketPlayerDatabase pode criar um arquivo novo sem repetir o erro.
/// </summary>
public static class PlayerDatabaseFileRecoveryBootstrap
{
    private const string DatabaseFileName = "player_database.mmdb";
    private const string EncryptedPrefixV1 = "MMDB1:";
    private const string EncryptedPrefixV2 = "MMDB2:";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ValidateDatabaseFileBeforeSceneLoad()
    {
        string path = Path.Combine(Application.persistentDataPath, DatabaseFileName);
        if (!File.Exists(path))
            return;

        try
        {
            string content = File.ReadAllText(path, Encoding.UTF8);
            if (IsStructurallyValid(content))
                return;

            QuarantineCorruptedFile(path);
            Debug.LogWarning(
                "[PlayerDatabase] Arquivo local inválido foi movido para backup. " +
                "Um banco novo será criado automaticamente."
            );
        }
        catch (Exception exception)
        {
            Debug.LogWarning(
                "[PlayerDatabase] Não foi possível validar o arquivo local: " +
                exception.Message
            );
        }
    }

    private static bool IsStructurallyValid(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return false;

        string trimmed = content.Trim().TrimStart('\uFEFF');

        if (trimmed.StartsWith(EncryptedPrefixV1, StringComparison.Ordinal) ||
            trimmed.StartsWith(EncryptedPrefixV2, StringComparison.Ordinal))
        {
            return IsEncryptedEnvelopeValid(trimmed);
        }

        if (!trimmed.StartsWith("{", StringComparison.Ordinal))
            return false;

        try
        {
            MiniMarketPlayerDatabase.MiniMarketPlayerData data =
                JsonUtility.FromJson<MiniMarketPlayerDatabase.MiniMarketPlayerData>(trimmed);
            return data != null;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsEncryptedEnvelopeValid(string content)
    {
        string[] parts = content.Split(':');
        bool prefixoConhecido = parts.Length == 4 &&
                                (parts[0] == "MMDB1" || parts[0] == "MMDB2");
        if (!prefixoConhecido)
            return false;

        try
        {
            byte[] iv = Convert.FromBase64String(parts[1]);
            byte[] cipher = Convert.FromBase64String(parts[2]);
            byte[] signature = Convert.FromBase64String(parts[3]);

            return iv.Length == 16 &&
                   cipher.Length > 0 &&
                   cipher.Length % 16 == 0 &&
                   signature.Length == 32;
        }
        catch
        {
            return false;
        }
    }

    private static void QuarantineCorruptedFile(string path)
    {
        string backup = path + ".corrupt_" +
                        DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff") +
                        ".bak";

        try
        {
            File.Move(path, backup);
        }
        catch
        {
            File.Copy(path, backup, true);
            File.Delete(path);
        }
    }
}