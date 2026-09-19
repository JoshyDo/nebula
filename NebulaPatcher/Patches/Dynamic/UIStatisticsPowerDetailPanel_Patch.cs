#region

using System;
using System.Collections.Generic;
using System.Text;
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
    private static readonly Color CyanColor = new(0.00f, 0.85f, 1.00f, 1f);
    private static readonly Color OrangeColor = new(1.00f, 0.60f, 0.05f, 1f);

    private static readonly Color[] CyanPalette = new Color[]
    {
        new(0.00f, 0.85f, 1.00f, 1f), // Bright Cyan
        new(0.12f, 0.72f, 0.95f, 1f), // Sky Blue
        new(0.25f, 0.88f, 0.95f, 1f), // Light Aqua
        new(0.05f, 0.60f, 0.85f, 1f), // Deep Cyan
        new(0.35f, 0.92f, 1.00f, 1f), // Vivid Azure
        new(0.00f, 0.78f, 0.88f, 1f), // Teal Cyan
        new(0.20f, 0.65f, 0.92f, 1f), // Ocean Blue
        new(0.40f, 0.80f, 1.00f, 1f), // Ice Blue
    };

    private static readonly Color[] OrangePalette = new Color[]
    {
        new(1.00f, 0.60f, 0.05f, 1f), // Warm Amber Orange
        new(1.00f, 0.45f, 0.15f, 1f), // Deep Coral Orange
        new(1.00f, 0.72f, 0.20f, 1f), // Bright Golden Orange
        new(0.95f, 0.35f, 0.10f, 1f), // Fiery Orange
        new(1.00f, 0.80f, 0.35f, 1f), // Light Amber
        new(0.90f, 0.40f, 0.05f, 1f), // Burnt Orange
        new(1.00f, 0.55f, 0.25f, 1f), // Warm Coral
        new(0.85f, 0.30f, 0.00f, 1f), // Dark Rust Orange
    };

    private struct SliceInfo
    {
        public bool isOrange;
        public int level;
        public int parent;
        public string name;
        public double value;
        public double fill;
        public double offset;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(UISectorGraph), nameof(UISectorGraph._OnUpdate))]
    public static void UISectorGraph_OnUpdate_Prefix(UISectorGraph __instance)
    {
        if (!Multiplayer.IsActive || Multiplayer.Session.LocalPlayer.IsHost || __instance == null) return;
        SanitizeSectorGraphState(__instance);
    }

    [HarmonyFinalizer]
    [HarmonyPatch(typeof(UISectorGraph), nameof(UISectorGraph._OnUpdate))]
    public static Exception UISectorGraph_OnUpdate_Finalizer(Exception __exception)
    {
        if (__exception != null && Multiplayer.IsActive && !Multiplayer.Session.LocalPlayer.IsHost)
        {
            return null; // Suppress client-side exception so UI update loop never crashes
        }
        return __exception;
    }

    [HarmonyFinalizer]
    [HarmonyPatch(typeof(UISectorGraph), nameof(UISectorGraph.Refresh))]
    public static Exception UISectorGraph_Refresh_Finalizer(Exception __exception)
    {
        if (__exception != null && Multiplayer.IsActive && !Multiplayer.Session.LocalPlayer.IsHost)
        {
            return null;
        }
        return __exception;
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
    [HarmonyPatch(typeof(UIStatisticsPowerDetailPanel), nameof(UIStatisticsPowerDetailPanel.RefreshDetailEntries))]
    public static Exception RefreshDetailEntries_Finalizer(Exception __exception)
    {
        if (__exception != null && Multiplayer.IsActive && !Multiplayer.Session.LocalPlayer.IsHost)
        {
            return null;
        }
        return __exception;
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
        // 1. Generation Small Pie Chart (Top Center)
        var genSlices = CollectSlices(panel.powerGenEntries, out var totalGen, wantOrange: false);
        if (panel.powerGenGraphSmall != null)
        {
            UpdateSectorGraph(panel.powerGenGraphSmall, genSlices);
            if (panel.powerGenGraphSmall.subText != null && string.IsNullOrEmpty(panel.powerGenGraphSmall.subText.text))
            {
                panel.powerGenGraphSmall.subText.text = "Generation".Translate();
            }
            if (panel.powerGenGraphSmall.mainText != null)
            {
                panel.powerGenGraphSmall.mainText.text = FormatPower(panel.sb1, totalGen);
            }
        }

        // 2. Consumption Small Pie Chart (Bottom Center)
        var conSlices = CollectSlices(panel.powerConEntries, out var totalCon, wantOrange: true);
        if (panel.powerConGraphSmall != null)
        {
            UpdateSectorGraph(panel.powerConGraphSmall, conSlices);
            if (panel.powerConGraphSmall.subText != null && string.IsNullOrEmpty(panel.powerConGraphSmall.subText.text))
            {
                panel.powerConGraphSmall.subText.text = "Consumption".Translate();
            }
            if (panel.powerConGraphSmall.mainText != null)
            {
                panel.powerConGraphSmall.mainText.text = FormatPower(panel.sb2, totalCon);
            }
        }

        // 3. Large Graph on the Left (Sufficiency dual concentric rings or selected breakdown)
        var largeGraph = panel.powerGenGraphLarge != null && panel.powerGenGraphLarge.gameObject.activeInHierarchy
            ? panel.powerGenGraphLarge
            : (panel.powerConGraphLarge != null && panel.powerConGraphLarge.gameObject.activeInHierarchy ? panel.powerConGraphLarge : null);

        if (largeGraph != null)
        {
            var sub = largeGraph.subText != null ? largeGraph.subText.text : "";
            var isGenMode = string.Equals(sub, "Generation".Translate(), StringComparison.OrdinalIgnoreCase) ||
                            sub.IndexOf("Generation", StringComparison.OrdinalIgnoreCase) >= 0;
            var isConMode = (largeGraph == panel.powerConGraphLarge) ||
                            string.Equals(sub, "Consumption".Translate(), StringComparison.OrdinalIgnoreCase) ||
                            sub.IndexOf("Consumption", StringComparison.OrdinalIgnoreCase) >= 0;

            if (isGenMode)
            {
                UpdateSectorGraph(largeGraph, genSlices);
                if (largeGraph.mainText != null)
                {
                    largeGraph.mainText.text = FormatPower(panel.sb3, totalGen);
                }
            }
            else if (isConMode)
            {
                UpdateSectorGraph(largeGraph, conSlices);
                if (largeGraph.mainText != null)
                {
                    largeGraph.mainText.text = FormatPower(panel.sb3, totalCon);
                }
            }
            else
            {
                // Default: Sufficiency Mode with Dual Concentric Rings (identical to singleplayer vanilla DSP)
                var suffSlices = new List<SliceInfo>();
                var ratio = totalCon > 0 ? (totalGen / totalCon) : (totalGen > 0 ? 1.0 : 0.0);
                var suffFill = Math.Min(1.0, Math.Max(0.0, ratio));

                // Outer Ring (Level 0, Cyan/Blue): Sufficiency percentage
                suffSlices.Add(new SliceInfo
                {
                    isOrange = false,
                    level = 0,
                    parent = -1,
                    name = "Sufficiency".Translate(),
                    value = totalGen,
                    fill = suffFill,
                    offset = 0.0
                });

                // Inner Ring (Level 1, Warm Orange): Machine consumption breakdown relative to capacity
                if (panel.powerConEntries != null && panel.powerConEntries.Count > 0)
                {
                    var normalizer = Math.Max(totalGen, totalCon);
                    var currentInnerOffset = 0.0;

                    for (var i = 0; i < panel.powerConEntries.Count; i++)
                    {
                        var entry = panel.powerConEntries[i];
                        if (entry != null && entry.gameObject.activeSelf && entry.power > 0)
                        {
                            var sliceFill = normalizer > 0 ? (entry.power / normalizer) : 0.0;
                            var name = entry.itemNameText != null ? entry.itemNameText.text : "";
                            if (string.IsNullOrEmpty(name) && entry.itemId > 0)
                            {
                                name = LDB.items?.Select(entry.itemId)?.Name ?? "";
                            }

                            suffSlices.Add(new SliceInfo
                            {
                                isOrange = true,
                                level = 1,
                                parent = -1,
                                name = name,
                                value = entry.power,
                                fill = sliceFill,
                                offset = currentInnerOffset
                            });
                            currentInnerOffset += sliceFill;
                        }
                    }
                }

                UpdateSectorGraph(largeGraph, suffSlices);

                if (largeGraph.subText != null)
                {
                    largeGraph.subText.text = "Sufficiency".Translate();
                }

                if (largeGraph.mainText != null)
                {
                    largeGraph.mainText.text = (ratio * 100.0).ToString("0.0") + "%";
                    if (ratio >= 1.0)
                        largeGraph.mainText.color = CyanColor;
                    else if (ratio >= 0.8)
                        largeGraph.mainText.color = new Color(0.3f, 0.85f, 0.4f, 1f); // Green
                    else if (ratio >= 0.5)
                        largeGraph.mainText.color = OrangeColor;
                    else
                        largeGraph.mainText.color = new Color(0.9f, 0.25f, 0.25f, 1f); // Red
                }
            }
        }
    }

    private static void ApplyChartAstroPowerFix(UIChartAstroPower chart)
    {
        var genSlices = CollectSlices(chart.powerGenEntries, out var totalGen, wantOrange: false);
        if (chart.genSectorGraph != null)
        {
            UpdateSectorGraph(chart.genSectorGraph, genSlices);
        }

        var conSlices = CollectSlices(chart.powerConEntries, out var totalCon, wantOrange: true);
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

    private static List<SliceInfo> CollectSlices(List<UIStatisticsPowerDetailEntry> entries, out double totalPower, bool wantOrange)
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
        for (var i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            if (entry != null && entry.gameObject.activeSelf && entry.power > 0)
            {
                var fill = entry.power / totalPower;
                var name = entry.itemNameText != null ? entry.itemNameText.text : "";
                if (string.IsNullOrEmpty(name) && entry.itemId > 0)
                {
                    name = LDB.items?.Select(entry.itemId)?.Name ?? "";
                }

                list.Add(new SliceInfo
                {
                    isOrange = wantOrange,
                    level = 0,
                    parent = -1,
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

    private static void SanitizeSectorGraphState(UISectorGraph graph)
    {
        if (graph == null) return;

        // 1. Ensure colors has ample capacity (at least 32 distinct colors)
        if (graph.colors == null || graph.colors.Length < 32)
        {
            var oldColors = graph.colors;
            graph.colors = new Color[32];
            for (var i = 0; i < 32; i++)
            {
                if (oldColors != null && i < oldColors.Length)
                    graph.colors[i] = oldColors[i];
                else
                    graph.colors[i] = i < 16 ? CyanPalette[i % CyanPalette.Length] : OrangePalette[i % OrangePalette.Length];
            }
        }

        // 2. Ensure levelRanges has at least 8 elements (supports up to 4 levels)
        if (graph.levelRanges == null || graph.levelRanges.Length < 8)
        {
            var oldRanges = graph.levelRanges;
            var r0 = (oldRanges != null && oldRanges.Length > 0) ? oldRanges[0] : 60f;
            var r1 = (oldRanges != null && oldRanges.Length > 1) ? oldRanges[1] : 90f;
            graph.levelRanges = new float[8]
            {
                r0, r1,                   // Level 0: Outer ring
                r0 * 0.72f, r1 * 0.82f,   // Level 1: Inner ring
                r0 * 0.45f, r1 * 0.55f,   // Level 2
                r0 * 0.20f, r1 * 0.30f    // Level 3
            };
        }

        // 3. Ensure tmp_sum has at least 8 elements
        if (graph.tmp_sum == null || graph.tmp_sum.Length < 8)
        {
            graph.tmp_sum = new double[8];
        }

        // 4. Ensure levelGroups has at least 4 elements
        if (graph.levelGroups == null || graph.levelGroups.Length < 4)
        {
            var newGroups = new RectTransform[4];
            if (graph.levelGroups != null)
            {
                for (var i = 0; i < graph.levelGroups.Length && i < 4; i++)
                    newGroups[i] = graph.levelGroups[i];
            }
            if (newGroups[0] == null) newGroups[0] = graph.rectTrans;
            for (var i = 1; i < 4; i++)
            {
                if (newGroups[i] == null)
                {
                    var childName = $"LevelGroup_{i}";
                    var existing = graph.rectTrans != null ? graph.rectTrans.Find(childName) : null;
                    if (existing != null)
                    {
                        newGroups[i] = existing.GetComponent<RectTransform>();
                    }
                    else if (graph.rectTrans != null)
                    {
                        var go = new GameObject(childName, typeof(RectTransform));
                        var rt = go.GetComponent<RectTransform>();
                        rt.SetParent(graph.rectTrans, false);
                        rt.anchorMin = Vector2.zero;
                        rt.anchorMax = Vector2.one;
                        rt.offsetMin = Vector2.zero;
                        rt.offsetMax = Vector2.zero;
                        rt.localScale = i == 1 ? new Vector3(0.82f, 0.82f, 1f) : Vector3.one;
                        newGroups[i] = rt;
                    }
                }
            }
            graph.levelGroups = newGroups;
        }

        // 5. Ensure levelSprites has at least 4 elements
        if (graph.levelSprites == null || graph.levelSprites.Length < 4)
        {
            var newSprites = new Sprite[4];
            var baseSprite = (graph.levelSprites != null && graph.levelSprites.Length > 0) ? graph.levelSprites[0] : null;
            if (graph.levelSprites != null)
            {
                for (var i = 0; i < graph.levelSprites.Length && i < 4; i++)
                    newSprites[i] = graph.levelSprites[i];
            }
            for (var i = 0; i < 4; i++)
            {
                if (newSprites[i] == null) newSprites[i] = baseSprite;
            }
            graph.levelSprites = newSprites;
        }

        // 6. Clamp coreFanIndex, grayFanIndex, hoveredFanIndex
        if (graph.coreFanIndex >= graph.fanCount) graph.coreFanIndex = -1;
        if (graph.grayFanIndex >= graph.fanCount) graph.grayFanIndex = -1;
        if (graph.hoveredFanIndex >= graph.fanCount) graph.hoveredFanIndex = -1;

        // 7. Sanitize fanDatas
        if (graph.fanDatas != null)
        {
            var maxLvl = Math.Max(0, (graph.levelRanges.Length / 2) - 1);
            var maxCol = Math.Max(0, graph.colors.Length - 1);
            for (var i = 0; i < graph.fanCount && i < graph.fanDatas.Length; i++)
            {
                if (graph.fanDatas[i].index < 0 || graph.fanDatas[i].index > maxCol)
                    graph.fanDatas[i].index = 0;
                if (graph.fanDatas[i].level < 0 || graph.fanDatas[i].level > maxLvl)
                    graph.fanDatas[i].level = 0;
                graph.fanDatas[i].parent = -1;
            }
        }
    }

    private static void UpdateSectorGraph(UISectorGraph graph, List<SliceInfo> slices)
    {
        if (graph == null) return;

        SanitizeSectorGraphState(graph);

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
            var colorIdx = (slice.isOrange ? 16 : 0) + (i % 8);
            if (colorIdx >= graph.colors.Length) colorIdx = 0;

            graph.fanDatas[i].index = colorIdx;
            graph.fanDatas[i].name = slice.name;
            graph.fanDatas[i].value = slice.value;
            graph.fanDatas[i].fill = slice.fill;
            graph.fanDatas[i].offset = slice.offset;
            graph.fanDatas[i].cursor = slice.offset + slice.fill * 0.5;
            graph.fanDatas[i].level = Math.Max(0, Math.Min(slice.level, 3));
            graph.fanDatas[i].parent = -1;
        }

        graph.fanCount = count;
        graph.coreFanIndex = -1;
        graph.grayFanIndex = -1;
        graph.hoveredFanIndex = -1;

        const double GAP = 0.0018; // Clean visible separation gap between slices

        // Ensure fans are instantiated, correctly parented, and configured
        for (var i = 0; i < count; i++)
        {
            var slice = slices[i];
            var level = Math.Max(0, Math.Min(slice.level, 3));

            var parent = (graph.levelGroups != null && level < graph.levelGroups.Length && graph.levelGroups[level] != null)
                ? graph.levelGroups[level]
                : graph.rectTrans;

            var sprite = (graph.levelSprites != null && level < graph.levelSprites.Length)
                ? graph.levelSprites[level]
                : null;

            if (graph.fans[i] == null && graph.fanPrefab != null)
            {
                graph.fans[i] = UnityEngine.Object.Instantiate(graph.fanPrefab, parent);
                graph.fans[i].graph = graph;
            }

            var fan = graph.fans[i];
            if (fan != null)
            {
                if (fan.transform.parent != parent)
                {
                    fan.transform.SetParent(parent, false);
                }

                if (fan.fanImage != null)
                {
                    var img = fan.fanImage;
                    if (sprite != null) img.sprite = sprite;
                    img.type = Image.Type.Filled;
                    img.fillMethod = Image.FillMethod.Radial360;
                    img.fillOrigin = (int)Image.Origin360.Top;
                    img.fillClockwise = true;

                    // Subtle separation gap between adjacent slices (only when multiple slices)
                    var hasGap = count > 1 && slice.fill > (GAP * 2.2) && slice.fill < 0.999;
                    var fillAmt = hasGap ? (slice.fill - GAP) : slice.fill;
                    var offsetAmt = hasGap ? (slice.offset + GAP * 0.5) : slice.offset;

                    img.fillAmount = (float)Math.Max(0.0005, fillAmt);

                    var color = slice.isOrange
                        ? OrangePalette[i % OrangePalette.Length]
                        : CyanPalette[i % CyanPalette.Length];
                    img.color = color;
                    img.rectTransform.localEulerAngles = new Vector3(0f, 0f, (float)(-offsetAmt * 360.0));
                }

                fan.gameObject.SetActive(slice.fill > 0.0001);
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

        // Run game's native Refresh for tooltip bounds and totals
        try
        {
            graph.Refresh();
        }
        catch
        {
            // Silently ignore
        }
    }

    private static string FormatPower(StringBuilder sb, double power)
    {
        if (sb == null) sb = new StringBuilder();
        sb.Clear();
        try
        {
            StringBuilderUtility.WriteKMGPower(sb, 0, (long)power, false);
            return sb.ToString();
        }
        catch
        {
            if (power >= 1_000_000_000)
                return $"{(power / 1_000_000_000):0.00} GW";
            if (power >= 1_000_000)
                return $"{(power / 1_000_000):0.00} MW";
            if (power >= 1_000)
                return $"{(power / 1_000):0.00} kW";
            return $"{power:0} W";
        }
    }
}
