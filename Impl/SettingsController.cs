using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Build1.PostMVC.Core.MVCS.Events;
using Build1.PostMVC.Core.MVCS.Injection;
using Build1.PostMVC.Unity.App.Modules.App;
using Build1.PostMVC.Unity.App.Modules.Logging;
using Newtonsoft.Json;

namespace Build1.PostMVC.Unity.Settings.Impl
{
    internal sealed class SettingsController : ISettingsController
    {
        public const string SettingsFileName = "settings.json";

        [Log(LogLevel.Warning)] public ILog             Log           { get; set; }
        [Inject]                public IEventDispatcher Dispatcher    { get; set; }
        [Inject]                public IAppController   AppController { get; set; }

        public bool   Initialized          => _settings != null;
        public bool   DeviceSettingsLoaded => _deviceSettingsValues != null;
        public bool   UserSettingsLoaded   => _userSettingsValues != null;
        public string UserId               { get; private set; }

        private IEnumerable<Setting> _settings;

        private Dictionary<string, object> _deviceSettingsValues;
        private bool                       _deviceSettingsDirty;

        private Dictionary<string, object> _userSettingsValues;
        private bool                       _userSettingsDirty;

        [PostConstruct]
        public void PostConstruct()
        {
            Dispatcher.AddListener(AppEvent.Pause, OnAppPause);
            Dispatcher.AddListener(AppEvent.Restarting, OnAppRestarting);
            Dispatcher.AddListener(AppEvent.Quitting, OnAppQuitting);
        }

        [PreDestroy]
        public void PreDestroy()
        {
            Dispatcher.RemoveListener(AppEvent.Pause, OnAppPause);
            Dispatcher.RemoveListener(AppEvent.Restarting, OnAppRestarting);
            Dispatcher.RemoveListener(AppEvent.Quitting, OnAppQuitting);
        }

        /*
         * Initialization.
         */

        public void Initialize(IEnumerable<Setting> settings)
        {
            if (_settings != null)
                throw new Exception("Settings already initialized");

            _settings = settings;
        }

        /*
         * Device Settings.
         */

        public void LoadDeviceSettings()
        {
            if (!Initialized)
            {
                Dispatcher.Dispatch(SettingsEvent.LoadResult, new SettingsResult(SettingType.Device, SettingsErrorCode.SettingsNotInitialized));
                return;
            }

            if (DeviceSettingsLoaded)
            {
                Dispatcher.Dispatch(SettingsEvent.LoadResult, new SettingsResult(SettingType.Device, SettingsErrorCode.DeviceSettingsAlreadyLoaded));
                return;
            }

            Log.Debug("Loading device settings...");

            try
            {
                var path = Path.Combine(AppController.PersistentDataPath, SettingsFileName);
                if (!File.Exists(path))
                {
                    _deviceSettingsValues = new Dictionary<string, object>();

                    Log.Debug("Device settings file doesn't exist. Empty collection initialized.");
                }
                else
                {
                    Log.Debug(p => $"Reading device settings file... Path: {p}", path);

                    var json = File.ReadAllText(path);

                    _deviceSettingsValues = ParseSettingsJson(json, SettingType.Device);
                }
            }
            catch (Exception exception)
            {
                Log.Error(exception);
                Dispatcher.Dispatch(SettingsEvent.LoadResult, new SettingsResult(SettingType.Device, exception));
                return;
            }

            Log.Debug("Device settings loaded");

            Dispatcher.Dispatch(SettingsEvent.LoadResult, new SettingsResult(SettingType.Device));
        }

        public bool CheckDeviceSettingsWasEverChanged()
        {
            var path = Path.Combine(AppController.PersistentDataPath, SettingsFileName);
            return File.Exists(path);
        }

        /*
         * User Settings.
         */

