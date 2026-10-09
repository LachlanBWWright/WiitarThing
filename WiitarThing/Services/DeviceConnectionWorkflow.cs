using System;
using System.IO;
using System.Threading.Tasks;
using System.Threading;
using System.Xml.Serialization;
using NintrollerLib;
using Shared;
using Shared.Windows;
using WiinUSoft.Holders;
using WiinUSoft.VirtualOutput;

namespace WiinUSoft.Services;

/// <summary>Creates and connects controller mappings without depending on a view.</summary>
public sealed class DeviceConnectionWorkflow
{
    private readonly IDeviceConfigurationService _configuration;

    public DeviceConnectionWorkflow(IDeviceConfigurationService configuration)
        => _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));

    public Result<Holder, VirtualControllerError> ConnectXInput(
        Nintroller controller,
        string profilePath,
        int player,
        Action<string>? checkInfraredMapping = null)
    {
        var holder = new XInputHolder(controller.Type);
        LoadProfile(profilePath, holder, checkInfraredMapping, controller);
        var result = holder.TryConnectXInput(player);
        if (result.IsError)
        {
            holder.Close();
            return Result<Holder, VirtualControllerError>.Err(result.Error);
        }
        return Result<Holder, VirtualControllerError>.Ok(holder);
    }

    public bool TryOpenStream(Nintroller controller)
    {
        if (controller.DataStream is not WinBtStream stream)
            return false;

        var result = stream.TryOpenConnection();
        if (result.IsError)
        {
            WiitarDebug.Log(result.Error.ToDisplayString(), WiitarDebug.LogLevel.Error);
            return false;
        }

        return controller.DataStream.CanRead;
    }

    public void BeginXInput(Nintroller controller)
    {
        controller.BeginReading();
        controller.GetStatus();
    }

    public void Disconnect(Nintroller controller, Holder? holder)
    {
        controller.StopReading();
        holder?.Close();
    }

    public void ApplyCalibration(Nintroller controller, Property properties, string calibration)
    {
        switch (properties.calPref)
        {
            case Property.CalibrationPreference.Default: controller.SetCalibration(Calibrations.CalibrationPreset.Default); break;
            case Property.CalibrationPreference.More: controller.SetCalibration(Calibrations.CalibrationPreset.Modest); break;
            case Property.CalibrationPreference.Extra: controller.SetCalibration(Calibrations.CalibrationPreset.Extra); break;
            case Property.CalibrationPreference.Minimal: controller.SetCalibration(Calibrations.CalibrationPreset.Minimum); break;
            case Property.CalibrationPreference.Raw: controller.SetCalibration(Calibrations.CalibrationPreset.None); break;
            case Property.CalibrationPreference.Custom:
                var storage = new CalibrationStorage(calibration);
                controller.SetCalibration(storage.ProCalibration);
                controller.SetCalibration(storage.NunchukCalibration);
                controller.SetCalibration(storage.ClassicCalibration);
                controller.SetCalibration(storage.ClassicProCalibration);
                controller.SetCalibration(storage.WiimoteCalibration);
                break;
        }
    }

    public async Task IdentifyAsync(Nintroller controller, bool wasConnected, int targetPlayer,
        Action<bool>? setIdentifying = null, CancellationToken cancellationToken = default,
        Action<Action>? executeControllerAction = null)
    {
        void Execute(Action action)
        {
            if (executeControllerAction is null)
                action();
            else
                executeControllerAction(action);
        }
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            Execute(() =>
            {
                if (!wasConnected)
                    controller.BeginReading();
                cancellationToken.ThrowIfCancellationRequested();
                setIdentifying?.Invoke(true);
                controller.RumbleEnabled = true;
                cancellationToken.ThrowIfCancellationRequested();
                controller.SetPlayerLED(1);
            });
            const int duration = 400;
            for (int i = 1; i <= 6; i++)
            {
                await Task.Delay((duration / 7) * i, cancellationToken).ConfigureAwait(false);
                var player = i switch { 1 => 2, 2 => 3, 3 => 4, 4 => 3, 5 => 2, _ => 1 };
                Execute(() => controller.SetPlayerLED(player));
            }
        }
        finally
        {
            Execute(() =>
            {
                controller.RumbleEnabled = false;
                setIdentifying?.Invoke(false);
                if (!wasConnected)
                    controller.StopReading();
                if (targetPlayer != 0)
                    controller.SetPlayerLED(targetPlayer);
            });
        }
    }

    private void LoadProfile(string profilePath, Holder holder, Action<string>? checkInfraredMapping, Nintroller controller)
    {
        Profile? profile = null;
        var profileResult = TryLoadProfile(profilePath);
        if (profileResult.IsOk)
            profile = profileResult.Value;
        else if (!string.IsNullOrWhiteSpace(profilePath))
            WiitarDebug.Log(profileResult.Error.ToDisplayString(), WiitarDebug.LogLevel.Warning);

        profile ??= _configuration.DefaultProfile;
        if (profile is null)
            return;

        for (int i = 0; i < Math.Min(profile.controllerMapKeys.Count, profile.controllerMapValues.Count); i++)
        {
            holder.SetMapping(profile.controllerMapKeys[i], profile.controllerMapValues[i]);
            checkInfraredMapping?.Invoke(profile.controllerMapKeys[i]);
            if (checkInfraredMapping is null)
                EnsureInfraredMode(controller, profile.controllerMapKeys[i]);
        }
    }

    private static void EnsureInfraredMode(Nintroller controller, string assignment)
    {
        if (!assignment.StartsWith("wIR", StringComparison.Ordinal) || controller.IRMode != IRCamMode.Off)
            return;
        if (controller.Type is ControllerType.Wiimote or ControllerType.Nunchuk or ControllerType.NunchukB)
            controller.IRMode = IRCamMode.Basic;
    }

    private static Result<Profile, PreferencesError> TryLoadProfile(string profilePath)
    {
        if (string.IsNullOrWhiteSpace(profilePath))
            return Result<Profile, PreferencesError>.Err(PreferencesError.ValidationFailed("Profile path is empty."));
        if (!File.Exists(profilePath))
            return Result<Profile, PreferencesError>.Err(PreferencesError.FileNotFound(profilePath));

        try
        {
            var serializer = new XmlSerializer(typeof(Profile));
            using var stream = File.OpenRead(profilePath);
            using var reader = new StreamReader(stream);
            using var xmlReader = System.Xml.XmlReader.Create(reader, new System.Xml.XmlReaderSettings
            {
                DtdProcessing = System.Xml.DtdProcessing.Prohibit,
                XmlResolver = null
            });
            var profile = serializer.Deserialize(xmlReader) as Profile;
            return profile is null
                ? Result<Profile, PreferencesError>.Err(PreferencesError.InvalidXml(profilePath, new InvalidOperationException("Profile XML did not deserialize to a valid profile.")))
                : Result<Profile, PreferencesError>.Ok(profile);
        }
        catch (UnauthorizedAccessException ex) { return Result<Profile, PreferencesError>.Err(PreferencesError.AccessDenied(profilePath, ex)); }
        catch (System.Xml.XmlException ex) { return Result<Profile, PreferencesError>.Err(PreferencesError.InvalidXml(profilePath, ex)); }
        catch (InvalidOperationException ex) when (ex.InnerException is System.Xml.XmlException) { return Result<Profile, PreferencesError>.Err(PreferencesError.InvalidXml(profilePath, ex)); }
        catch (InvalidOperationException ex) { return Result<Profile, PreferencesError>.Err(PreferencesError.Unknown(profilePath, ex)); }
        catch (System.Security.SecurityException ex) { return Result<Profile, PreferencesError>.Err(PreferencesError.AccessDenied(profilePath, ex)); }
        catch (IOException ex) { return Result<Profile, PreferencesError>.Err(PreferencesError.Unknown(profilePath, ex)); }
    }
}
