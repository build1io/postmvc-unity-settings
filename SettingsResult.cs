using System;

namespace Build1.PostMVC.Unity.Settings
{
    public sealed class SettingsResult
    {
        public readonly SettingType       settingsType;
        public readonly SettingsErrorCode errorCode;
        public readonly Exception         exception;
        public readonly bool              isSuccess;
        public readonly bool              isError;

        internal SettingsResult(SettingType settingsType) : this(SettingsErrorCode.None)
        {
            this.settingsType = settingsType;
        }

        internal SettingsResult(SettingType settingsType, SettingsErrorCode errorCode) : this(errorCode)
        {
            this.settingsType = settingsType;
        }

        internal SettingsResult(SettingType settingsType, Exception exception) : this(SettingsErrorCode.Exception)
        {
            this.settingsType = settingsType;
            this.exception = exception;
        }

        private SettingsResult(SettingsErrorCode errorCode)
        {
            this.errorCode = errorCode;
            this.isSuccess = errorCode == SettingsErrorCode.None; 
            this.isError = errorCode != SettingsErrorCode.None;
        }

        public Exception ToException()
        {
            if (errorCode == SettingsErrorCode.None)
                return null;
            
            return exception ?? new Exception(errorCode.ToString());
        }
    }
}