using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace SamplePlugin.Windows;

public sealed class AllAttunedPopupWindow : Window, IDisposable
{
    private const float EntranceDuration = 0.65f;
    private const double HoldDuration = 3.25;
    private const double FadeDuration = 0.75;
    private const double ConfettiDuration = 2.5;
    private static readonly Vector4[] ConfettiColors =
    [
        new(1f, 0.35f, 0.45f, 1f),
        new(1f, 0.78f, 0.25f, 1f),
        new(0.35f, 0.85f, 1f, 1f),
        new(0.55f, 0.95f, 0.48f, 1f),
        new(0.8f, 0.5f, 1f, 1f),
    ];

    private readonly string imagePath;
    private double shownAt;

    public AllAttunedPopupWindow(string imagePath)
        : base(
            "All Aetherytes Attuned###AetheryteTrackerAllAttunedPopup",
            ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoBackground |
            ImGuiWindowFlags.NoInputs | ImGuiWindowFlags.NoSavedSettings |
            ImGuiWindowFlags.NoFocusOnAppearing)
    {
        this.imagePath = imagePath;
        IsOpen = false;
        Size = new Vector2(540f, 810f);
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
        Size = new Vector2(540f, 810f);
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
            return;
        }

        var scale = 1f;
        if (elapsed < EntranceDuration)
        {
            var progress = Math.Clamp((float)(elapsed / EntranceDuration), 0f, 1f);
            var shiftedProgress = progress - 1f;
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            scale = Math.Max(0.05f, 1f + c3 * MathF.Pow(shiftedProgress, 3f) + c1 * MathF.Pow(shiftedProgress, 2f));
        }

        var opacity = elapsed >= HoldDuration
            ? 1f - Math.Clamp((float)((elapsed - HoldDuration) / FadeDuration), 0f, 1f)
            : 1f;

        const float imageWidth = 420f;
        const float imageHeight = 620f;
        var windowWidth = ImGui.GetWindowSize().X;
        ImGui.PushStyleVar(ImGuiStyleVar.Alpha, opacity);

        var imageSize = new Vector2(imageWidth, imageHeight) * scale;
        ImGui.SetCursorPos(new Vector2(
            (windowWidth - imageSize.X) * 0.5f,
            12f + (650f - imageSize.Y) * 0.5f));

        var image = Plugin.TextureProvider.GetFromFile(imagePath).GetWrapOrDefault();
        if (image != null)
        {
            ImGui.Image(image.Handle, imageSize);
        }

        const string successMessage = "ALL AETHERYTES ATTUNED!";
        ImGui.SetCursorPosY(690f);
        ImGui.SetWindowFontScale(1.6f);
        ImGui.SetCursorPosX((windowWidth - ImGui.CalcTextSize(successMessage).X) * 0.5f);
        ImGui.TextColored(new Vector4(1f, 0.85f, 0.25f, 1f), successMessage);
        ImGui.SetWindowFontScale(1f);
        ImGui.PopStyleVar();

        if (elapsed < ConfettiDuration)
        {
            DrawConfetti(elapsed);
        }
    }

    private static void DrawConfetti(double elapsed)
    {
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetWindowPos();
        var windowSize = ImGui.GetWindowSize();

        for (var i = 0; i < 32; i++)
        {
            var speed = 90f + (i % 6) * 22f;
            var y = ((i * 43f + (float)elapsed * speed) % (windowSize.Y + 24f)) - 12f;
            var baseX = (i * 79f + 17f) % windowSize.X;
            var x = baseX + MathF.Sin((float)elapsed * 4f + i) * 20f;
            var pieceWidth = 5f + (i % 4) * 2f;
            var pieceHeight = 9f + (i % 5) * 2f;
            var color = ImGui.GetColorU32(ConfettiColors[i % ConfettiColors.Length]);
            var topLeft = origin + new Vector2(x, y);

            drawList.AddRectFilled(
                topLeft,
                topLeft + new Vector2(pieceWidth, pieceHeight),
                color,
                2f);
        }
    }

    public void Dispose() { }
}