using System.Runtime.InteropServices;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Memory;
using CounterStrikeSharp.API.Modules.Memory.DynamicFunctions;
using Microsoft.Extensions.Logging;

namespace AstraSkins;

internal sealed class LoadoutItemViewStore : IDisposable
{
    private const string ConstructorKey = "AstraSkins_CEconItemView_Constructor";
    private const string OperatorEqualsKey = "AstraSkins_CEconItemView_OperatorEquals";

    private readonly ILogger _logger;
    private readonly MemoryFunctionWithReturn<nint, nint>? _constructor;
    private readonly MemoryFunctionWithReturn<nint, nint, nint>? _operatorEquals;
    private readonly Dictionary<(ulong SteamId, int Team, int Slot), nint> _views = new();
    private bool _errorLogged;
    private bool _disposed;

    public LoadoutItemViewStore(ILogger logger)
    {
        _logger = logger;

        try
        {
            var signature = GameData.GetSignature(ConstructorKey);
            if (string.IsNullOrWhiteSpace(signature))
            {
                _logger.LogError(
                    "Astra Skins gamedata signature {SignatureKey} is missing. Copy astra_skins.json to addons/counterstrikesharp/gamedata/.",
                    ConstructorKey);
            }
            else
            {
                _constructor = new MemoryFunctionWithReturn<nint, nint>(signature);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Astra Skins failed to load gamedata signature {SignatureKey}. Buy-menu weapon previews will stay vanilla.",
                ConstructorKey);
        }

        try
        {
            var signature = GameData.GetSignature(OperatorEqualsKey);
            if (!string.IsNullOrWhiteSpace(signature))
            {
                _operatorEquals = new MemoryFunctionWithReturn<nint, nint, nint>(signature);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Astra Skins failed to load gamedata signature {SignatureKey}. Loadout views will be constructed without copying the original item.",
                OperatorEqualsKey);
        }
    }

    public bool NativeAvailable => _constructor is not null;

    public bool TryGetOrCreate(ulong steamId, int team, int slot, nint copyFrom, out nint itemView)
    {
        itemView = nint.Zero;
        if (_disposed || _constructor is null)
        {
            return false;
        }

        var key = (steamId, team, slot);
        if (_views.TryGetValue(key, out itemView))
        {
            return itemView != nint.Zero;
        }

        try
        {
            var classSize = Schema.GetClassSize("CEconItemView");
            if (classSize <= 0)
            {
                return false;
            }

            itemView = Marshal.AllocHGlobal(classSize);
            try
            {
                _constructor.Invoke(itemView);
                if (copyFrom != nint.Zero && _operatorEquals is not null)
                {
                    _operatorEquals.Invoke(itemView, copyFrom);
                }

                _views[key] = itemView;
                return true;
            }
            catch
            {
                Marshal.FreeHGlobal(itemView);
                itemView = nint.Zero;
                throw;
            }
        }
        catch (Exception ex)
        {
            if (!_errorLogged)
            {
                _errorLogged = true;
                _logger.LogError(ex, "Astra Skins failed to construct a persistent loadout item view.");
            }

            itemView = nint.Zero;
            return false;
        }
    }

    public void Clear(ulong steamId)
    {
        foreach (var key in _views.Keys.Where(key => key.SteamId == steamId).ToArray())
        {
            if (_views.Remove(key, out var handle) && handle != nint.Zero)
            {
                Marshal.FreeHGlobal(handle);
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        foreach (var handle in _views.Values)
        {
            if (handle != nint.Zero)
            {
                Marshal.FreeHGlobal(handle);
            }
        }

        _views.Clear();
    }
}
