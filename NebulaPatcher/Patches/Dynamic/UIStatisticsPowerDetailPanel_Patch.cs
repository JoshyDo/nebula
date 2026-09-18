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
    private static readonly Color CyanColor = new(0.2f, 0.75f, 1.0f, 1f);
    private static readonly Color OrangeColor = new(1.0f, 0.65f, 0.2f, 1f);

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
                                parent = 0,
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

    private static int GetColorIndex(UISectorGraph graph, bool wantOrange)
    {
        if (graph.colors == null || graph.colors.Length == 0) return 0;
        if (graph.colors.Length == 1) return 0;

        for (var i = 0; i < graph.colors.Length; i++)
        {
            var isOrange = graph.colors[i].r > graph.colors[i].b;
            if (isOrange == wantOrange)
            {
                return i;
            }
        }

        return wantOrange ? Math.Min(1, graph.colors.Length - 1) : 0;
    }

    private static void UpdateSectorGraph(UISectorGraph graph, List<SliceInfo> slices)
    {
        if (graph == null) return;

        // Ensure palette has both cyan and orange
        if (graph.colors == null || graph.colors.Length == 0)
        {
            graph.colors = new[] { CyanColor, OrangeColor };
        }
        else if (graph.colors.Length == 1)
        {
            var c0 = graph.colors[0];
            var c1 = c0.r > c0.b ? CyanColor : OrangeColor;
            graph.colors = new[] { c0, c1 };
        }

        // Ensure tmp_sum has capacity for multi-level calculations
        if (graph.tmp_sum == null || graph.tmp_sum.Length < 4)
        {
            graph.tmp_sum = new double[4];
        }

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

        var maxLevel = Math.Max(0, (graph.levelGroups?.Length ?? 1) - 1);

        // Populate FanDatas
        for (var i = 0; i < count; i++)
        {
            var slice = slices[i];
            var colorIdx = GetColorIndex(graph, slice.isOrange);
            var level = Math.Max(0, Math.Min(slice.level, maxLevel));

            graph.fanDatas[i].index = colorIdx;
            graph.fanDatas[i].name = slice.name;
            graph.fanDatas[i].value = slice.value;
            graph.fanDatas[i].fill = slice.fill;
            graph.fanDatas[i].offset = slice.offset;
            graph.fanDatas[i].cursor = slice.offset + slice.fill * 0.5;
            graph.fanDatas[i].level = level;
            graph.fanDatas[i].parent = slice.parent;
        }

        graph.fanCount = count;

        // Ensure fans are instantiated, correctly parented, and configured
        for (var i = 0; i < count; i++)
        {
            var slice = slices[i];
            var level = Math.Max(0, Math.Min(slice.level, maxLevel));

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
                    img.fillAmount = (float)slice.fill;

                    var colorIdx = GetColorIndex(graph, slice.isOrange);
                    img.color = graph.colors[colorIdx];
                    img.rectTransform.localEulerAngles = new Vector3(0f, 0f, (float)(-slice.offset * 360.0));
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
