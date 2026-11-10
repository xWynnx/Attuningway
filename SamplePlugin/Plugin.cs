using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Lumina.Excel.Sheets;
using SamplePlugin.Windows;

namespace SamplePlugin;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ITextureProvider TextureProvider { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static IPlayerState PlayerState { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;

    private const string CommandName = "/attunement";

    // TerritoryType row IDs from the current game data. Include housing districts and
    // their private chambers, houses, and company workshops so neither arrival nor
    // leaving warnings can fire while the player is inside housing.
    private static readonly HashSet<uint> WarningExcludedHousingTerritoryIds = new()
    {
        // The Mist: district, private rooms/houses, and company workshop.
        282, 283, 284, 339, 384, 423,
        // The Lavender Beds.
        340, 342, 343, 344, 385, 425,
        // The Goblet.
        341, 345, 346, 347, 386, 424,
        // Shirogane.
        641, 649, 650, 651, 652, 653,
        // Empyreum.
        979, 980, 981, 982, 983, 984,
    };

    public Configuration Configuration { get; init; }

    public readonly WindowSystem WindowSystem = new("SamplePlugin");
    private ConfigWindow ConfigWindow { get; init; } = null!;
    private MainWindow MainWindow { get; init; } = null!;
    private AttunementPopupWindow AttunementPopup { get; init; } = null!;
    private MissingAetheryteWarningWindow MissingAetheryteWarning { get; init; } = null!;
    private LeavingAetheryteWarningWindow LeavingAetheryteWarning { get; init; } = null!;
    private AllAttunedPopupWindow AllAttunedPopup { get; init; } = null!;

    private readonly Aetheryte[] aetherytes;
    private readonly Dictionary<uint, bool> attunementSnapshot = new();
    private readonly Mp3SoundPlayer soundPlayer;
    private readonly Mp3SoundPlayer areaWarningSoundPlayer;
    private readonly Mp3SoundPlayer leavingWarningSoundPlayer;
    private readonly Mp3SoundPlayer allAttunedSoundPlayer;
    private bool hasInitialSnapshot;
    private bool allAttunedPending;
    private uint previousTerritoryId;

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        var pluginDirectory = PluginInterface.AssemblyLocation.Directory?.FullName ?? System.AppContext.BaseDirectory;
        var popupImagePath = Path.Combine(pluginDirectory, "EirwynChibi.png");
        var allAttunedImagePath = Path.Combine(pluginDirectory, "RhysChibi.png");
        var soundPath = Path.Combine(pluginDirectory, "AetheryteAttuned.mp3");
        var areaWarningSoundPath = Path.Combine(pluginDirectory, "FFXIV_Error.mp3");
        var leavingWarningSoundPath = Path.Combine(pluginDirectory, "FFXIV_Instance_Failed.mp3");
        var allAttunedSoundPath = Path.Combine(pluginDirectory, "AllAetherytesAttuned.mp3");

        aetherytes = DataManager.GetExcelSheet<Aetheryte>().ToArray();
        soundPlayer = new Mp3SoundPlayer(soundPath);
        areaWarningSoundPlayer = new Mp3SoundPlayer(areaWarningSoundPath);
        leavingWarningSoundPlayer = new Mp3SoundPlayer(leavingWarningSoundPath);
        allAttunedSoundPlayer = new Mp3SoundPlayer(allAttunedSoundPath);

        ConfigWindow = new ConfigWindow(this);
        MainWindow = new MainWindow();
        AttunementPopup = new AttunementPopupWindow(popupImagePath);
        MissingAetheryteWarning = new MissingAetheryteWarningWindow();
        LeavingAetheryteWarning = new LeavingAetheryteWarningWindow();
        AllAttunedPopup = new AllAttunedPopupWindow(allAttunedImagePath);

        WindowSystem.AddWindow(ConfigWindow);
        WindowSystem.AddWindow(MainWindow);
        WindowSystem.AddWindow(AttunementPopup);
        WindowSystem.AddWindow(MissingAetheryteWarning);
        WindowSystem.AddWindow(LeavingAetheryteWarning);
        WindowSystem.AddWindow(AllAttunedPopup);

