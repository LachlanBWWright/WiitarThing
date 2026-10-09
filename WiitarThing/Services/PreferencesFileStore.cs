using System;
using System.IO;
using System.Security;
using System.Xml.Serialization;
using Shared;

namespace WiinUSoft.Services;

/// <summary>Publishes a complete preferences file while retaining the previous version.</summary>
internal static class PreferencesFileStore
{
    private static readonly object SaveGate = new();

    internal static Result<Unit, PreferencesError> Save(string path, UserPrefs preferences)
    {
        if (string.IsNullOrEmpty(path))
            return Result<Unit, PreferencesError>.Err(PreferencesError.MissingPath());

        // Serialize publications so two initial saves cannot race, and backups follow save order.
        lock (SaveGate)
        {
            string? temporaryPath = null;
            try
            {
                var destination = Path.GetFullPath(path);
                temporaryPath = Path.Combine(Path.GetDirectoryName(destination)!,
                    $".{Path.GetFileName(destination)}.{Guid.NewGuid():N}.tmp");

                using (var stream = new FileStream(temporaryPath, FileMode.CreateNew,
                    FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
                using (var writer = new StreamWriter(stream))
                {
                    new XmlSerializer(typeof(UserPrefs)).Serialize(writer, preferences);
                    writer.Flush();
                    stream.Flush(flushToDisk: true);
                }

                if (File.Exists(destination))
                    File.Replace(temporaryPath, destination, destination + ".bak");
                else
                    File.Move(temporaryPath, destination);

                temporaryPath = null;
                return Result<Unit, PreferencesError>.Ok(Unit.Value);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Result<Unit, PreferencesError>.Err(PreferencesError.AccessDenied(path, ex));
            }
            catch (SecurityException ex)
            {
                return Result<Unit, PreferencesError>.Err(PreferencesError.AccessDenied(path, ex));
            }
            catch (InvalidOperationException ex)
            {
                return Result<Unit, PreferencesError>.Err(PreferencesError.SerializationFailed(path, ex));
            }
            catch (IOException ex)
            {
                return Result<Unit, PreferencesError>.Err(PreferencesError.SerializationFailed(path, ex));
            }
            catch (ArgumentException ex)
            {
                return Result<Unit, PreferencesError>.Err(PreferencesError.ValidationFailed(ex.Message, path));
            }
            finally
            {
                if (temporaryPath != null)
                    DeleteTemporaryFile(temporaryPath);
            }
        }
    }

    private static void DeleteTemporaryFile(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException ex)
        {
            System.Diagnostics.Debug.WriteLine($"Could not remove temporary preferences file {path}: {ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            System.Diagnostics.Debug.WriteLine($"Could not remove temporary preferences file {path}: {ex.Message}");
        }
        catch (SecurityException ex)
        {
            System.Diagnostics.Debug.WriteLine($"Could not remove temporary preferences file {path}: {ex.Message}");
        }
    }
}
