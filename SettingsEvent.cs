using System;
using Build1.PostMVC.Core.MVCS.Events;

namespace Build1.PostMVC.Unity.Settings
{
    public static class SettingsEvent
    {
        public static readonly Event            LoadSuccess    = new(typeof(SettingsEvent), nameof(LoadSuccess));
        public static readonly Event<Exception> LoadFail       = new(typeof(SettingsEvent), nameof(LoadFail));
        public static readonly Event            UnloadSuccess  = new(typeof(SettingsEvent), nameof(UnloadSuccess));
        public static readonly Event<Exception> UnloadFail     = new(typeof(SettingsEvent), nameof(UnloadFail));
        public static readonly Event<Setting>   SettingChanged = new(typeof(SettingsEvent), nameof(SettingChanged));
        public static readonly Event<Exception> SaveFail       = new(typeof(SettingsEvent), nameof(SaveFail));
        public static readonly Event            Reset          = new(typeof(SettingsEvent), nameof(Reset));
    }
}