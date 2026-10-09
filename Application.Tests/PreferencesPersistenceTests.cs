using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Shared;
using WiinUSoft;
using WiinUSoft.Services;
using Xunit;

namespace Application.Tests;

public sealed class PreferencesPersistenceTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "WiitarThing-preferences-" + Guid.NewGuid().ToString("N"));
    private readonly string _path;

    public PreferencesPersistenceTests()
    {
        Directory.CreateDirectory(_directory);
        _path = Path.Combine(_directory, "prefs.config");
    }

    [Fact]
    public void InitialSaveProducesReadablePreferencesWithoutBackup()
    {
        var result = PreferencesFileStore.Save(_path, Preferences("first"));

        Assert.True(result.IsOk);
        Assert.Equal("first", Read(_path).devicePrefs.Single().name);
        Assert.False(File.Exists(_path + ".bak"));
        AssertNoTemporaryFiles();
    }

    [Fact]
    public void EachSuccessfulReplacementRetainsImmediatelyPreviousSave()
    {
        Assert.True(PreferencesFileStore.Save(_path, Preferences("first")).IsOk);
        Assert.True(PreferencesFileStore.Save(_path, Preferences("second")).IsOk);
        Assert.Equal("first", Read(_path + ".bak").devicePrefs.Single().name);

        Assert.True(PreferencesFileStore.Save(_path, Preferences("third")).IsOk);

        Assert.Equal("third", Read(_path).devicePrefs.Single().name);
        Assert.Equal("second", Read(_path + ".bak").devicePrefs.Single().name);
        AssertNoTemporaryFiles();
    }

    [Fact]
    public void SerializationFailureLeavesExistingPreferencesAndBackupUntouched()
    {
        Assert.True(PreferencesFileStore.Save(_path, Preferences("first")).IsOk);
        Assert.True(PreferencesFileStore.Save(_path, Preferences("second")).IsOk);
        var original = File.ReadAllBytes(_path);
        var backup = File.ReadAllBytes(_path + ".bak");

        var result = PreferencesFileStore.Save(_path, Preferences("invalid\u0001xml"));

        Assert.True(result.IsError);
        Assert.Equal(PreferencesErrorKind.SerializationFailed, result.Error.Kind);
        Assert.Equal(_path, result.Error.Path);
        Assert.Equal(original, File.ReadAllBytes(_path));
        Assert.Equal(backup, File.ReadAllBytes(_path + ".bak"));
        AssertNoTemporaryFiles();
    }

    [Fact]
    public void FailedReplacementPreservesExistingFileAndRemovesTemporaryFile()
    {
        Assert.True(PreferencesFileStore.Save(_path, Preferences("original")).IsOk);
        var original = File.ReadAllBytes(_path);

        using (var lockedDestination = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            var result = PreferencesFileStore.Save(_path, Preferences("replacement"));

            Assert.True(result.IsError);
            Assert.Equal(PreferencesErrorKind.SerializationFailed, result.Error.Kind);
            AssertNoTemporaryFiles();
        }

        Assert.Equal(original, File.ReadAllBytes(_path));
        Assert.False(File.Exists(_path + ".bak"));
    }

    [Fact]
    public void FailedFirstSerializationDoesNotPublishPartialPreferences()
    {
        var result = PreferencesFileStore.Save(_path, Preferences("invalid\u0001xml"));

        Assert.True(result.IsError);
        Assert.False(File.Exists(_path));
        Assert.False(File.Exists(_path + ".bak"));
        AssertNoTemporaryFiles();
    }

    [Fact]
    public async Task ConcurrentSavesPublishWholeDocumentsAndRetainWholeBackup()
    {
        var names = Enumerable.Range(0, 8).Select(index => "save-" + index).ToArray();
        var results = await Task.WhenAll(names.Select(name =>
            Task.Run(() => PreferencesFileStore.Save(_path, Preferences(name)))));

        Assert.All(results, result => Assert.True(result.IsOk));
        var current = Read(_path).devicePrefs.Single().name;
        var previous = Read(_path + ".bak").devicePrefs.Single().name;
        Assert.Contains(current, names);
        Assert.Contains(previous, names);
        Assert.NotEqual(current, previous);
        AssertNoTemporaryFiles();
    }

    [Fact]
    public void MissingDirectoryReturnsStructuredFailure()
    {
        var path = Path.Combine(_directory, "missing", "prefs.config");

        var result = PreferencesFileStore.Save(path, Preferences("first"));

        Assert.True(result.IsError);
        Assert.Equal(PreferencesErrorKind.SerializationFailed, result.Error.Kind);
        Assert.Equal(path, result.Error.Path);
        Assert.False(File.Exists(path));
    }

    [Fact]
    public void MissingPathReturnsStructuredFailure()
    {
        var result = PreferencesFileStore.Save(string.Empty, Preferences("first"));

        Assert.True(result.IsError);
        Assert.Equal(PreferencesErrorKind.MissingPath, result.Error.Kind);
    }

    private static UserPrefs Preferences(string name) => new()
    {
        devicePrefs = { new Property("test-device") { name = name, vJoyDeviceId = 3 } },
        defaultProfile = new Profile()
    };

    private static UserPrefs Read(string path)
    {
        using var stream = File.OpenRead(path);
        return Assert.IsType<UserPrefs>(new XmlSerializer(typeof(UserPrefs)).Deserialize(stream));
    }

    private void AssertNoTemporaryFiles() => Assert.Empty(Directory.EnumerateFiles(_directory, "*.tmp"));

    public void Dispose() => Directory.Delete(_directory, recursive: true);
}
