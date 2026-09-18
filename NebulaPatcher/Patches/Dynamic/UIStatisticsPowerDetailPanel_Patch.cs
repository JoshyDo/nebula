#region

using System;
using System.Collections.Generic;
using HarmonyLib;
using NebulaModel.Logger;
using NebulaWorld;
using UnityEngine;
using UnityEngine.UI;

#endregion

namespace NebulaPatcher.Patches.Dynamic;

[HarmonyPatch]
internal class UIStatisticsPowerDetailPanel_Patch
{
    private static readonly Color[] DefaultColors =
    [
        new Color(0.2f, 0.75f, 1.0f, 1f),  // Bright Blue
        new Color(1.0f, 0.65f, 0.2f, 1f),  // Orange
        new Color(0.3f, 0.9f, 0.4f, 1f),   // Green
        new Color(1.0f, 0.85f, 0.2f, 1f),  // Yellow
        new Color(0.9f, 0.35f, 0.35f, 1f), // Red/Coral
        new Color(0.7f, 0.4f, 1.0f, 1f),   // Purple
        new Color(0.2f, 0.9f, 0.9f, 1f),   // Cyan
        new Color(1.0f, 0.4f, 0.7f, 1f),   // Pink
        new Color(0.6f, 0.8f, 0.2f, 1f),   // Lime
        new Color(0.9f, 0.5f, 0.1f, 1f),   // Amber
        new Color(0.4f, 0.6f, 1.0f, 1f),   // Soft Blue
        new Color(0.8f, 0.8f, 0.8f, 1f)    // Light Gray
    ];

    private struct SliceInfo
    {
        public int index;
        public string name;
        public double value;
        public double fill;
        public double offset;
    }

    [HarmonyFinalizer]
    [HarmonyPatch(typeof(UIStatisticsPowerDetailPanel), nameof(UIStatisticsPowerDetailPanel.RefreshPowerDatas))]
    public static Exception RefreshPowerDatas_Finalizer(Exception __exception)
    {
        // Suppress any remote factory NRE on clients
        if (__exception != null && Multiplayer.IsActive && !Multiplayer.Session.LocalPlayer.IsHost)
        {
            return null;
        }
        return __exception;
    }

    [HarmonyFinalizer]
    [HarmonyPatch(typeof(UIStatisticsPowerDetailPanel), nameof(UIStatisticsPowerDetailPanel.RefreshGraphs))]
    public static Exception RefreshGraphs_Finalizer(Exception __exception, UIStatisticsPowerDetailPanel __instance)
    {
        if (!Multiplayer.IsActive || Multiplayer.Session.LocalPlayer.IsHost || __instance == null)
        {
            return __exception;
        }

        try
        {
            ApplyPowerPanelGraphsFix(__instance);
        }
        catch (Exception e)
        {
            Log.Warn($"ApplyPowerPanelGraphsFix error: {e}");
        }

        return null; // Suppress any exception in base method
    }

    [HarmonyFinalizer]
    [HarmonyPatch(typeof(UIChartAstroPower), nameof(UIChartAstroPower.RefreshGraphs))]
    public static Exception UIChartAstroPower_RefreshGraphs_Finalizer(Exception __exception, UIChartAstroPower __instance)
    {
        if (!Multiplayer.IsActive || Multiplayer.Session.LocalPlayer.IsHost || __instance == null)
        {
            return __exception;
        }

        try
        {
            ApplyChartAstroPowerFix(__instance);
        }
        catch (Exception e)
        {
            Log.Warn($"ApplyChartAstroPowerFix error: {e}");
        }

        return null;
    }

