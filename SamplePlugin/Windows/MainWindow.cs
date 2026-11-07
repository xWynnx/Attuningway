using System;
using System.IO;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Lumina.Excel.Sheets;

namespace SamplePlugin.Windows;

public class MainWindow : Window, IDisposable
{
    public MainWindow()
        : base("Aetheryte Tracker##Aetheryte Tracker", ImGuiWindowFlags.None)
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(375, 330),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };
    }

    public void Dispose() { }

    public override void Draw()
    {
        if (!Plugin.PlayerState.IsLoaded)
        {
            ImGui.Text("Log in to see this zone's aetherytes.");
            return;
        }

        var territoryId = Plugin.ClientState.TerritoryType;
        if (!Plugin.DataManager.GetExcelSheet<TerritoryType>().TryGetRow(territoryId, out var territoryRow))
        {
            ImGui.Text("Waiting for your current zone...");
            return;
        }

        ImGui.Text($"Current zone: {territoryRow.PlaceName.Value.Name}");
        ImGui.Separator();

        var locations = Plugin.DataManager.GetExcelSheet<Aetheryte>()
            .Where(row => row.Territory.RowId == territoryId)
            .Select(row => new
            {
                Row = row,
                Name = row.IsAetheryte
                    ? (row.PlaceName.IsValid ? row.PlaceName.Value.Name.ToString() : string.Empty)
                    : (row.AethernetName.IsValid ? row.AethernetName.Value.Name.ToString() : string.Empty),
            })
            .Where(location => !string.IsNullOrWhiteSpace(location.Name))
            .OrderByDescending(location => location.Row.IsAetheryte)
            .ThenBy(location => location.Name)
            .ToList();

        if (locations.Count == 0)
        {
            ImGui.Text("No aetherytes or aethernet shards found in this zone.");
            return;
        }

        using var child = ImRaii.Child("AetheryteList", Vector2.Zero, true);
        if (!child.Success)
        {
            return;
        }

        var pluginDirectory = Plugin.PluginInterface.AssemblyLocation.Directory;
        var iconPath = pluginDirectory == null
            ? string.Empty
            : Path.Combine(pluginDirectory.FullName, "aetheryte-icon.png");
        var iconTexture = Plugin.TextureProvider.GetFromFile(iconPath).GetWrapOrDefault();
        var iconSize = new Vector2(24f, 30f) * ImGuiHelpers.GlobalScale;

        unsafe
        {
            var uiState = UIState.Instance();
            if (uiState == null)
            {
                ImGui.Text("Waiting for the game's attunement data...");
                return;
            }

            foreach (var location in locations)
            {
                var isAttuned = uiState->IsAetheryteUnlocked(location.Row.RowId);
                var color = isAttuned
                    ? new Vector4(0.2f, 0.9f, 0.35f, 1.0f)
                    : new Vector4(1.0f, 0.25f, 0.25f, 1.0f);

                if (iconTexture != null)
                {
                    ImGui.Image(iconTexture.Handle, iconSize);
                }
                else
                {
                    ImGui.Dummy(iconSize);
                }

                ImGui.SameLine();
                ImGui.Text(location.Name);
                ImGui.SameLine();

                var status = isAttuned ? "Attuned! ✓" : "ATTUNE NEEDED ";
                ImGui.TextColored(color, status);
            }
        }
    }
}