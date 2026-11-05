using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SensEssentials;

public sealed class KnownIdsStorage
{
    private readonly HashSet<ulong> _steamIds;
    private readonly string _filePath;

    public KnownIdsStorage(string filePath)
    {
        _filePath = filePath;
        _steamIds = [];
    }

    public void Read()
    {
        try
        {
            if (!File.Exists(_filePath)) return;

            _steamIds.Clear();
            foreach (var line in File.ReadAllLines(_filePath))
            {
                if (ulong.TryParse(line.Trim(), out var steamId))
                {
                    _steamIds.Add(steamId);
                }
            }
        }
        catch (Exception e)
        {
            Log.Error(e, $"Failed to read known Steam IDs from {_filePath}");
        }
    }

    public bool Contains(ulong steamId)
    {
        return _steamIds.Contains(steamId);
    }

    public void Add(ulong steamId)
    {
        if (!_steamIds.Add(steamId))
            return;

        try
        {
            File.AppendAllLines(_filePath, [$"{steamId}"]);
        }
        catch (Exception e)
        {
            // The id stays known for this session and is written with the next new player.
            Log.Error(e, $"Failed to save known Steam IDs to {_filePath}");
        }
    }
}