        public void LoadUserSettings(string userId)
        {
            if (!Initialized)
            {
                Dispatcher.Dispatch(SettingsEvent.LoadResult, new SettingsResult(SettingType.User, SettingsErrorCode.SettingsNotInitialized));
                return;
            }

            if (UserSettingsLoaded)
            {
                if (UserId == userId)
                {
                    Dispatcher.Dispatch(SettingsEvent.LoadResult, new SettingsResult(SettingType.User, SettingsErrorCode.UserSettingsAlreadyLoaded));
                    return;
                }

                Log.Warn("Another user settings loading requested. Previously loaded settings will be unloaded.");
            }

            Log.Debug(i => $"Loading user settings... UserId: {i}", userId);

            try
            {
                var path = Path.Combine(AppController.PersistentDataPath, userId, SettingsFileName);
                if (!File.Exists(path))
                {
                    _userSettingsValues = new Dictionary<string, object>();

                    Log.Debug("User settings file doesn't exist. Empty collection initialized.");
                }
                else
                {
                    Log.Debug(p => $"Reading user settings file... Path: {p}", path);

                    var json = File.ReadAllText(path);

                    _userSettingsValues = ParseSettingsJson(json, SettingType.User);
                }
            }
            catch (Exception exception)
            {
                Log.Error(exception);
                Dispatcher.Dispatch(SettingsEvent.LoadResult, new SettingsResult(SettingType.User, exception));
                return;
            }

            Log.Debug(i => $"User settings loaded. UserId: {i}", userId);

            UserId = userId;

            Dispatcher.Dispatch(SettingsEvent.LoadResult, new SettingsResult(SettingType.User));
        }

        public void UnloadUserSettings()
        {
            if (!UserSettingsLoaded)
                return;

            _userSettingsValues = null;

            UserId = null;

            Dispatcher.Dispatch(SettingsEvent.Unload, SettingType.User);
        }

