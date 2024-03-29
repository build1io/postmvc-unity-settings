using Build1.PostMVC.Core.MVCS.Events;

namespace Build1.PostMVC.Unity.Settings
{
    public static class SettingsEvent
    {
        public static readonly Event<SettingsResult> LoadResult = new(typeof(SettingsEvent), nameof(LoadResult));
        public static readonly Event<SettingType>    Unload     = new(typeof(SettingsEvent), nameof(Unload));
        public static readonly Event<SettingType>    Reset      = new(typeof(SettingsEvent), nameof(Reset));
        public static readonly Event<Setting>        Changed    = new(typeof(SettingsEvent), nameof(Changed));
    }
}