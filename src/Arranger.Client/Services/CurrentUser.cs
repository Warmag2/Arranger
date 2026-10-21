using System;
using System.Threading.Tasks;
using Faemiyah.Arranger.Contracts.Users;

namespace Faemiyah.Arranger.Client.Services;

/// <summary>
/// The signed-in user's settings, loaded once, and conversion of times into their time zone.
/// </summary>
public sealed class CurrentUser
{
    private readonly ArrangerApi _api;
    private UserSettings? _settings;
    private TimeZoneInfo _timeZone = TimeZoneInfo.Utc;

    /// <summary>
    /// Initializes a new instance of the <see cref="CurrentUser"/> class.
    /// </summary>
    /// <param name="api">The API.</param>
    public CurrentUser(ArrangerApi api)
    {
        _api = api;
    }

    /// <summary>
    /// Get the settings, loading them on first use.
    /// </summary>
    /// <returns>The settings, or <b>null</b> when they could not be loaded.</returns>
    public async Task<UserSettings?> GetSettingsAsync()
    {
        if (_settings == null)
        {
            var result = await _api.GetAsync<UserSettings>("api/me/settings");
            if (result.IsSuccess)
            {
                Update(result.Value);
            }
        }

        return _settings;
    }

    /// <summary>
    /// Replace the settings after they were changed.
    /// </summary>
    /// <param name="settings">The new settings.</param>
    public void Update(UserSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _settings = settings;
        _timeZone = TimeZoneInfo.TryFindSystemTimeZoneById(settings.TimeZoneId, out var timeZone) ? timeZone : TimeZoneInfo.Utc;
    }

    /// <summary>
    /// Gets the user's time zone, or UTC until the settings are loaded.
    /// </summary>
    public TimeZoneInfo TimeZone => _timeZone;

    /// <summary>
    /// Convert a time in the user's time zone into UTC.
    /// </summary>
    /// <param name="date">The local date.</param>
    /// <param name="time">The local time of day.</param>
    /// <returns>The UTC time, or <b>null</b> when the time is skipped by a daylight saving change.</returns>
    public DateTime? ToUtc(DateOnly date, TimeOnly time)
    {
        var local = date.ToDateTime(time, DateTimeKind.Unspecified);
        return _timeZone.IsInvalidTime(local) ? null : TimeZoneInfo.ConvertTimeToUtc(local, _timeZone);
    }

    /// <summary>
    /// Convert a UTC time into the user's time zone.
    /// </summary>
    /// <param name="utc">The UTC time.</param>
    /// <returns>The local time.</returns>
    public DateTime ToLocalTime(DateTime utc)
    {
        return TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), _timeZone);
    }
}
