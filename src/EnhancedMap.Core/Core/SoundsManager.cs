using System;

namespace EnhancedMap.Core
{
    public enum SOUNDS_TYPE
    {
        NONE = 0,

        PANIC,
        CHATMSG,
        SIGNAL
    }

    /// <summary>
    ///     Cross-platform stub. The original implementation used System.Media.SoundPlayer
    ///     which is Windows-only. Audio playback is delegated to the UI layer / future backend.
    /// </summary>
    public static class SoundsManager
    {
        public static event Action<SOUNDS_TYPE> SoundRequested;

        public static void Play(SOUNDS_TYPE type)
        {
            if (type == SOUNDS_TYPE.NONE)
                return;

            SoundRequested?.Invoke(type);
        }
    }
}
