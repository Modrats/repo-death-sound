using System;
using System.Collections;
using System.IO;
using BepInEx;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Networking;

namespace DeathSound;

[BepInPlugin(PluginMetadata.Guid, PluginMetadata.Name, PluginMetadata.Version)]
public class DeathSound : BaseUnityPlugin
{
    private static DeathSound? _instance;
    private AudioClip? _deathClip;
    private Harmony? _harmony;

    private void Awake()
    {
        _instance = this;

        transform.SetParent(null);
        gameObject.hideFlags = HideFlags.HideAndDontSave;

        ApplyHarmonyPatches();
        StartCoroutine(LoadDeathClip());

        Logger.LogInfo($"{Info.Metadata.GUID} v{Info.Metadata.Version} has loaded!");
    }

    private void OnDestroy()
    {
        _harmony?.UnpatchSelf();
        PendingDeathSoundMutes.Clear();

        if (_deathClip != null)
        {
            Destroy(_deathClip);
        }

        if (ReferenceEquals(_instance, this))
        {
            _instance = null;
        }
    }

    private IEnumerator LoadDeathClip()
    {
        string pluginDirectory = Path.GetDirectoryName(Info.Location)!;
        string? deathClipPath = FindFirstWavFile(pluginDirectory);
        if (deathClipPath == null)
        {
            Logger.LogInfo($"No .wav file found in {pluginDirectory}. Drop any .wav there to replace the death sound; vanilla sound plays until then.");
            yield break;
        }

        var deathClipUri = new Uri(deathClipPath);
        using UnityWebRequest audioClipRequest = UnityWebRequestMultimedia.GetAudioClip(deathClipUri.AbsoluteUri, AudioType.WAV);
        yield return audioClipRequest.SendWebRequest();

        CompleteDeathClipLoad(audioClipRequest, deathClipPath);
    }

    private void CompleteDeathClipLoad(UnityWebRequest audioClipRequest, string deathClipPath)
    {
        if (audioClipRequest.result != UnityWebRequest.Result.Success)
        {
            Logger.LogError($"Failed to load {Path.GetFileName(deathClipPath)}: {audioClipRequest.error}");
            return;
        }

        _deathClip = DownloadHandlerAudioClip.GetContent(audioClipRequest);
        Logger.LogInfo($"Death sound replaced with {Path.GetFileName(deathClipPath)}");
    }

    private static string? FindFirstWavFile(string pluginDirectory)
    {
        string[] wavFiles = Directory.GetFiles(pluginDirectory, "*.wav");
        Array.Sort(wavFiles, StringComparer.OrdinalIgnoreCase);
        return wavFiles.Length == 0 ? null : wavFiles[0];
    }

    internal static void ReplaceDeathSoundIfLoaded(Sound vanillaDeathSound, Vector3 position)
    {
        DeathSound? instance = _instance;
        AudioClip? deathClip = instance?._deathClip;
        if (instance == null || deathClip == null)
        {
            return;
        }

        int generation = PendingDeathSoundMutes.Queue(vanillaDeathSound);
        instance.StartCoroutine(PendingDeathSoundMutes.Expire(vanillaDeathSound, generation));
        AudioSource.PlayClipAtPoint(deathClip, position);
    }

    private void ApplyHarmonyPatches()
    {
        _harmony = new Harmony(Info.Metadata.GUID);
        _harmony.PatchAll();
    }
}

static class PluginMetadata
{
    internal const string Guid = "Modrats.DeathSound";
    internal const string Name = "DeathSound";
    internal const string Version = "1.0.0";
}