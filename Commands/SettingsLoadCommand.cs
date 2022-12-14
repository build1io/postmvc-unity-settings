using System;
using Build1.PostMVC.Core.MVCS.Commands;
using Build1.PostMVC.Core.MVCS.Events;
using Build1.PostMVC.Core.MVCS.Injection;
using Build1.PostMVC.Unity.App.Modules.Logging;

namespace Build1.PostMVC.Unity.Settings.Commands
{
    public sealed class SettingsLoadCommand : Command
    {
        [Log(LogLevel.Warning)] public ILog                Log                { get; set; }
        [Inject]                public IEventDispatcher    Dispatcher         { get; set; }
        [Inject]                public ISettingsController SettingsController { get; set; }

        public override void Execute()
        {
            if (SettingsController.IsLoaded)
            {
                Log.Debug("Settings already loaded");
                return;
            }

            Log.Debug("Loading settings...");

            Retain();

            Dispatcher.AddListener(SettingsEvent.LoadSuccess, OnSuccess);
            Dispatcher.AddListener(SettingsEvent.LoadFail, OnFail);

            SettingsController.Load();
        }

        private void OnSuccess()
        {
            Log.Debug("Done");

            Dispatcher.RemoveListener(SettingsEvent.LoadSuccess, OnSuccess);
            Dispatcher.RemoveListener(SettingsEvent.LoadFail, OnFail);

            Release();
        }

        private void OnFail(Exception exception)
        {
            Log.Error(exception);

            Dispatcher.RemoveListener(SettingsEvent.LoadSuccess, OnSuccess);
            Dispatcher.RemoveListener(SettingsEvent.LoadFail, OnFail);

            Fail(exception);
        }
    }
}