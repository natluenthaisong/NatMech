using System;
using System.Linq;
using AnoMech.Core.Game.Party;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using static AnoMech.Scenarios.Umad.UmadConstants;

namespace AnoMech.Scenarios.Umad.P3BlackHole;

// ImGui panel rendered in the main window's "Scenario config" pane when this scenario is
// active. Owns the StateOverrides instance and writes user choices into it.
public sealed class UmadP3BlackHoleSettingsWindow
{
    public UmadP3BlackHoleStateOverrides Overrides { get; } = new();

    // Every dropdown, slider and button here is this wide, matching the main window's Setup dropdowns.
    private const float ControlWidth = 180f;
    private static float Width => ControlWidth * ImGuiHelpers.GlobalScale;

    // Which seat the per-player rows are showing. UI state only; never broadcast.
    private PartyRole editingSeat = PartyRole.MainTank;

    // Solo, the player's own picks sit in this panel; a host assigns seats from the Multiplayer
    // window.
    public void Draw()
    {
        var solo = !PerRole.SeatsActive;
#if !DEBUG
        if (!solo)
        {
            ImGui.TextDisabled("Everything here is per player -- use the button below.");
            return;
        }
#endif
        Section("Your debuffs");
        if (ImGui.Button("Reset to random", new System.Numerics.Vector2(Width, 0f)))
        {
#if DEBUG
            Overrides.SlapAttacks[0] = null;
#endif
            if (solo) ResetMine();
        }
        Tooltip("Auto rolls a new line and Accretion every run, as the real fight does.");
        if (SettingsGrid.Begin("##umadp3blackhole"))
        {
            if (solo)
            {
                DrawLineNumber();
                DrawAccretion();
            }
#if DEBUG
            DrawFirstSlap();
            if (solo) DrawFirstSlapTarget();
#endif
            SettingsGrid.End();
        }
    }

    public void DrawPerPlayer()
    {
        if (ImGui.Button("Reset to random", new System.Numerics.Vector2(Width, 0f))) ResetPerPlayer();
        if (SettingsGrid.Begin("##umadp3blackholeplayers"))
        {
            editingSeat = SettingsGrid.SeatRow("##bhseat", editingSeat);
            DrawLineNumber();
            DrawAccretion();
#if DEBUG
            DrawFirstSlapTarget();
#endif
            SettingsGrid.ForcedRecapRow("Lines set:", Overrides.LineNumber);
            SettingsGrid.ForcedRecapRow("Accretion set:", Overrides.Accretion);
            SettingsGrid.End();
        }
        SettingsGrid.ConflictRows(Overrides.Validate());
    }

    // Forces the seat into the slot carrying that line number (Auto = the fight's own roll).
    private void DrawLineNumber()
    {
        var v = Overrides.LineNumber.Effective(editingSeat);
        SettingsGrid.PlayerRow("line:");
        if (ImGui.RadioButton("Auto##line",   v == null)) Overrides.LineNumber.Set(editingSeat, null);
        ImGui.SameLine();
        if (ImGui.RadioButton("First##line",  v == 1))    Overrides.LineNumber.Set(editingSeat, 1);
        ImGui.SameLine();
        if (ImGui.RadioButton("Second##line", v == 2))    Overrides.LineNumber.Set(editingSeat, 2);
        ImGui.SameLine();
        if (ImGui.RadioButton("Third##line",  v == 3))    Overrides.LineNumber.Set(editingSeat, 3);
    }

    // Yes is dropped for tanks and third-in-line, which never carry Accretion in the fight.
    private void DrawAccretion()
    {
        var v = Overrides.Accretion.Effective(editingSeat);
        SettingsGrid.PlayerRow("Accretion:");
        if (ImGui.RadioButton("Auto##accretion", v == null))  Overrides.Accretion.Set(editingSeat, null);
        ImGui.SameLine();
        if (ImGui.RadioButton("Yes##accretion",  v == true))  Overrides.Accretion.Set(editingSeat, true);
        ImGui.SameLine();
        if (ImGui.RadioButton("No##accretion",   v == false)) Overrides.Accretion.Set(editingSeat, false);
    }

#if DEBUG
    private void DrawFirstSlap()
    {
        var v = Overrides.SlapAttacks[0];
        SettingsGrid.Row("1st Slap:");
        if (ImGui.RadioButton("Auto##firstslap",  v == null))                     Overrides.SlapAttacks[0] = null;
        ImGui.SameLine();
        if (ImGui.RadioButton("Left##firstslap",  v == ActionId.SlapHappy_Left))  Overrides.SlapAttacks[0] = ActionId.SlapHappy_Left;
        ImGui.SameLine();
        if (ImGui.RadioButton("Right##firstslap", v == ActionId.SlapHappy_Right)) Overrides.SlapAttacks[0] = ActionId.SlapHappy_Right;
    }

