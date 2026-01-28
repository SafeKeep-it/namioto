namespace Comptatata.CodeAnalysis.Common;

/// <summary>
///     Tracks generated files for a source generator to enable incremental rename detection.
///     Stored in obj/ directory and regenerated on clean builds.
///     Uses simple text format for netstandard2.0 compatibility.
/// </summary>
public sealed class GeneratorManifest
{
    const string ManifestFileName = "generator-manifest.txt";

    /// <summary>
    ///     Maps source file paths to their generated file paths.
    /// </summary>
    public Dictionary<string, GeneratedFileInfo> Entries { get; } = new(StringComparer.Ordinal);

    /// <summary>
    ///     Loads or creates a manifest for the given generator in the project's obj directory.
    /// </summary>
    public static GeneratorManifest LoadOrCreate(string generatorName,
                                                 string projectDirectory,
                                                 string headerSignature,
                                                 string? projectName = null)
    {
        var manifestPath = GetManifestPath(generatorName, projectDirectory, projectName);
        var manifest = new GeneratorManifest();

        if (File.Exists(manifestPath))
            try
            {
                // Simple text format: each line is "sourcePath|generatedPath|symbol1,symbol2,..."
                foreach (var line in File.ReadAllLines(manifestPath))
                {
                    var parts = line.Split('|');
                    if (parts.Length >= 2)
                    {
                        var symbols = parts.Length >= 3 ? parts[2].Split(',').ToList() : new();
                        manifest.Entries[parts[0]] = new(parts[1], symbols);
                    }
                }

                return manifest;
            }
            catch
            {
                // Fall through to bootstrap
            }

        // Bootstrap from existing generated files
        manifest.BootstrapFromExistingFiles(projectDirectory, headerSignature);
        return manifest;
    }

