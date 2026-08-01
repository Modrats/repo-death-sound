using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace DeathSound;

// PlayerDeathRPC is a Photon PunRPC with RpcTarget.All, so this fires once per death on every client.
[HarmonyPatch(typeof(PlayerAvatar), nameof(PlayerAvatar.PlayerDeathRPC))]
static class PlayerDeathPatch
{
    [HarmonyPostfix]
    private static void Postfix(PlayerAvatar __instance)
    {
        DeathSound.ReplaceDeathSoundIfLoaded(__instance.deathSound, __instance.transform.position);
    }
}

[HarmonyPatch(typeof(Sound), nameof(Sound.Play), new[] { typeof(Vector3), typeof(float), typeof(float), typeof(float), typeof(float) })]
static class VanillaDeathSoundMutePatch
{
    private static bool Prefix(Sound __instance)
    {
        return !PendingDeathSoundMutes.TryConsume(__instance);
    }
}

static class PendingDeathSoundMutes
{
    private const float ExpirationSeconds = 5f;
    private static readonly Dictionary<Sound, int> PendingGenerations = new();
    private static int _nextGeneration;

    internal static int Queue(Sound deathSound)
    {
        int generation = ++_nextGeneration;
        PendingGenerations[deathSound] = generation;
        return generation;
    }

    internal static bool TryConsume(Sound deathSound)
    {
        return PendingGenerations.Remove(deathSound);
    }

    internal static IEnumerator Expire(Sound deathSound, int generation)
    {
        yield return new WaitForSeconds(ExpirationSeconds);

        if (PendingGenerations.TryGetValue(deathSound, out int pendingGeneration) && pendingGeneration == generation)
        {
            PendingGenerations.Remove(deathSound);
        }
    }

    internal static void Clear()
    {
        PendingGenerations.Clear();
    }
}
