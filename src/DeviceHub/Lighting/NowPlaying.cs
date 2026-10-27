using Windows.Media.Control;

namespace DeviceHub.Lighting;

/// <summary>
/// The song Windows knows is playing (Spotify, a browser tab, the Media Player…): the same source as the media
/// flyout over the volume keys. Read only; nothing is controlled.
/// </summary>
internal static class NowPlaying
{
    public sealed record Track(string Title, string Artist, bool Playing, TimeSpan Position, TimeSpan Duration);

    private static GlobalSystemMediaTransportControlsSessionManager? manager;

    public static async Task<Track?> GetAsync()
    {
        try
        {
            manager ??= await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            var session = manager.GetCurrentSession();
            if (session is null) return null;
            var props = await session.TryGetMediaPropertiesAsync();
            if (props is null || string.IsNullOrWhiteSpace(props.Title)) return null;
            var playback = session.GetPlaybackInfo();
            var timeline = session.GetTimelineProperties();
            var playing = playback.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
            // The timeline is a snapshot; move it forward by the time since it was taken while the song plays.
            var position = timeline.Position + (playing ? DateTimeOffset.Now - timeline.LastUpdatedTime : TimeSpan.Zero);
            var duration = timeline.EndTime - timeline.StartTime;
            if (position > duration) position = duration;
            return new Track(props.Title, props.Artist ?? "", playing, position < TimeSpan.Zero ? TimeSpan.Zero : position, duration);
        }
        catch (Exception) { manager = null; return null; } // the media service can restart; ask again next time
    }
}
