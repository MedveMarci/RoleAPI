using System;
using System.Collections.Generic;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using LabApi.Features;

namespace RoleAPI.ApiFeatures;

public static class ApiManager
{
    private const string ApiBase = "https://bearmanapi.hu";

    private static readonly Dictionary<string, DateTime> AutoErrorLastSent = new();
    private static readonly TimeSpan DedupWindow = TimeSpan.FromSeconds(5);

    internal static void CheckForUpdates()
    {
        var name = RoleApiPlugin.Singleton.Name;
        var currentVersion = RoleApiPlugin.Singleton.Version;

        var resp = HttpQuery.Get($"{ApiBase}/api/v1/plugin/{Uri.EscapeDataString(name)}/latest");
        var (statusCode, message) = ParseApiResponse(resp);

        if (statusCode != HttpStatusCode.OK)
        {
            LogManager.Error($"Version check failed: {statusCode} - {message}");
            return;
        }

        var root = JsonDocument.Parse(resp).RootElement;

        if (!root.TryGetProperty("version", out var versionProp) || versionProp.ValueKind != JsonValueKind.String)
        {
            LogManager.Error("Version check failed: 'version' field missing or invalid.");
            return;
        }

        var version = versionProp.GetString();

        if (version == null || !Version.TryParse(version, out var latestRemoteVersion))
        {
            LogManager.Error("Version check failed: Invalid version format.");
            return;
        }

        var outdated = latestRemoteVersion > currentVersion;
        var currentIsNewerThanRemote = currentVersion > latestRemoteVersion;

        var currentVersionResp = HttpQuery.Get(
            $"{ApiBase}/api/v1/plugin/{Uri.EscapeDataString(name)}/version/{Uri.EscapeDataString(currentVersion.ToString())}");
        var (currentStatusCode, currentMessage) = ParseApiResponse(currentVersionResp);
        if (currentStatusCode != HttpStatusCode.OK)
            LogManager.Debug($"Recall check failed: {currentStatusCode} - {currentMessage}");

        var recallRoot = JsonDocument.Parse(currentVersionResp).RootElement;

        if (recallRoot.TryGetProperty("is_recalled", out var isRecalledProp) &&
            isRecalledProp.ValueKind == JsonValueKind.True)
        {
            var recallReason = recallRoot.TryGetProperty("recall_reason", out var reasonProp) &&
                               reasonProp.ValueKind == JsonValueKind.String
                ? reasonProp.GetString()
                : "No reason provided.";
            LogManager.Error(
                $"This version of {name} has been recalled.\nPlease update to {latestRemoteVersion} as soon as possible.\nReason: {recallReason}",
                ConsoleColor.DarkRed);
            return;
        }

        if (outdated)
            LogManager.Info(
                $"A new version of {name} is available: {version} (current {currentVersion}). {GetDownloadUrl(root)}",
                ConsoleColor.DarkRed);
        else
            LogManager.Info(
                $"Thanks for using {name} v{currentVersion}. Join my Discord: https://discord.gg/KmpA8cfaSA",
                ConsoleColor.Blue);

        if (!currentIsNewerThanRemote) return;
        LogManager.Info(
            $"You are running a newer version of {name} ({currentVersion}) than {latestRemoteVersion}. This is a development/pre-release build.",
            ConsoleColor.DarkMagenta);
    }

    internal static string SendLogsAsync(string content)
    {
        try
        {
            var url = $"{ApiBase}/api/v1/plugin/{Uri.EscapeDataString(RoleApiPlugin.Singleton.Name)}/log";
            LogManager.Info("Sending logs to BearmanAPI...", ConsoleColor.Green);

            var payload = new
            {
                content,
                plugin_version = RoleApiPlugin.Singleton.Version.ToString(),
                labapi_version = LabApiProperties.CurrentVersion,
                trigger = "manual"
            };
            var json = JsonSerializer.Serialize(payload);
            var resp = HttpQuery.Post(url, json, "application/json");
            var data = ParseApiResponse(resp);

            if (data.StatusCode != HttpStatusCode.Created)
            {
                LogManager.Error($"Failed to send logs: {data.StatusCode}");
                return null;
            }

            if (JsonDocument.Parse(resp).RootElement.TryGetProperty("log_id", out var logIdProp) &&
                logIdProp.ValueKind == JsonValueKind.String)
                return logIdProp.GetString();

            LogManager.Warn("Logs sent but no log_id returned.");
            return null;
        }
        catch (Exception e)
        {
            LogManager.Error($"Sending logs failed.\n{e}");
            return null;
        }
    }

    internal static void SendAutoError(string errorMessage)
    {
        Task.Run(() =>
        {
            try
            {
                if (RoleApiPlugin.Singleton?.Config == null) return;

                var hash = ComputeShortHash(errorMessage);

                lock (AutoErrorLastSent)
                {
                    if (AutoErrorLastSent.TryGetValue(hash, out var lastSent) &&
                        DateTime.UtcNow - lastSent < DedupWindow)
                        return;

                    AutoErrorLastSent[hash] = DateTime.UtcNow;

                    var cutoff = DateTime.UtcNow - TimeSpan.FromMinutes(5);
                    var toRemove = new List<string>();
                    foreach (var kv in AutoErrorLastSent)
                        if (kv.Value < cutoff)
                            toRemove.Add(kv.Key);
                    foreach (var k in toRemove)
                        AutoErrorLastSent.Remove(k);
                }

                var content = LogManager.BuildLogContent(errorMessage);
                var url = $"{ApiBase}/api/v1/plugin/{Uri.EscapeDataString(RoleApiPlugin.Singleton.Name)}/log";
                var payload = new
                {
                    content,
                    plugin_version = RoleApiPlugin.Singleton.Version?.ToString(),
                    labapi_version = LabApiProperties.CurrentVersion,
                    trigger = "auto_error"
                };
                var json = JsonSerializer.Serialize(payload);
                HttpQuery.Post(url, json, "application/json");
            }
            catch (Exception e)
            {
                LogManager.Debug($"SendAutoError failed: {e.Message}");
            }
        });
    }

    private static string ComputeShortHash(string input)
    {
        using var md5 = MD5.Create();
        var bytes = md5.ComputeHash(Encoding.UTF8.GetBytes(input));

        var sb = new StringBuilder(bytes.Length * 2);
        foreach (var b in bytes)
            sb.Append(b.ToString("X2"));

        return sb.ToString(0, 8);
    }

    private static string GetDownloadUrl(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object) return "";
        if (root.TryGetProperty("download_url", out var d) && d.ValueKind == JsonValueKind.String)
            return string.IsNullOrEmpty(d.GetString()) ? "" : $"Download: {d.GetString()}";
        return "";
    }

    private static (HttpStatusCode StatusCode, string Message) ParseApiResponse(string json)
    {
        try
        {
            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var statusCode = HttpStatusCode.InternalServerError;
            string message = null;

            if (root.TryGetProperty("status", out var statusProp) && statusProp.ValueKind == JsonValueKind.Number)
                statusCode = (HttpStatusCode)statusProp.GetInt32();

            if (root.TryGetProperty("message", out var messageProp) && messageProp.ValueKind == JsonValueKind.String)
                message = messageProp.GetString();

            return (statusCode, message);
        }
        catch (Exception e)
        {
            LogManager.Error("Failed to parse API response.");
            LogManager.Debug($"ParseApiResponse failed.\n{e}");
            return (HttpStatusCode.InternalServerError, null);
        }
    }
}