        AttunementPopup.Finished += OnAttunementPopupFinished;

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "A useful message to display in /xlhelp"
        });

        PluginInterface.UiBuilder.Draw += WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUi;
        PluginInterface.UiBuilder.OpenMainUi += ToggleMainUi;
        Framework.Update += OnFrameworkUpdate;

        Log.Information($"===A cool log message from {PluginInterface.Manifest.Name}===");
    }

    public void Dispose()
    {
        Framework.Update -= OnFrameworkUpdate;
        AttunementPopup.Finished -= OnAttunementPopupFinished;
        PluginInterface.UiBuilder.Draw -= WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi -= ToggleConfigUi;
        PluginInterface.UiBuilder.OpenMainUi -= ToggleMainUi;

        WindowSystem.RemoveAllWindows();

        ConfigWindow.Dispose();
        MainWindow.Dispose();
        AttunementPopup.Dispose();
        MissingAetheryteWarning.Dispose();
        AllAttunedPopup.Dispose();
        soundPlayer.Dispose();
        areaWarningSoundPlayer.Dispose();
        leavingWarningSoundPlayer.Dispose();
        allAttunedSoundPlayer.Dispose();

        CommandManager.RemoveHandler(CommandName);
    }

    private unsafe void OnFrameworkUpdate(IFramework framework)
    {
        if (!ClientState.IsLoggedIn || !PlayerState.IsLoaded)
        {
            hasInitialSnapshot = false;
            allAttunedPending = false;
            previousTerritoryId = 0;
            attunementSnapshot.Clear();
            return;
        }

        var currentTerritory = ClientState.TerritoryType;
        if (currentTerritory == 0)
        {
            return;
        }

        var uiState = UIState.Instance();
        if (uiState == null)
        {
            return;
        }

        if (!hasInitialSnapshot)
        {
            foreach (var aetheryte in aetherytes)
            {
                attunementSnapshot[aetheryte.RowId] = uiState->IsAetheryteUnlocked(aetheryte.RowId);
            }

            hasInitialSnapshot = true;
            previousTerritoryId = currentTerritory;
            return;
        }

        if (currentTerritory != previousTerritoryId)
        {
            var enteredHousingTerritory = IsWarningExcludedHousingTerritory(currentTerritory);
            var hasUnattunedInPreviousZone = !enteredHousingTerritory &&
                !IsWarningExcludedHousingTerritory(previousTerritoryId) &&
                HasUnattunedAetherytes(previousTerritoryId, uiState);
            if (hasUnattunedInPreviousZone)
            {
                LeavingAetheryteWarning.Show();
                leavingWarningSoundPlayer.Play();
            }

            var hasUnattunedAetheryte = false;
            if (!enteredHousingTerritory)
            {
                foreach (var aetheryte in aetherytes)
                {
                    if (aetheryte.Territory.RowId == currentTerritory && !uiState->IsAetheryteUnlocked(aetheryte.RowId))
                    {
                        hasUnattunedAetheryte = true;
                        break;
                    }
                }
            }

            if (hasUnattunedAetheryte)
            {
                MainWindow.IsOpen = true;
                MissingAetheryteWarning.Show();
                areaWarningSoundPlayer.Play();
            }

            previousTerritoryId = currentTerritory;
        }

        string? newlyAttunedLocation = null;
        foreach (var aetheryte in aetherytes)
        {
            var isAttuned = uiState->IsAetheryteUnlocked(aetheryte.RowId);
            var wasAttuned = attunementSnapshot.TryGetValue(aetheryte.RowId, out var previousState) && previousState;

            if (!wasAttuned && isAttuned && aetheryte.Territory.RowId == currentTerritory)
            {
                var locationName = GetAetheryteName(aetheryte);
                if (!string.IsNullOrWhiteSpace(locationName))
                {
                    newlyAttunedLocation = locationName;
                }
            }

            attunementSnapshot[aetheryte.RowId] = isAttuned;
        }

        if (newlyAttunedLocation == null)
        {
            return;
        }

        AttunementPopup.Show(newlyAttunedLocation);
        soundPlayer.Play();

        if (IsZoneFullyAttuned(currentTerritory, uiState))
        {
            allAttunedPending = true;
        }
    }

    private unsafe bool HasUnattunedAetherytes(uint territoryId, UIState* uiState)
    {
        foreach (var aetheryte in aetherytes)
        {
            if (aetheryte.Territory.RowId == territoryId &&
                !string.IsNullOrWhiteSpace(GetAetheryteName(aetheryte)) &&
                !uiState->IsAetheryteUnlocked(aetheryte.RowId))
            {
                return true;
            }
        }

        return false;
    }

    private bool IsWarningExcludedHousingTerritory(uint territoryId)
    {
        return WarningExcludedHousingTerritoryIds.Contains(territoryId);
    }

    private unsafe bool IsZoneFullyAttuned(uint territoryId, UIState* uiState)
    {
        var foundLocation = false;
        foreach (var aetheryte in aetherytes)
        {
            if (aetheryte.Territory.RowId != territoryId ||
                string.IsNullOrWhiteSpace(GetAetheryteName(aetheryte)))
            {
                continue;
            }

            foundLocation = true;
            if (!uiState->IsAetheryteUnlocked(aetheryte.RowId))
            {
                return false;
            }
        }

        return foundLocation;
    }

    private void OnAttunementPopupFinished()
    {
        if (!allAttunedPending)
        {
            return;
        }

        allAttunedPending = false;
        AllAttunedPopup.Show();
        allAttunedSoundPlayer.Play();
    }

    private static string GetAetheryteName(Aetheryte aetheryte)
    {
        if (aetheryte.IsAetheryte)
        {
            return aetheryte.PlaceName.IsValid ? aetheryte.PlaceName.Value.Name.ToString() : string.Empty;
        }

        return aetheryte.AethernetName.IsValid ? aetheryte.AethernetName.Value.Name.ToString() : string.Empty;
    }

    private void OnCommand(string command, string args) => MainWindow.Toggle();

    public void ToggleConfigUi() => ConfigWindow.Toggle();
    public void ToggleMainUi() => MainWindow.Toggle();
}
