using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;

namespace SamplePlugin.Windows;

public sealed class AttunementPopupWindow : Window, IDisposable
{
    private const float EntranceDuration = 0.65f;
    private const double HoldDuration = 2.35;
    private const double FadeDuration = 0.65;
    private readonly string imagePath;
    private string locationName = string.Empty;
    private double shownAt;

    public event Action? Finished;

    public AttunementPopupWindow(string imagePath)
        : base(
            "Attunement Celebration###AetheryteTrackerAttunementPopup",
            ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoBackground |
            ImGuiWindowFlags.NoInputs | ImGuiWindowFlags.NoSavedSettings |
            ImGuiWindowFlags.NoFocusOnAppearing)
    {
        this.imagePath = imagePath;
        IsOpen = false;
        Size = new Vector2(520f, 690f);
        SizeCondition = ImGuiCond.Always;
    }

    public void Show(string location)
    {
        locationName = location;
        shownAt = ImGui.GetTime();
        IsOpen = true;
    }

    public override void PreDraw()
    {
        var viewport = ImGui.GetMainViewport();
        Size = new Vector2(520f, 690f);
        SizeCondition = ImGuiCond.Always;
        Position = viewport.Pos + (viewport.Size - Size) * 0.5f;
        PositionCondition = ImGuiCond.Always;
    }

    public override void Draw()
    {
        var elapsed = ImGui.GetTime() - shownAt;
        if (elapsed >= HoldDuration + FadeDuration)
        {
            IsOpen = false;
            Finished?.Invoke();
            return;
        }

        var scale = 1f;
        var opacity = 1f;
        if (elapsed < EntranceDuration)
        {
            var entranceProgress = Math.Clamp((float)(elapsed / EntranceDuration), 0f, 1f);
            var shiftedProgress = entranceProgress - 1f;
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            scale = 1f + c3 * MathF.Pow(shiftedProgress, 3f) + c1 * MathF.Pow(shiftedProgress, 2f);
            scale = Math.Max(scale, 0.05f);
        }
        else if (elapsed >= HoldDuration)
        {
            var fadeProgress = Math.Clamp((float)((elapsed - HoldDuration) / FadeDuration), 0f, 1f);
            opacity = 1f - fadeProgress;
        }

        var imageSize = new Vector2(420f, 560f) * scale;
        var windowWidth = ImGui.GetWindowSize().X;
        const float reservedImageHeight = 575f;
        ImGui.PushStyleVar(ImGuiStyleVar.Alpha, opacity);

        ImGui.SetCursorPos(new Vector2(
            (windowWidth - imageSize.X) * 0.5f,
            8f + (reservedImageHeight - imageSize.Y) * 0.5f));

        var image = Plugin.TextureProvider.GetFromFile(imagePath).GetWrapOrDefault();
        if (image != null)
        {
            ImGui.Image(image.Handle, imageSize);
        }

        ImGui.SetCursorPosY(610f);
        const string successMessage = "Aetheryte Attuned!";
        ImGui.SetCursorPosX((windowWidth - ImGui.CalcTextSize(successMessage).X) * 0.5f);
        ImGui.TextColored(new Vector4(0.2f, 0.9f, 0.35f, 1f), successMessage);

        ImGui.SetCursorPosY(640f);
        ImGui.SetCursorPosX((windowWidth - ImGui.CalcTextSize(locationName).X) * 0.5f);
        ImGui.Text(locationName);

        ImGui.PopStyleVar();
    }

    public void Dispose() { }
}