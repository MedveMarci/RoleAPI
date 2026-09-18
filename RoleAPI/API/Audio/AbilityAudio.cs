using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using RoleAPI.API.Resources;
using RoleAPI.ApiFeatures;
using SecretLabNAudio.Core;
using SecretLabNAudio.Core.Extensions;
using SecretLabNAudio.Core.FileReading;
using SecretLabNAudio.Core.Processors;

namespace RoleAPI.API.Audio;

public sealed class AbilityAudio
{
    private static readonly Dictionary<Assembly, string> OverrideDirectories = new();
    private static readonly object OverrideLock = new();

    /// <summary>Gets the file path, or the embedded resource name when <see cref="IsEmbedded" /> is <c>true</c>.</summary>
    public string Identifier { get; }

    /// <summary>Gets the assembly the embedded resource is read from, or <see langword="null" /> for files.</summary>
    public Assembly? Assembly { get; }

    /// <summary>Gets a value indicating whether this audio is read from an embedded resource.</summary>
    public bool IsEmbedded => Assembly != null;

    /// <summary>Gets a short name for this audio, used in player-facing messages and logs.</summary>
    public string DisplayName => IsEmbedded ? Identifier : Path.GetFileName(Identifier);

    /// <summary>Gets the file type (extension without the dot) used to pick an audio reader, e.g. <c>ogg</c>.</summary>
    public string FileType => Path.GetExtension(Identifier).TrimStart('.').ToLowerInvariant();

    /// <summary>
    ///     Gets the plain file name of this audio, e.g. <c>beep.ogg</c> for the resource <c>MyPlugin.Audio.beep.ogg</c>.
    ///     This is the name looked up in the override directory.
    /// </summary>
    public string FileName
    {
        get
        {
            if (!IsEmbedded)
                return Path.GetFileName(Identifier);

            string normalized = Identifier.Replace('/', '.').Replace('\\', '.');
            int extension = normalized.LastIndexOf('.');
            if (extension <= 0)
                return normalized;

            int name = normalized.LastIndexOf('.', extension - 1);
            return name < 0 ? normalized : normalized.Substring(name + 1);
        }
    }

    private AbilityAudio(string identifier, Assembly? assembly)
    {
        Identifier = identifier;
        Assembly = assembly;
    }

    /// <summary>
    ///     Sets the directory searched for replacement audio files before embedded resources of the given assembly are
    ///     used. Lets server owners swap a plugin's built-in sounds by dropping a file with the same name into it.
    /// </summary>
    /// <param name="directory">The directory to search, or <see langword="null" /> to disable overrides.</param>
    /// <param name="assembly">The assembly whose embedded audio can be overridden.</param>
    public static void SetOverrideDirectory(string? directory, Assembly assembly)
    {
        if (assembly == null)
            throw new ArgumentNullException(nameof(assembly));

        lock (OverrideLock)
        {
            if (string.IsNullOrEmpty(directory))
                OverrideDirectories.Remove(assembly);
            else
                OverrideDirectories[assembly] = directory!;
        }

        LogManager.Debug(string.IsNullOrEmpty(directory) ? $"Cleared audio override directory for {assembly.GetName().Name}." : $"Audio override directory for {assembly.GetName().Name}: {directory}");
    }

    /// <inheritdoc cref="SetOverrideDirectory(string?, Assembly)" />
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void SetOverrideDirectory(string? directory)
    {
        SetOverrideDirectory(directory, Assembly.GetCallingAssembly());
    }

    /// <summary>Gets the override directory registered for the given assembly, if any.</summary>
    public static string? GetOverrideDirectory(Assembly assembly)
    {
        lock (OverrideLock)
        {
            return assembly != null && OverrideDirectories.TryGetValue(assembly, out string? directory) ? directory : null;
        }
    }

