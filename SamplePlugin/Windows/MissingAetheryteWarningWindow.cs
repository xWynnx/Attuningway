using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace SamplePlugin.Windows;

public sealed class MissingAetheryteWarningWindow : Window, IDisposable
{
    private const double DisplayDuration = 4.0;
    private const double FadeDuration = 0.75;
    private double shownAt;

    public MissingAetheryteWarningWindow()
        : base(
            "Missing Aetheryte Attunement###AetheryteTrackerMissingWarning",
            ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoInputs |
            ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoFocusOnAppearing)
    {
        IsOpen = false;
        Size = new Vector2(1050f, 170f);
        SizeCondition = ImGuiCond.Always;
    }

    public void Show()
    {
        shownAt = ImGui.GetTime();
        IsOpen = true;
    }

    public override void PreDraw()
    {
        var viewport = ImGui.GetMainViewport();
        Size = new Vector2(1050f, 170f);
        SizeCondition = ImGuiCond.Always;
        Position = viewport.Pos + (viewport.Size - Size) * 0.5f;
        PositionCondition = ImGuiCond.Always;
    }

    public override void Draw()
    {
        var elapsed = ImGui.GetTime() - shownAt;
        if (elapsed >= DisplayDuration)
        {
            IsOpen = false;
            return;
        }

        var opacity = elapsed >= DisplayDuration - FadeDuration
            ? 1f - (float)((elapsed - (DisplayDuration - FadeDuration)) / FadeDuration)
            : 1f;

        const string warning = "MISSING AETHERYTE ATTUNEMENT";
        ImGui.PushStyleVar(ImGuiStyleVar.Alpha, opacity);
        ImGui.SetWindowFontScale(2.2f);
        var textWidth = ImGui.CalcTextSize(warning).X;
        ImGui.SetCursorPosX((ImGui.GetWindowSize().X - textWidth) * 0.5f);
        ImGui.SetCursorPosY(55f);
        ImGui.TextColored(new Vector4(1f, 0.2f, 0.15f, 1f), warning);
        ImGui.SetWindowFontScale(1f);
        ImGui.PopStyleVar();
    }

    public void Dispose() { }
}