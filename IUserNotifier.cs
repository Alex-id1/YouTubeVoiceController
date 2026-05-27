namespace YouTubeVoiceController{
    public enum NotifyLevel { Info, Warn }

    /// <summary>
    /// Abstraction over user-facing feedback channel.
    /// Current implementation writes to the on-screen log box.
    /// Future implementations may add text-to-speech
    /// </summary>
    public interface IUserNotifier{
        /// <summary>
        /// Show <paramref name="message"/> in the UI log.
        /// If <paramref name="speechText"/> is provided, TTS speaks that instead of <paramref name="message"/>.
        /// Pass an empty string to suppress TTS entirely for this notification.
        /// <para>
        /// <paramref name="muteMic"/>: when true (default) the mic is muted while TTS plays to prevent feedback loops.
        /// Set to false for short cue phrases that contain no grammar keywords (e.g. the wake-word "ok" cue)
        /// so the user's command spoken right after the cue is not dropped
        /// </para>
        /// </summary>
        void Notify(string message, NotifyLevel level = NotifyLevel.Info, string? speechText = null, bool muteMic = true);
    }
}