using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;
using Shared;

namespace WiinUSoft
{
    public class UserPrefs
    {
        private static UserPrefs _instance;

        public static UserPrefs Instance
        {
            get
            {
                if (_instance == null)
                {
                    if (File.Exists(AppDomain.CurrentDomain.BaseDirectory + @"\prefs.config"))
                    {
                        DataPath = AppDomain.CurrentDomain.BaseDirectory + @"\prefs.config";
                        var result = LoadPrefs();
                        if (result.IsError)
                            System.Diagnostics.Debug.WriteLine(result.Error);
                    }
                    else if (File.Exists(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + @"\WiinUSoft_prefs.config"))
                    {
                        DataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + @"\WiinUSoft_prefs.config";
                        var result = LoadPrefs();
                        if (result.IsError)
                            System.Diagnostics.Debug.WriteLine(result.Error);
                    }
                    else
                    {
                        _instance = new UserPrefs();
                        _instance.defaultProfile = new Profile();
                        DataPath = AppDomain.CurrentDomain.BaseDirectory + @"\prefs.config";

                        var saveResult = SavePrefs();
                        if (saveResult.IsError)
                        {
                            System.Diagnostics.Debug.WriteLine(saveResult.Error);
                            DataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + @"\WiinUSoft_prefs.config";
                            var fallback = SavePrefs();
                            if (fallback.IsError)
                                System.Diagnostics.Debug.WriteLine(fallback.Error);
                        }
                    }
                }

                return _instance;
            }
        }

        public static string DataPath { get; protected set; }

        public static bool AutoStart
        {
            get { return Instance.autoStartup; }
            set
            {
                try
                {
                    RegistryKey key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);

                    if (value)
                    {
                        if (key.GetValue("WiinUSoft") == null)
                        {
                            key.SetValue("WiinUSoft", (new Uri(System.Reflection.Assembly.GetEntryAssembly().CodeBase)).LocalPath);
                        }
                    }
                    else
                    {
                        key.DeleteValue("WiinUSoft", false);
                    }
                }
                catch
                {
                    string dir = Environment.GetFolderPath(Environment.SpecialFolder.Startup);

                    if (value)
                    {
                        if (!File.Exists(Path.Combine(dir, "WiinUSoft.lnk")))
                        {
                            MainWindow.Instance.CreateShortcut(dir);
                        }
                    }
                    else
                    {
                        if (File.Exists(Path.Combine(dir, "WiinUSoft.lnk")))
                        {
                            File.Delete(Path.Combine(dir, "WiinUSoft.lnk"));
                        }
                    }
                }

                Instance.autoStartup = value;
            }
        }

        // devicePrefs is always initialized; never null.
        public List<Property> devicePrefs = new List<Property>();
        public Profile defaultProfile;
        // defaultProperty is explicitly nullable: absent when no "all" entry exists.
        public Property defaultProperty;
        public bool autoStartup;
        public bool startMinimized;
        public bool greedyMode;
        public bool toshibaMode;
        public bool autoRefresh = true;

        // Parameterless constructor required by XmlSerializer.
        public UserPrefs()
        {
            devicePrefs = new List<Property>();
        }

        /// <summary>
        /// Loads preferences from <see cref="DataPath"/>.
        /// Returns <see cref="Result{T,TError}.Ok"/> on success,
        /// or a structured <see cref="PreferencesError"/> on recoverable failure.
        /// </summary>
        public static Result<UserPrefs, PreferencesError> LoadPrefs()
        {
            if (string.IsNullOrEmpty(DataPath))
                return Result<UserPrefs, PreferencesError>.Err(
                    new PreferencesError(PreferencesErrorKind.MissingPath, "DataPath has not been set."));

            if (!File.Exists(DataPath))
                return Result<UserPrefs, PreferencesError>.Err(
                    PreferencesError.FileNotFound(DataPath));

            var serializer = new XmlSerializer(typeof(UserPrefs));
            try
            {
                using (var stream = File.OpenRead(DataPath))
                using (var reader = new StreamReader(stream))
                {
                    _instance = (UserPrefs)serializer.Deserialize(reader);
                }

                // Ensure collections are never null after deserialization.
                if (_instance.devicePrefs == null)
                    _instance.devicePrefs = new List<Property>();

                // Locate the optional "all" default property entry.
                _instance.defaultProperty = _instance.devicePrefs.Find(
                    p => p.hid != null && p.hid.Equals("all", StringComparison.OrdinalIgnoreCase));

                return Result<UserPrefs, PreferencesError>.Ok(_instance);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Result<UserPrefs, PreferencesError>.Err(
                    PreferencesError.AccessDenied(DataPath, ex));
            }
            catch (System.Xml.XmlException ex)
            {
                return Result<UserPrefs, PreferencesError>.Err(
                    PreferencesError.InvalidXml(DataPath, ex));
            }
            catch (InvalidOperationException ex) when (ex.InnerException is System.Xml.XmlException)
            {
                return Result<UserPrefs, PreferencesError>.Err(
                    PreferencesError.InvalidXml(DataPath, ex));
            }
            catch (Exception ex)
            {
                return Result<UserPrefs, PreferencesError>.Err(
                    PreferencesError.Unknown(DataPath, ex));
            }
        }