    private static void ApplyPowerPanelGraphsFix(UIStatisticsPowerDetailPanel panel)
    {
        // 1. Generation Small Pie Chart
        var genSlices = CollectSlices(panel.powerGenEntries, out var totalGen);
        if (panel.powerGenGraphSmall != null)
        {
            UpdateSectorGraph(panel.powerGenGraphSmall, genSlices);
        }

        // 2. Consumption Small Pie Chart
        var conSlices = CollectSlices(panel.powerConEntries, out var totalCon);
        if (panel.powerConGraphSmall != null)
        {
            UpdateSectorGraph(panel.powerConGraphSmall, conSlices);
        }

        // 3. Large Graph on the Left (Sufficiency or selected breakdown)
        var largeGraph = panel.powerGenGraphLarge != null && panel.powerGenGraphLarge.gameObject.activeInHierarchy
            ? panel.powerGenGraphLarge
            : (panel.powerConGraphLarge != null && panel.powerConGraphLarge.gameObject.activeInHierarchy ? panel.powerConGraphLarge : null);

        if (largeGraph != null)
        {
            var sub = largeGraph.subText != null ? largeGraph.subText.text : "";
            var main = largeGraph.mainText != null ? largeGraph.mainText.text : "";

            if (sub.IndexOf("Sufficiency", StringComparison.OrdinalIgnoreCase) >= 0 ||
                main.IndexOf("%", StringComparison.OrdinalIgnoreCase) >= 0 ||
                sub.Length == 0)
            {
                // Display sufficiency gauge
                var ratio = totalCon > 0 ? totalGen / totalCon : (totalGen > 0 ? 1.0 : 0.0);
                var fill = Math.Min(1.0, Math.Max(0.0, ratio));
                Color suffColor;
                if (ratio >= 1.0)
                    suffColor = new Color(0.2f, 0.75f, 1.0f, 1f); // Cyan
                else if (ratio >= 0.8)
                    suffColor = new Color(0.3f, 0.85f, 0.4f, 1f); // Green
                else if (ratio >= 0.5)
                    suffColor = new Color(1.0f, 0.65f, 0.2f, 1f); // Orange
                else
                    suffColor = new Color(0.9f, 0.25f, 0.25f, 1f); // Red

                var suffSlices = new List<SliceInfo>
                {
                    new()
                    {
                        index = 0,
                        name = sub.Length > 0 ? sub : "Sufficiency",
                        value = ratio,
                        fill = fill,
                        offset = 0.0
                    }
                };
                UpdateSectorGraph(largeGraph, suffSlices, suffColor);
            }
            else if (sub.IndexOf("Generation", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                UpdateSectorGraph(largeGraph, genSlices);
            }
            else if (sub.IndexOf("Consumption", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                UpdateSectorGraph(largeGraph, conSlices);
            }
            else
            {
                // Default to sufficiency
                var ratio = totalCon > 0 ? totalGen / totalCon : (totalGen > 0 ? 1.0 : 0.0);
                var fill = Math.Min(1.0, Math.Max(0.0, ratio));
                var suffSlices = new List<SliceInfo>
                {
                    new()
                    {
                        index = 0,
                        name = "Sufficiency",
                        value = ratio,
                        fill = fill,
                        offset = 0.0
                    }
                };
                UpdateSectorGraph(largeGraph, suffSlices, new Color(0.2f, 0.75f, 1.0f, 1f));
            }
        }
    }

    private static void ApplyChartAstroPowerFix(UIChartAstroPower chart)
    {
        var genSlices = CollectSlices(chart.powerGenEntries, out var totalGen);
        if (chart.genSectorGraph != null)
        {
            UpdateSectorGraph(chart.genSectorGraph, genSlices);
        }

        var conSlices = CollectSlices(chart.powerConEntries, out var totalCon);
        if (chart.conSectorGraph != null)
        {
            UpdateSectorGraph(chart.conSectorGraph, conSlices);
        }

        if (chart.powerRoundFg != null)
        {
            var ratio = totalCon > 0 ? Mathf.Clamp01((float)(totalGen / totalCon)) : (totalGen > 0 ? 1f : 0f);
            chart.powerRoundFg.fillAmount = ratio;
            if (ratio >= 1f)
                chart.powerRoundFg.color = chart.powerRoundFgColor0;
            else if (ratio >= 0.8f)
                chart.powerRoundFg.color = chart.powerRoundFgColor1;
            else if (ratio >= 0.5f)
                chart.powerRoundFg.color = chart.powerRoundFgColor2;
            else
                chart.powerRoundFg.color = chart.powerRoundFgColor3;
        }
    }

    private static List<SliceInfo> CollectSlices(List<UIStatisticsPowerDetailEntry> entries, out double totalPower)
    {
        totalPower = 0;
        var list = new List<SliceInfo>();
        if (entries == null) return list;

        for (var i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            if (entry != null && entry.gameObject.activeSelf && entry.power > 0)
            {
                totalPower += entry.power;
            }
        }

        if (totalPower <= 0) return list;

        var currentOffset = 0.0;
        var sliceIdx = 0;
        for (var i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            if (entry != null && entry.gameObject.activeSelf && entry.power > 0)
            {
                var fill = entry.power / totalPower;
                var name = entry.itemNameText != null ? entry.itemNameText.text : "";
                list.Add(new SliceInfo
                {
                    index = sliceIdx++,
                    name = name,
                    value = entry.power,
                    fill = fill,
                    offset = currentOffset
                });
                currentOffset += fill;
            }
        }

        return list;
    }

    private static void UpdateSectorGraph(UISectorGraph graph, List<SliceInfo> slices, Color? overrideColor = null)
    {
        if (graph == null) return;

        var count = slices != null ? slices.Count : 0;

        // Ensure fanDatas capacity
        if (graph.fanDatas == null || graph.fanDatas.Length < count)
        {
            var newDatas = new UISectorGraph.FanData[Math.Max(count + 4, 32)];
            if (graph.fanDatas != null) Array.Copy(graph.fanDatas, newDatas, graph.fanDatas.Length);
            graph.fanDatas = newDatas;
        }

        // Ensure fans capacity
        if (graph.fans == null || graph.fans.Length < graph.fanDatas.Length)
        {
            var newFans = new UISectorFan[graph.fanDatas.Length];
            if (graph.fans != null) Array.Copy(graph.fans, newFans, graph.fans.Length);
            graph.fans = newFans;
        }

        // Populate FanDatas
        for (var i = 0; i < count; i++)
        {
            var slice = slices[i];
            graph.fanDatas[i].index = slice.index;
            graph.fanDatas[i].name = slice.name;
            graph.fanDatas[i].value = slice.value;
            graph.fanDatas[i].fill = slice.fill;
            graph.fanDatas[i].offset = slice.offset;
            graph.fanDatas[i].cursor = slice.offset + slice.fill * 0.5;
            graph.fanDatas[i].level = 0;
            graph.fanDatas[i].parent = -1;
        }

        graph.fanCount = count;

        // Try game's native Refresh first
        try
        {
            graph.Refresh();
        }
        catch
        {
            // Fall back to direct instantiation/setup below
        }

        // Direct instantiation & configuration guarantee
        Transform parent = graph.levelGroups != null && graph.levelGroups.Length > 0 && graph.levelGroups[0] != null
            ? graph.levelGroups[0]
            : graph.rectTrans;

        Sprite sprite = graph.levelSprites != null && graph.levelSprites.Length > 0 ? graph.levelSprites[0] : null;

        for (var i = 0; i < count; i++)
        {
            var slice = slices[i];
            if (graph.fans[i] == null && graph.fanPrefab != null)
            {
                graph.fans[i] = UnityEngine.Object.Instantiate(graph.fanPrefab, parent);
                graph.fans[i].graph = graph;
            }

            var fan = graph.fans[i];
            if (fan != null && fan.fanImage != null)
            {
                var img = fan.fanImage;
                if (sprite != null) img.sprite = sprite;
                img.type = Image.Type.Filled;
                img.fillMethod = Image.FillMethod.Radial360;
                img.fillOrigin = (int)Image.Origin360.Top;
                img.fillClockwise = true;
                img.fillAmount = (float)slice.fill;

                var c = overrideColor ??
                        (graph.colors != null && graph.colors.Length > 0
                            ? graph.colors[slice.index % graph.colors.Length]
                            : DefaultColors[slice.index % DefaultColors.Length]);
                img.color = c;
                img.rectTransform.localEulerAngles = new Vector3(0f, 0f, (float)(-slice.offset * 360.0));
                fan.gameObject.SetActive(true);
            }
        }

        // Deactivate unused fans
        for (var j = count; j < graph.fans.Length; j++)
        {
            if (graph.fans[j] != null)
            {
                graph.fans[j].gameObject.SetActive(false);
            }
        }
    }
}