    private void DrawFirstSlapTarget()
    {
        var v = Overrides.FirstSlapAllOnMe.Effective(editingSeat);
        SettingsGrid.Row(PerRole.SeatsActive ? "1st Slap at:" : "1st Slap Target:");
        if (ImGui.RadioButton("Auto##firstslaptarget", v != true)) Overrides.FirstSlapAllOnMe.Set(editingSeat, null);
        ImGui.SameLine();
        if (ImGui.RadioButton($"{(PerRole.SeatsActive ? "This seat" : "Player")}##firstslaptarget", v == true))
            Overrides.FirstSlapAllOnMe.Set(editingSeat, true);
    }
#endif

    // Drawn from the scenario's DrawMultiplayerSettings so it stays editable while Multiplayer is open.
    // Viewer preferences live in the plugin config and are never broadcast; only the host's count.
    public void DrawMultiplayerSettings()
    {
        var mpGuest = Plugin.MultiplayerInstance is { IsConnected: true, IsHost: false };
        if (mpGuest) ImGui.TextDisabled("Guides, callouts and plans follow the host's settings.");
        ImGui.BeginDisabled(mpGuest);
        DrawGuides();
        DrawCallouts();
        DrawParty();
        DrawThunderIIIPlan();
        ImGui.EndDisabled();
    }

    private static void DrawGuides()
    {
        Section("Guides");
        ConfigCheckbox("Where to go (safe / stack spot)", Plugin.Config.ShowBlackHoleSpotHint, v => Plugin.Config.ShowBlackHoleSpotHint = v,
            "Marks where the selected strategy sends you for each mechanic: Slap Happy, Edict, Look upon Me, Thunder, implosion, Stomp-a-Mole towers, and back to the stack.");
        ImGui.SameLine();
        ConfigCheckbox("Your tethers", Plugin.Config.ShowBlackHoleTetherGuide, v => Plugin.Config.ShowBlackHoleTetherGuide = v,
            "Which black hole tether(s) the strategy gives you, where to step onto the beam, and where to hold it.");
        if (!Plugin.Config.ShowBlackHoleSpotHint && !Plugin.Config.ShowBlackHoleTetherGuide) return;

        var floor = Plugin.Config.BlackHoleGameMarkers;
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted("Style:");
        ImGui.SameLine();
        if (ImGui.RadioButton("In-game markers", floor)) SetConfig(() => Plugin.Config.BlackHoleGameMarkers = true);
        Tooltip("Drawn with the game's own pieces, so it looks like part of the fight: a circle on your spot, a PlayStation symbol above it, glowing arrows and a tether leading you there.");
        ImGui.SameLine();
        if (ImGui.RadioButton("Overlay", !floor)) SetConfig(() => Plugin.Config.BlackHoleGameMarkers = false);
        Tooltip("Coloured lines, circles and text labels drawn over the screen.");
        if (!floor) return;

        ImGui.Indent();
        ImGui.TextDisabled("Above your spot:  O safe spot   /\\ stack   [] hold your tether");

        var tetherIndex = Math.Max(0, Array.FindIndex(SpotTethers, t => t.Id == Plugin.Config.BlackHoleSpotTether));
        ImGui.SetNextItemWidth(Width);
        if (ImGui.Combo("Tether to your spot", ref tetherIndex, SpotTetherNames, SpotTetherNames.Length))
            SetConfig(() => Plugin.Config.BlackHoleSpotTether = SpotTethers[tetherIndex].Id);
        Tooltip("A game tether from you to your spot; it goes away once you're there.");
        if (Plugin.Config.BlackHoleSpotTether != 0)
        {
            ImGui.SameLine();
            ConfigCheckbox("Flip", Plugin.Config.BlackHoleSpotTetherReversed, v => Plugin.Config.BlackHoleSpotTetherReversed = v,
                "Tick if the tether's arrows point away from your spot.");
        }

        ConfigCheckbox("Rainbow circle", Plugin.Config.BlackHoleRainbowMarkers, v => Plugin.Config.BlackHoleRainbowMarkers = v,
            "Cycles the spot circle through the rainbow so it can't be mistaken for an attack.");
        if (Plugin.Config.BlackHoleRainbowMarkers)
        {
            ImGui.SameLine();
            var brightness = Plugin.Config.BlackHoleMarkerBrightness;
            ImGui.SetNextItemWidth(Width);
            if (ImGui.SliderFloat("Brightness", ref brightness, 0.5f, 5f, "%.1f"))
                SetConfig(() => Plugin.Config.BlackHoleMarkerBrightness = brightness);
        }
        ImGui.Unindent();
    }

    private static readonly (string Name, ushort Id)[] SpotTethers =
    [
        ("None", 0),
        ("Closer (chevrons)", UmadConstants.TetherId.Closer),
        ("Arrow 1", UmadConstants.TetherId.Arrow1),
        ("Arrow 2", UmadConstants.TetherId.Arrow2),
        ("Arrow 3", UmadConstants.TetherId.Arrow3),
        ("Arrow 4", UmadConstants.TetherId.Arrow4),
        ("Light", UmadConstants.TetherId.Light),
        ("Plain", UmadConstants.TetherId.Plain),
    ];