        public bool CheckSettingSet<T>(Setting<T> setting) where T : struct
        {
            switch (setting.type)
            {
                case SettingType.Device:
                    if (!DeviceSettingsLoaded)
                        throw new Exception("Device settings not loaded");
                    return _deviceSettingsValues.ContainsKey(setting.key);
                
                case SettingType.User:
                    if (!UserSettingsLoaded)
                        throw new Exception("User settings not loaded");
                    return _userSettingsValues.ContainsKey(setting.key);
                
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        /*
         * Management.
         */

        public T Get<T>(Setting<T> setting) where T : struct
        {
            switch (setting.type)
            {
                case SettingType.Device:

                    if (!DeviceSettingsLoaded)
                        throw new Exception("Device settings not loaded");

                    return GetSettingValueFrom(_deviceSettingsValues, setting);

                case SettingType.User:

                    if (!UserSettingsLoaded)
                        throw new Exception("User settings not loaded");

                    return GetSettingValueFrom(_userSettingsValues, setting);

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public void Set<T>(Setting<T> setting, T value) where T : struct
        {
            switch (setting.type)
            {
                case SettingType.Device:

                    if (!DeviceSettingsLoaded)
                        throw new Exception("Device settings not loaded");

                    if (_deviceSettingsValues.ContainsKey(setting.key) && EqualityComparer<T>.Default.Equals(GetSettingValueFrom(_deviceSettingsValues, setting), value))
                        return;

                    _deviceSettingsValues[setting.key] = value;
                    _deviceSettingsDirty = true;

                    break;

                case SettingType.User:

                    if (!UserSettingsLoaded)
                        throw new Exception("User settings not loaded");

                    if (_userSettingsValues.ContainsKey(setting.key) && EqualityComparer<T>.Default.Equals(GetSettingValueFrom(_userSettingsValues, setting), value))
                        return;

                    _userSettingsValues[setting.key] = value;
                    _userSettingsDirty = true;

                    break;

                default:
                    throw new ArgumentOutOfRangeException();
            }

            Dispatcher.Dispatch(SettingsEvent.Changed, setting);
        }

        /*
         * Resetting.
         */

        public void Reset(SettingType type)
        {
            if ((type & SettingType.Device) == SettingType.Device)
            {
                if (!DeviceSettingsLoaded)
                    throw new Exception("Device settings not loaded");

                Log.Debug("Resetting device settings...");

                var path = Path.Combine(AppController.PersistentDataPath, SettingsFileName);
                if (File.Exists(path))
                    File.Delete(path);

                _deviceSettingsValues.Clear();

                Log.Debug("Device settings reset");
            }

            if ((type & SettingType.User) == SettingType.User)
            {
                if (!UserSettingsLoaded)
                    throw new Exception("User settings not loaded");

                Log.Debug("Resetting user settings...");

                var path = Path.Combine(AppController.PersistentDataPath, UserId, SettingsFileName);
                if (File.Exists(path))
                    File.Delete(path);

                _userSettingsValues.Clear();

                Log.Debug(i => $"User settings reset. UserId: {i}", UserId);
            }

            Dispatcher.Dispatch(SettingsEvent.Reset, type);
        }

        /*
         * Saving.
         */

        public void Save(SettingType type)
        {
            if ((type & SettingType.Device) == SettingType.Device)
            {
                if (!DeviceSettingsLoaded)
                {
                    Log.Debug("Saving. Device settings not loaded");
                }
                else
                {
                    if (!_deviceSettingsDirty)
                    {
                        Log.Debug("Device settings not dirty");
                    }
                    else
                    {
                        Log.Debug("Saving device settings...");

                        // We put in try/catch as this operation is performed when app is shutting down or put on pause.
                        // If it fails with an exception, other operations must not be interrupted.   
                        try
                        {
                            var path = Path.Combine(AppController.PersistentDataPath, SettingsFileName);
                            var json = JsonConvert.SerializeObject(_deviceSettingsValues);

                            Log.Debug(j => $"Json: {j}", json);

                            File.WriteAllText(path, json);

                            _deviceSettingsDirty = false;

                            Log.Debug("Device settings saved");
                        }
                        catch (Exception exception)
                        {
                            Log.Error(exception);
                        }
                    }
                }
            }

            if ((type & SettingType.User) == SettingType.User)
            {
                if (!UserSettingsLoaded)
                {
                    Log.Debug("Saving. User settings not loaded");
                }
                else
                {
                    if (!_userSettingsDirty)
                    {
                        Log.Debug("User settings not dirty");
                    }
                    else
                    {
                        Log.Debug("Saving user settings...");

                        // We put in try/catch as this operation is performed when app is shutting down or put on pause.
                        // If it fails with an exception, other operations must not be interrupted.   
                        try
                        {
                            var pathDir = Path.Combine(AppController.PersistentDataPath, UserId);
                            var path = Path.Combine(pathDir, SettingsFileName);
                            var json = JsonConvert.SerializeObject(_userSettingsValues);

                            Log.Debug(j => $"Json: {j}", json);

                            if (!Directory.Exists(pathDir))
                                Directory.CreateDirectory(pathDir);
                            
                            File.WriteAllText(path, json);

                            _userSettingsDirty = false;

                            Log.Debug(i => $"User settings saved. UserId: {i}", UserId);
                        }
                        catch (Exception exception)
                        {
                            Log.Error(exception);
                        }
                    }
                }
            }
        }

        /*
         * Helpers.
         */

        private Dictionary<string, object> ParseSettingsJson(string json, SettingType type)
        {
            Log.Debug("Parsing settings json...");
            Log.Debug(j => $"Json: {j}", json);

            var settings = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
            var values = new Dictionary<string, object>();

            foreach (var setting in _settings)
            {
                if (setting.type != type)
                {
                    Log.Debug((t, k) => $"Setting has unmatched type and will be ignored. Type: {t} Key: {k}", type, setting.key);
                    continue;
                }

                if (!settings.TryGetValue(setting.key, out var value))
                    continue;

                Log.Debug(() => setting.key + ": " + value);

                if (bool.TryParse(value, out var valueBool))
                    values[setting.key] = valueBool;
                else if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var valueInt))
                    values[setting.key] = valueInt;
                else if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var valueFloat))
                    values[setting.key] = valueFloat;
            }

            return values;
        }

        private T GetSettingValueFrom<T>(IReadOnlyDictionary<string, object> values, Setting<T> setting) where T : struct
        {
            if (!values.TryGetValue(setting.key, out var value))
                return setting.DefaultValue;

            if (typeof(T).IsEnum)
                return (T)Enum.ToObject(typeof(T), Convert.ToInt32(value));

            return (T)Convert.ChangeType(value, typeof(T));
        }

        /*
         * Event Handlers.
         */

        private void OnAppPause(bool paused)
        {
            if (paused)
                Save(SettingType.Device | SettingType.User);
        }

        private void OnAppRestarting() { Save(SettingType.Device | SettingType.User); }
        private void OnAppQuitting()   { Save(SettingType.Device | SettingType.User); }
    }
}