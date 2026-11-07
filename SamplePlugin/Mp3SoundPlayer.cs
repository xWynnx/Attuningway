using System;
using System.IO;
using Dalamud.Plugin.Services;
using NAudio.Wave;

namespace SamplePlugin;

internal sealed class Mp3SoundPlayer : IDisposable
{
    private readonly IPluginLog log;
    private AudioFileReader? audioFile;
    private WaveOutEvent? output;

    public Mp3SoundPlayer(string path)
    {
        log = Plugin.Log;
        if (!File.Exists(path))
        {
            log.Warning($"Aetheryte attunement sound was not found: {path}");
            return;
        }

        try
        {
            audioFile = new AudioFileReader(path);
            output = new WaveOutEvent();
            output.Init(audioFile);
        }
        catch (Exception ex)
        {
            log.Warning($"Could not prepare the aetheryte attunement sound: {ex.Message}");
            output?.Dispose();
            audioFile?.Dispose();
            output = null;
            audioFile = null;
        }
    }

    public void Play()
    {
        if (audioFile == null || output == null)
        {
            return;
        }

        try
        {
            if (output.PlaybackState == PlaybackState.Playing)
            {
                output.Stop();
            }

            audioFile.Position = 0;
            output.Play();
        }
        catch (Exception ex)
        {
            log.Warning($"Could not play the aetheryte attunement sound: {ex.Message}");
        }
    }

    public void Dispose()
    {
        output?.Dispose();
        audioFile?.Dispose();
    }
}