    /// <summary>
    ///     Saves the manifest to the project's obj directory.
    /// </summary>
    public void Save(string generatorName, string projectDirectory, string? projectName = null)
    {
        var manifestPath = GetManifestPath(generatorName, projectDirectory, projectName);
        var directory = Path.GetDirectoryName(manifestPath);

        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory)) Directory.CreateDirectory(directory);

        var lines = Entries.Select(e => $"{e.Key}|{e.Value.GeneratedFilePath}|{string.Join(",", e.Value.SymbolNames)}");
        File.WriteAllLines(manifestPath, lines);
    }

    /// <summary>
    ///     Records a generated file in the manifest.
    /// </summary>
    public void RecordGeneration(string sourceFilePath, string generatedFilePath, IEnumerable<string> symbolNames)
    {
        var normalizedSourcePath = Path.GetFullPath(sourceFilePath);
        var normalizedGeneratedPath = Path.GetFullPath(generatedFilePath);
        Entries[normalizedSourcePath] = new(normalizedGeneratedPath, symbolNames.ToList());
    }

    /// <summary>
    ///     Gets the previously recorded generated file path for a source file.
    /// </summary>
    public string? GetPreviousGeneratedPath(string sourceFilePath)
    {
        var normalizedPath = Path.GetFullPath(sourceFilePath);
        return Entries.TryGetValue(normalizedPath, out var info) ? info.GeneratedFilePath : null;
    }

    /// <summary>
    ///     Removes an entry from the manifest and optionally deletes the generated file.
    /// </summary>
    public void RemoveEntry(string sourceFilePath, bool deleteFile = true)
    {
        var normalizedPath = Path.GetFullPath(sourceFilePath);
        if (Entries.TryGetValue(normalizedPath, out var info))
        {
            if (deleteFile && File.Exists(info.GeneratedFilePath))
                try
                {
                    File.Delete(info.GeneratedFilePath);
                }
                catch { }

            Entries.Remove(normalizedPath);
        }
    }

    /// <summary>
    ///     Handles file rename by updating manifest and renaming/deleting old generated file.
    /// </summary>
    public void HandleFileRename(string oldSourcePath, string newSourcePath, string newGeneratedPath)
    {
        var normalizedOldPath = Path.GetFullPath(oldSourcePath);
        var normalizedNewGeneratedPath = Path.GetFullPath(newGeneratedPath);
        if (Entries.TryGetValue(normalizedOldPath, out var oldInfo))
        {
            // Delete old generated file if it exists and differs from new path
            if (oldInfo.GeneratedFilePath != normalizedNewGeneratedPath && File.Exists(oldInfo.GeneratedFilePath))
                try
                {
                    File.Delete(oldInfo.GeneratedFilePath);
                }
                catch { }

            Entries.Remove(normalizedOldPath);
        }
    }

    /// <summary>
    ///     Cleans up entries that no longer have corresponding registrations.
    ///     Only removes entries from the manifest - does NOT delete files from disk.
    ///     File deletion is disabled to prevent race conditions where the generator
    ///     runs with incomplete information (e.g., during incremental compilation)
    ///     and incorrectly deletes files that are still needed.
    /// </summary>
    public void CleanupStaleEntries(HashSet<string> currentSourceFiles)
    {
        // If no source files were found, don't do anything - this likely means
        // the generator ran in a context where it couldn't find registrations
        // (e.g., during incremental compilation or run without rebuild)
        if (currentSourceFiles.Count == 0) return;

        // Only remove from manifest tracking, do NOT delete files from disk.
        // Deleting files causes race conditions with MSBuild file enumeration.
        var staleKeys = Entries.Keys.Where(k => !currentSourceFiles.Contains(k)).ToList();
        foreach (var key in staleKeys) RemoveEntry(key, false);
    }

    static string GetManifestPath(string generatorName, string projectDirectory, string? projectName = null)
    {
        var artifactsRoot = FindArtifactsRoot(projectDirectory);
        var manifestFileName = (projectName ?? Path.GetFileName(projectDirectory)) + "." + ManifestFileName;
        if (artifactsRoot != null)
            return Path.Combine(artifactsRoot, "obj", "generators", generatorName, manifestFileName);

        return Path.Combine(projectDirectory, "obj", manifestFileName);
    }

    static string? FindArtifactsRoot(string projectDirectory)
    {
        var current = projectDirectory;
        while (!string.IsNullOrEmpty(current))
        {
            var artifactsPath = Path.Combine(current, ".artifacts");
            if (Directory.Exists(artifactsPath)) return artifactsPath;
            current = Path.GetDirectoryName(current);
        }

        return null;
    }

    public static string? FindProjectRoot(string? filePath)
    {
        if (string.IsNullOrEmpty(filePath)) return null;
        var current = Path.GetDirectoryName(filePath);
        while (!string.IsNullOrEmpty(current))
        {
            if (Directory.GetFiles(current, "*.csproj").Any()) return current;
            current = Path.GetDirectoryName(current);
        }

        return null;
    }

    void BootstrapFromExistingFiles(string projectDirectory, string signature)
    {
        try
        {
            foreach (var generatedFile in Directory.GetFiles(projectDirectory,
                                                             "*.generated.cs",
                                                             SearchOption.AllDirectories))
            {
                // Verify ownership by checking the header signature
                var isOwned = false;
                try
                {
                    using var reader = new StreamReader(generatedFile);
                    var firstLine = reader.ReadLine();
                    if (firstLine != null && firstLine.Contains(signature)) isOwned = true;
                }
                catch { }

                if (isOwned)
                {
                    // Map back to source file based on naming convention
                    var sourceFile = generatedFile.Replace(".generated.cs", ".cs");

                    // Normalize paths for consistent comparison
                    var normalizedSourceFile = Path.GetFullPath(sourceFile);
                    var normalizedGeneratedFile = Path.GetFullPath(generatedFile);

                    // Add to entries even if source file doesn't exist (it might have been deleted/renamed)
                    // This allows CleanupStaleEntries to detect and remove it.
                    Entries[normalizedSourceFile] = new(normalizedGeneratedFile, new());
                }
            }
        }
        catch { }
    }
}

public sealed class GeneratedFileInfo
{
    public GeneratedFileInfo(string generatedFilePath, List<string> symbolNames)
    {
        GeneratedFilePath = generatedFilePath;
        SymbolNames = symbolNames;
    }

    public string GeneratedFilePath { get; }
    public List<string> SymbolNames { get; }
}