    private static readonly string[] SpotTetherNames = SpotTethers.Select(t => t.Name).ToArray();

    private static void DrawCallouts()
    {
        Section("Callouts");
        ConfigCheckbox("Show on screen", Plugin.Config.BlackHoleCallouts, v => Plugin.Config.BlackHoleCallouts = v,
            "Cactbot-style calls: your line number, Slap Happy, Thunder III, Damning Edict, Look upon Me, Lat/Long implosion, White Hole, Stomp-a-Mole, and your tether jobs.");
        ImGui.SameLine();
        ImGui.BeginDisabled(!Plugin.Config.BlackHoleCallouts);
        ConfigCheckbox("Read aloud", Plugin.Config.SpeakBlackHoleCallouts, v => Plugin.Config.SpeakBlackHoleCallouts = v,
            "Speaks each call with the Windows voice (Settings > Time & language > Speech).");
        ImGui.EndDisabled();
    }

    private void DrawParty()
    {
        Section("Party");
        var automarkers = Overrides.Automarkers;
        if (ImGui.Checkbox("Automarkers", ref automarkers)) Overrides.Automarkers = automarkers;
        Tooltip("Marks everyone by tether order: first line Attack 1-3, second line Bind 1-3, third line Ignore 1-2. Applies from the next run.");
    }

    // Only matters when a tank slot is bot-driven. Two rows: the sim casts Thunder III twice.
    private void DrawThunderIIIPlan()
    {
        Section("Thunder III tank plan");
        ImGui.TextDisabled("What bot tanks do for each tank buster.");
        DrawThunderIIICombo("1st (~43s)##thunder1", Overrides.ThunderSet1, Overrides.ThunderSet2, v => Overrides.ThunderSet1 = v);
        DrawThunderIIICombo("2nd (~84s)##thunder2", Overrides.ThunderSet2, Overrides.ThunderSet1, v => Overrides.ThunderSet2 = v);
    }

    private static readonly (ThunderIIIAssignment Plan, string Name)[] ThunderIIIPlans =
    [
        (ThunderIIIAssignment.MtInvulnsBoth, "MT invulns both"),
        (ThunderIIIAssignment.OtInvulnsBoth, "OT invulns both"),
        (ThunderIIIAssignment.ShareMtFirst, "Share, MT first"),
        (ThunderIIIAssignment.ShareOtFirst, "Share, OT first"),
    ];

    // Invuln and Share are separate pools: neither an invuln nor the big self-mit cooldowns
    // recover in the ~41s between sets.
    private static void DrawThunderIIICombo(string label, ThunderIIIAssignment current, ThunderIIIAssignment otherSet, Action<ThunderIIIAssignment> set)
    {
        ImGui.SetNextItemWidth(Width);
        if (!ImGui.BeginCombo(label, ThunderIIIPlans.First(p => p.Plan == current).Name)) return;
        var otherIsShare = otherSet is ThunderIIIAssignment.ShareMtFirst or ThunderIIIAssignment.ShareOtFirst;
        foreach (var (plan, name) in ThunderIIIPlans)
        {
            var isShare = plan is ThunderIIIAssignment.ShareMtFirst or ThunderIIIAssignment.ShareOtFirst;
            var blocked = isShare ? otherIsShare : plan == otherSet;
            if (ImGui.Selectable(name, plan == current, blocked ? ImGuiSelectableFlags.Disabled : ImGuiSelectableFlags.None)) set(plan);
            if (blocked)
                Tooltip(isShare
                    ? "The tanks already share the other buster; their big cooldowns can't come back in ~41s."
                    : "That tank already invulns the other buster; no invuln comes back in ~41s.");
        }
        ImGui.EndCombo();
    }

    private static void ConfigCheckbox(string label, bool value, Action<bool> set, string tooltip)
    {
        if (ImGui.Checkbox(label, ref value)) SetConfig(() => set(value));
        Tooltip(tooltip);
    }

    private static void SetConfig(Action change)
    {
        change();
        Plugin.Config.Save();
    }

    private static readonly System.Numerics.Vector4 SectionColour = new(0.55f, 0.8f, 1f, 1f);

    private static void Section(string title)
    {
        ImGui.Spacing();
        ImGui.TextColored(SectionColour, title);
        ImGui.Separator();
    }

    private static void Tooltip(string text)
    {
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled)) ImGui.SetTooltip(text);
    }

    private void ResetPerPlayer()
    {
        Overrides.LineNumber.Clear();
        Overrides.Accretion.Clear();
        Overrides.FirstSlapAllOnMe.Clear();
    }

    // Solo's own picks only: a host's seat assignments are kept apart.
    private void ResetMine()
    {
        Overrides.LineNumber.Mine = null;
        Overrides.Accretion.Mine = null;
        Overrides.FirstSlapAllOnMe.Mine = null;
    }
}