        /// <summary>
        /// Saves the current preferences to <see cref="DataPath"/>.
        /// Returns <see cref="Result{T,TError}.Ok"/> on success,
        /// or a structured <see cref="PreferencesError"/> on recoverable failure.
        /// </summary>
        public static Result<Unit, PreferencesError> SavePrefs()
        {
            if (string.IsNullOrEmpty(DataPath))
                return Result<Unit, PreferencesError>.Err(
                    new PreferencesError(PreferencesErrorKind.MissingPath, "DataPath has not been set."));

            var serializer = new XmlSerializer(typeof(UserPrefs));
            try
            {
                var mode = File.Exists(DataPath) ? FileMode.Create : FileMode.CreateNew;
                using (var stream = File.Open(DataPath, mode, FileAccess.Write))
                using (var writer = new StreamWriter(stream))
                {
                    serializer.Serialize(writer, _instance);
                }

                return Result<Unit, PreferencesError>.Ok(Unit.Value);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Result<Unit, PreferencesError>.Err(
                    PreferencesError.AccessDenied(DataPath, ex));
            }
            catch (Exception ex)
            {
                return Result<Unit, PreferencesError>.Err(
                    PreferencesError.SerializationFailed(DataPath, ex));
            }
        }

        /// <summary>
        /// Returns the saved preference for the given HID path,
        /// or the global "all" default if one exists, or <c>null</c> if absent.
        /// </summary>
        public Property GetDevicePref(string hid)
        {
            foreach (var pref in devicePrefs)
            {
                if (pref.hid == hid)
                    return pref;
            }

            return defaultProperty;
        }

        public void AddDevicePref(Property property)
        {
            foreach (var pref in devicePrefs)
            {
                if (pref.hid == property.hid)
                {
                    pref.name            = property.name;
                    pref.autoConnect     = property.autoConnect;
                    pref.profile         = property.profile;
                    pref.connType        = property.connType;
                    pref.autoNum         = property.autoNum;
                    pref.rumbleIntensity = property.rumbleIntensity;
                    pref.useRumble       = property.useRumble;
                    pref.calPref         = property.calPref;

                    return;
                }
            }

            devicePrefs.Add(property);
        }

        public void UpdateDeviceIcon(string path, string icon)
        {
            var prop = devicePrefs.FindIndex((p) => p.hid == path);

            if (prop >= 0)
            {
                devicePrefs[prop].lastIcon = icon;
                var result = SavePrefs();
                if (result.IsError)
                    System.Diagnostics.Debug.WriteLine(result.Error);
            }
        }

        public string GetDeviceIcon(string path)
        {
            var prop = devicePrefs.FindIndex((p) => p.hid == path);

            if (prop >= 0)
            {
                return devicePrefs[prop].lastIcon;
            }

            return "";
        }
    }

    public class Property
    {
        public enum ProfHolderType
        {
            XInput = 0,
            DInput = 1
        }

        public enum CalibrationPreference
        {
            Raw     = -2,
            Minimal = -1,
            Default = 0,
            Defalut = 0,
            More    = 1,
            Extra   = 2,
            Custom  = 3
        }

        public enum PointerOffScreenMode
        {
            Center = 0,
            SnapX  = 1,
            SnapY  = 2,
            SnapXY = 3
        }

        public string hid = "";
        public string name = "";
        public string lastIcon = "";
        public bool autoConnect = false;
        public bool useRumble = true;
        public int autoNum = 0;
        public int rumbleIntensity = 2;
        public ProfHolderType connType;
        public string profile = "";
        public CalibrationPreference calPref;
        public string calString = ""; // not the best solution for saving the custom config but makes it easy
        public PointerOffScreenMode pointerMode = PointerOffScreenMode.Center;

        public Property()
        {
            hid = "";
            connType = ProfHolderType.XInput;
            calPref = CalibrationPreference.Default;
            pointerMode = PointerOffScreenMode.Center;
        }

        public Property(string ID)
        {
            hid = ID;
            connType = ProfHolderType.XInput;
            calPref = CalibrationPreference.Default;
            pointerMode = PointerOffScreenMode.Center;
        }

        public Property(Property copy)
        {
            hid = copy.hid;
            name = copy.name;
            autoConnect = copy.autoConnect;
            autoNum = copy.autoNum;
            useRumble = copy.useRumble;
            rumbleIntensity = copy.rumbleIntensity;
            connType = copy.connType;
            profile = copy.profile;
            calPref = copy.calPref;
            calString = copy.calString;
            pointerMode = copy.pointerMode;
        }
    }

    public class Profile
    {
        public enum HolderType
        {
            XInput = 0,
            DInput = 1
        }

        public NintrollerLib.ControllerType profileType;
        public HolderType connType;
        public List<string> controllerMapKeys;
        public List<string> controllerMapValues;

        public Profile()
        {
            profileType = NintrollerLib.ControllerType.Wiimote;
            controllerMapKeys = new List<string>();
            controllerMapValues = new List<string>();
            connType = HolderType.XInput;
        }

        public Profile(NintrollerLib.ControllerType type)
        {
            profileType = type;
            controllerMapKeys = new List<string>();
            controllerMapValues = new List<string>();
            connType = HolderType.XInput;
        }
    }

}
