using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using RoleAPI.ApiFeatures;

namespace RoleAPI.API.Resources;

public static class EmbeddedResources
{
    private static readonly Dictionary<Assembly, Dictionary<string, string?>> ResolvedNames = new();
    private static readonly Dictionary<Assembly, Dictionary<string, byte[]>> CachedBytes = new();
    private static readonly object SyncRoot = new();
    
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static IReadOnlyList<string> GetNames(Assembly? assembly = null)
    {
        return (assembly ?? Assembly.GetCallingAssembly()).GetManifestResourceNames();
    }
    
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool TryResolveName(string name, out string? fullName, Assembly? assembly = null)
    {
        fullName = ResolveName(name, assembly ?? Assembly.GetCallingAssembly());
        return fullName != null;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool Exists(string name, Assembly? assembly = null)
    {
        return ResolveName(name, assembly ?? Assembly.GetCallingAssembly()) != null;
    }
    
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static byte[]? GetBytes(string name, Assembly? assembly = null)
    {
        return GetBytesCore(name, assembly ?? Assembly.GetCallingAssembly());
    }
    
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static Stream? OpenStream(string name, Assembly? assembly = null)
    {
        return OpenStreamCore(name, assembly ?? Assembly.GetCallingAssembly());
    }
    
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static string? GetText(string name, Assembly? assembly = null)
    {
        var bytes = GetBytesCore(name, assembly ?? Assembly.GetCallingAssembly());
        if (bytes == null)
            return null;

        using var reader = new StreamReader(new MemoryStream(bytes, false), true);
        return reader.ReadToEnd();
    }
    
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool ExtractToFile(string name, string destinationPath, bool overwrite = false,
        Assembly? assembly = null)
    {
        var source = assembly ?? Assembly.GetCallingAssembly();
        try
        {
            if (File.Exists(destinationPath) && !overwrite)
                return true;

            var bytes = GetBytesCore(name, source);
            if (bytes == null)
            {
                LogManager.Warn($"Embedded resource '{name}' not found in {source.GetName().Name}.");
                return false;
            }

            var directory = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory!);

            File.WriteAllBytes(destinationPath, bytes);
            return true;
        }
        catch (Exception ex)
        {
            LogManager.Error($"Error extracting embedded resource '{name}' to '{destinationPath}'\n{ex}");
            return false;
        }
    }
    
    public static void ClearCache(Assembly? assembly = null)
    {
        lock (SyncRoot)
        {
            if (assembly == null)
            {
                CachedBytes.Clear();
                ResolvedNames.Clear();
                return;
            }

            CachedBytes.Remove(assembly);
            ResolvedNames.Remove(assembly);
        }
    }

    internal static Stream? OpenStreamCore(string name, Assembly assembly)
    {
        var bytes = GetBytesCore(name, assembly);
        return bytes == null ? null : new MemoryStream(bytes, false);
    }

    internal static byte[]? GetBytesCore(string name, Assembly assembly)
    {
        var fullName = ResolveName(name, assembly);
        if (fullName == null)
            return null;

        lock (SyncRoot)
        {
            if (!CachedBytes.TryGetValue(assembly, out var cache))
                CachedBytes[assembly] = cache = new Dictionary<string, byte[]>(StringComparer.Ordinal);

            if (cache.TryGetValue(fullName, out var cached))
                return cached;

            try
            {
                using var stream = assembly.GetManifestResourceStream(fullName);
                if (stream == null)
                    return null;

                var buffer = new byte[stream.Length];
                var read = 0;
                while (read < buffer.Length)
                {
                    var count = stream.Read(buffer, read, buffer.Length - read);
                    if (count <= 0)
                        break;
                    read += count;
                }

                cache[fullName] = buffer;
                LogManager.Debug($"Loaded embedded resource '{fullName}' ({buffer.Length} bytes) from " +
                                 $"{assembly.GetName().Name}.");
                return buffer;
            }
            catch (Exception ex)
            {
                LogManager.Error($"Error reading embedded resource '{fullName}'\n{ex}");
                return null;
            }
        }
    }

    internal static string? ResolveName(string name, Assembly assembly)
    {
        if (string.IsNullOrEmpty(name))
            return null;

        var normalized = name.Replace('/', '.').Replace('\\', '.');

        lock (SyncRoot)
        {
            if (!ResolvedNames.TryGetValue(assembly, out var lookup))
                ResolvedNames[assembly] = lookup = new Dictionary<string, string?>(StringComparer.Ordinal);

            if (lookup.TryGetValue(normalized, out var cached))
                return cached;

            var resolved = ResolveUncached(normalized, assembly);
            lookup[normalized] = resolved;
            return resolved;
        }
    }

    private static string? ResolveUncached(string normalized, Assembly assembly)
    {
        string[] names;
        try
        {
            names = assembly.GetManifestResourceNames();
        }
        catch (Exception ex)
        {
            LogManager.Error($"Error listing embedded resources of {assembly.GetName().Name}\n{ex}");
            return null;
        }

        foreach (var candidate in names)
            if (string.Equals(candidate, normalized, StringComparison.Ordinal))
                return candidate;

        foreach (var candidate in names)
            if (string.Equals(candidate, normalized, StringComparison.OrdinalIgnoreCase))
                return candidate;

        var suffix = "." + normalized;
        string? match = null;
        foreach (var candidate in names)
        {
            if (!candidate.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                continue;

            if (match != null)
            {
                LogManager.Warn($"Embedded resource name '{normalized}' is ambiguous in " +
                                $"{assembly.GetName().Name}: matches '{match}' and '{candidate}'. Using '{match}'.");
                return match;
            }

            match = candidate;
        }

        return match;
    }
}
