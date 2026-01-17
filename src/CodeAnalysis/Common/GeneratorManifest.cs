using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Comptatata.CodeAnalysis.Common;

/// <summary>
/// Tracks generated files for a source generator to enable incremental rename detection.
/// Stored in obj/ directory and regenerated on clean builds.
/// Uses simple text format for netstandard2.0 compatibility.
/// </summary>
public sealed class GeneratorManifest
{
    private const string ManifestFileName = "generator-manifest.txt";
    
    /// <summary>
    /// Maps source file paths to their generated file paths.
    /// </summary>
    public Dictionary<string, GeneratedFileInfo> Entries { get; } = new Dictionary<string, GeneratedFileInfo>(StringComparer.Ordinal);

    /// <summary>
    /// Loads or creates a manifest for the given generator in the project's obj directory.
    /// </summary>
    public static GeneratorManifest LoadOrCreate(string generatorName, string projectDirectory, string headerSignature, string? projectName = null)
    {
        var manifestPath = GetManifestPath(generatorName, projectDirectory, projectName);
        var manifest = new GeneratorManifest();
        
        if (File.Exists(manifestPath))
        {
            try
            {
                // Simple text format: each line is "sourcePath|generatedPath|symbol1,symbol2,..."
                foreach (var line in File.ReadAllLines(manifestPath))
                {
                    var parts = line.Split('|');
                    if (parts.Length >= 2)
                    {
                        var symbols = parts.Length >= 3 ? parts[2].Split(',').ToList() : new List<string>();
                        manifest.Entries[parts[0]] = new GeneratedFileInfo(parts[1], symbols);
                    }
                }
                return manifest;
            }
            catch
            {
                // Fall through to bootstrap
            }
        }
        
        // Bootstrap from existing generated files
        manifest.BootstrapFromExistingFiles(projectDirectory, headerSignature);
        return manifest;
    }

    /// <summary>
    /// Saves the manifest to the project's obj directory.
    /// </summary>
    public void Save(string generatorName, string projectDirectory, string? projectName = null)
    {
        var manifestPath = GetManifestPath(generatorName, projectDirectory, projectName);
        var directory = Path.GetDirectoryName(manifestPath);
        
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var lines = Entries.Select(e => $"{e.Key}|{e.Value.GeneratedFilePath}|{string.Join(",", e.Value.SymbolNames)}");
        File.WriteAllLines(manifestPath, lines);
    }

    /// <summary>
    /// Records a generated file in the manifest.
    /// </summary>
    public void RecordGeneration(string sourceFilePath, string generatedFilePath, IEnumerable<string> symbolNames)
    {
        Entries[sourceFilePath] = new GeneratedFileInfo(generatedFilePath, symbolNames.ToList());
    }

    /// <summary>
    /// Gets the previously recorded generated file path for a source file.
    /// </summary>
    public string? GetPreviousGeneratedPath(string sourceFilePath)
    {
        return Entries.TryGetValue(sourceFilePath, out var info) ? info.GeneratedFilePath : null;
    }

    /// <summary>
    /// Removes an entry from the manifest and optionally deletes the generated file.
    /// </summary>
    public void RemoveEntry(string sourceFilePath, bool deleteFile = true)
    {
        if (Entries.TryGetValue(sourceFilePath, out var info))
        {
            if (deleteFile && File.Exists(info.GeneratedFilePath))
            {
                try { File.Delete(info.GeneratedFilePath); } catch { }
            }
            Entries.Remove(sourceFilePath);
        }
    }

    /// <summary>
    /// Handles file rename by updating manifest and renaming/deleting old generated file.
    /// </summary>
    public void HandleFileRename(string oldSourcePath, string newSourcePath, string newGeneratedPath)
    {
        if (Entries.TryGetValue(oldSourcePath, out var oldInfo))
        {
            // Delete old generated file if it exists and differs from new path
            if (oldInfo.GeneratedFilePath != newGeneratedPath && File.Exists(oldInfo.GeneratedFilePath))
            {
                try { File.Delete(oldInfo.GeneratedFilePath); } catch { }
            }
            Entries.Remove(oldSourcePath);
        }
    }

    /// <summary>
    /// Cleans up entries that no longer have corresponding registrations.
    /// </summary>
    public void CleanupStaleEntries(HashSet<string> currentSourceFiles)
    {
        var staleKeys = Entries.Keys.Where(k => !currentSourceFiles.Contains(k)).ToList();
        foreach (var key in staleKeys)
        {
            RemoveEntry(key, deleteFile: true);
        }
    }

    private static string GetManifestPath(string generatorName, string projectDirectory, string? projectName = null)
    {
        var artifactsRoot = FindArtifactsRoot(projectDirectory);
        var manifestFileName = (projectName ?? Path.GetFileName(projectDirectory)) + "." + ManifestFileName;
        if (artifactsRoot != null)
        {
            return Path.Combine(artifactsRoot, "obj", "generators", generatorName, manifestFileName);
        }
        
        return Path.Combine(projectDirectory, "obj", manifestFileName);
    }

    private static string? FindArtifactsRoot(string projectDirectory)
    {
        var current = projectDirectory;
        while (!string.IsNullOrEmpty(current))
        {
            var artifactsPath = Path.Combine(current, ".artifacts");
            if (Directory.Exists(artifactsPath))
            {
                return artifactsPath;
            }
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
            if (Directory.GetFiles(current, "*.csproj").Any())
            {
                return current;
            }
            current = Path.GetDirectoryName(current);
        }
        return null;
    }

    private void BootstrapFromExistingFiles(string projectDirectory, string signature)
    {
        try
        {
            foreach (var generatedFile in Directory.GetFiles(projectDirectory, "*.generated.cs", SearchOption.AllDirectories))
            {
                // Verify ownership by checking the header signature
                var isOwned = false;
                try
                {
                    using var reader = new StreamReader(generatedFile);
                    var firstLine = reader.ReadLine();
                    if (firstLine != null && firstLine.Contains(signature))
                    {
                        isOwned = true;
                    }
                }
                catch { }

                if (isOwned)
                {
                    // Map back to source file based on naming convention
                    var sourceFile = generatedFile.Replace(".generated.cs", ".cs");
                    
                    // Add to entries even if source file doesn't exist (it might have been deleted/renamed)
                    // This allows CleanupStaleEntries to detect and remove it.
                    Entries[sourceFile] = new GeneratedFileInfo(generatedFile, new List<string>());
                }
            }
        }
        catch { }
    }
}

public sealed class GeneratedFileInfo
{
    public string GeneratedFilePath { get; }
    public List<string> SymbolNames { get; }

    public GeneratedFileInfo(string generatedFilePath, List<string> symbolNames)
    {
        GeneratedFilePath = generatedFilePath;
        SymbolNames = symbolNames;
    }
}