    /// <summary>
    ///     Gets the path of the file that replaces this embedded audio, or <see langword="null" /> when there is no
    ///     override directory for the assembly or it holds no file with this audio's <see cref="FileName" />.
    /// </summary>
    public string? ResolveOverrideFile()
    {
        if (Assembly == null)
            return null;

        string? directory = GetOverrideDirectory(Assembly);
        if (string.IsNullOrEmpty(directory))
            return null;

        try
        {
            string path = Path.Combine(directory!, FileName);
            return System.IO.File.Exists(path) ? path : null;
        }
        catch (Exception ex)
        {
            LogManager.Error($"Error resolving audio override for '{Identifier}'\n{ex}");
            return null;
        }
    }

    /// <summary>Creates an audio source that reads from a file on disk.</summary>
    /// <param name="path">The full path to the audio file.</param>
    public static AbilityAudio File(string path)
    {
        if (string.IsNullOrEmpty(path))
            throw new ArgumentException("Audio file path must not be empty.", nameof(path));

        return new AbilityAudio(path, null);
    }

    /// <summary>
    ///     Creates an audio source that reads from an embedded resource of the calling assembly.
    ///     See <see cref="EmbeddedResources" /> for how resource names are matched and how to embed files.
    /// </summary>
    /// <param name="resourceName">The resource name or a unique trailing part of it, e.g. <c>"Audio/beep.ogg"</c>.</param>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static AbilityAudio Embedded(string resourceName)
    {
        return Embedded(resourceName, Assembly.GetCallingAssembly());
    }

    /// <summary>Creates an audio source that reads from an embedded resource of the given assembly.</summary>
    /// <param name="resourceName">The resource name or a unique trailing part of it, e.g. <c>"Audio/beep.ogg"</c>.</param>
    /// <param name="assembly">The assembly holding the resource.</param>
    public static AbilityAudio Embedded(string resourceName, Assembly assembly)
    {
        if (string.IsNullOrEmpty(resourceName))
            throw new ArgumentException("Embedded resource name must not be empty.", nameof(resourceName));

        return new AbilityAudio(resourceName, assembly ?? throw new ArgumentNullException(nameof(assembly)));
    }

    /// <summary>
    ///     Returns whether the underlying file, override file or embedded resource can be found.
    /// </summary>
    public bool Exists()
    {
        if (Assembly == null)
            return System.IO.File.Exists(Identifier);

        return ResolveOverrideFile() != null || EmbeddedResources.ResolveName(Identifier, Assembly) != null;
    }

    /// <summary>Loads the audio into the given player.</summary>
    /// <param name="player">The player to load the audio into.</param>
    /// <param name="error">The reason loading failed, if it did.</param>
    /// <returns>Whether the audio was loaded successfully.</returns>
    internal bool TryApply(AudioPlayer player, out string? error)
    {
        error = null;
        try
        {
            if (Assembly == null)
            {
                if (!System.IO.File.Exists(Identifier))
                {
                    error = $"Sound file not found: {Identifier}";
                    return false;
                }

                player.UseFileSafe(Identifier);
                return true;
            }

            string? overrideFile = ResolveOverrideFile();
            if (overrideFile != null)
            {
                LogManager.Debug($"Using override file '{overrideFile}' instead of embedded '{Identifier}'.");
                player.UseFileSafe(overrideFile);
                return true;
            }

            Stream? stream = EmbeddedResources.OpenStreamCore(Identifier, Assembly);
            if (stream == null)
            {
                error = $"Embedded resource '{Identifier}' not found in {Assembly.GetName().Name}.";
                return false;
            }

            if (!TryCreateAudioProcessor.FromStream(stream, FileType, true, out StreamAudioProcessor? processor))
            {
                stream.Dispose();
                error = $"No audio reader is installed for '{FileType}' files ({Identifier}).";
                return false;
            }

            player.Use(processor);
            return true;
        }
        catch (Exception ex)
        {
            LogManager.Error($"Error loading audio '{Identifier}'\n{ex}");
            error = $"Failed to load audio: {DisplayName}";
            return false;
        }
    }
}