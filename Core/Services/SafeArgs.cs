namespace PureUpdate.Core.Services;

/// <summary>Garde-fous sur ce qui est insere dans une ligne de commande ou une tache planifiee.</summary>
public static class SafeArgs
{
    private const string Forbidden = "\"'`&|;<>^%$";

    /// <summary>Leve ArgumentException si l'ID contient guillemet, metacaractere shell, espace ou caractere de controle.</summary>
    public static string PackageId(string id)
    {
        if (string.IsNullOrEmpty(id) || id.Any(c => char.IsWhiteSpace(c) || char.IsControl(c) || Forbidden.Contains(c)))
            throw new ArgumentException("Identifiant de paquet refuse (caracteres interdits).");
        return id;
    }

    /// <summary>Vrai si l'exe est sous %ProgramFiles% ou %ProgramFiles(x86)% (dossier inscriptible par les seuls administrateurs).</summary>
    public static bool IsUnderProgramFiles(string exePath)
    {
        if (string.IsNullOrWhiteSpace(exePath)) return false;
        string full;
        try { full = Path.GetFullPath(exePath); } catch (ArgumentException) { return false; }
        foreach (var f in new[] { Environment.SpecialFolder.ProgramFiles, Environment.SpecialFolder.ProgramFilesX86 })
        {
            var root = Environment.GetFolderPath(f);
            if (root.Length > 0 && full.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }
}
