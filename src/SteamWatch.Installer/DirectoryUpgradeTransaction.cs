using System.Text.Json;

namespace SteamWatch.Installer;

/// <summary>Durable directory switch shared by installation, recovery and uninstall.</summary>
internal sealed class DirectoryUpgradeTransaction : IDisposable
{
    private sealed record Journal(int Version, string Id, string InstallName, string StageName,
        bool HadPrevious, string RegistrationSnapshot, string Phase);

    private readonly string _parent;
    private readonly string _installName;
    private readonly string _journalPath;
    private readonly FileStream _lock;
    private Journal? _journal;
    private const string Marker = ".steamwatch-install-transaction";

    public DirectoryUpgradeTransaction(string installDirectory)
    {
        var install = Path.TrimEndingDirectorySeparator(Path.GetFullPath(installDirectory));
        _parent = Path.GetDirectoryName(install) ?? throw new ArgumentException("Installation must have a parent directory.");
        _installName = Path.GetFileName(install);
        if (string.IsNullOrEmpty(_installName)) throw new ArgumentException("Invalid installation directory.");
        EnsureNoLinks(_parent);
        Directory.CreateDirectory(_parent);
        _journalPath = Path.Combine(_parent, _installName + ".upgrade.json");
        var lockPath = _journalPath + ".lock";
        EnsureNoLinks(lockPath);
        EnsureNoLinks(_journalPath);
        _lock = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        try
        {
            if (File.Exists(_journalPath))
            {
                if (new FileInfo(_journalPath).Length > 1_048_576) throw new InvalidDataException("Upgrade journal is too large.");
                _journal = JsonSerializer.Deserialize<Journal>(File.ReadAllText(_journalPath))
                    ?? throw new InvalidDataException("Empty upgrade journal.");
                Validate(_journal);
            }
        }
        catch { _lock.Dispose(); throw; }
    }

    public string InstallDirectory => Path.Combine(_parent, _installName);
    public string StagingDirectory => Path.Combine(_parent, Current.StageName);
    public string BackupDirectory => Path.Combine(_parent, "SteamWatch.previous-" + Current.Id);
    public string Phase => _journal?.Phase ?? "None";
    private Journal Current => _journal ?? throw new InvalidOperationException("No prepared transaction.");

    public void Prepare(string stagingDirectory, string registrationSnapshot)
    {
        if (_journal is not null) throw new InvalidOperationException("Existing journal must be resolved and explicitly archived first.");
        var stage = Path.TrimEndingDirectorySeparator(Path.GetFullPath(stagingDirectory));
        if (!string.Equals(Path.GetDirectoryName(stage), _parent, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Staging must be a sibling of the installation.");
        var journal = new Journal(1, Guid.NewGuid().ToString("N"), _installName, Path.GetFileName(stage),
            Directory.Exists(InstallDirectory), registrationSnapshot, "Prepared");
        Validate(journal);
        if (!Directory.Exists(stage)) throw new DirectoryNotFoundException(stage);
        EnsureTreeHasNoLinks(stage);
        if (Directory.Exists(InstallDirectory)) EnsureTreeHasNoLinks(InstallDirectory);
        if (File.Exists(Path.Combine(stage, Marker))) throw new InvalidDataException("Staging already has a transaction marker.");
        WriteDurable(Path.Combine(stage, Marker), journal.Id);
        Save(journal); // Durable intent precedes the first directory rename.
    }

    public void SwitchDirectories(Action<string>? checkpoint = null)
    {
        if (Current.Phase != "Prepared") throw new InvalidOperationException("Only a prepared transaction can switch directories.");
        EnsureTreeHasNoLinks(StagingDirectory);
        if (!Owns(StagingDirectory)) throw new InvalidDataException("Staging identity changed.");
        if (Current.HadPrevious)
        {
            EnsureTreeHasNoLinks(InstallDirectory);
            Directory.Move(InstallDirectory, BackupDirectory);
            checkpoint?.Invoke("old-renamed");
        }
        Save(Current with { Phase = "OldMoved" });
        Directory.Move(StagingDirectory, InstallDirectory);
        checkpoint?.Invoke("new-renamed");
        Save(Current with { Phase = "Switched" });
    }

    public void Commit()
    {
        if (Current.Phase != "Switched" || !Owns(InstallDirectory))
            throw new InvalidOperationException("Only a switched, identified payload can commit.");
        Save(Current with { Phase = "Committed" });
        // Journal, previous installation and failed staging are retained; no recursive deletion.
    }

    public void Rollback(Action<string> restoreRegistration)
    {
        if (Current.Phase == "Committed") throw new InvalidOperationException("Committed transactions cannot be automatically downgraded.");
        if (Current.Phase == "RolledBack") return;
        EnsureNoLinks(StagingDirectory);
        EnsureNoLinks(InstallDirectory);
        EnsureNoLinks(BackupDirectory);
        if (Directory.Exists(InstallDirectory) && Owns(InstallDirectory))
        {
            if (Directory.Exists(StagingDirectory)) throw new InvalidDataException("Both staged and installed new payload exist; recovery refused.");
            EnsureTreeHasNoLinks(InstallDirectory);
            Directory.Move(InstallDirectory, StagingDirectory);
        }
        if (Directory.Exists(BackupDirectory))
        {
            if (!Current.HadPrevious || Directory.Exists(InstallDirectory))
                throw new InvalidDataException("Conflicting recovery directories; nothing overwritten.");
            EnsureTreeHasNoLinks(BackupDirectory);
            Directory.Move(BackupDirectory, InstallDirectory);
        }
        if (Current.HadPrevious != Directory.Exists(InstallDirectory) || !Owns(StagingDirectory))
            throw new InvalidDataException("Recovery directories do not match the journal.");
        restoreRegistration(Current.RegistrationSnapshot); // Must be idempotent; journal retained on failure.
        Save(Current with { Phase = "RolledBack" });
    }

    public string ArchiveResolved()
    {
        if (Current.Phase is not ("Committed" or "RolledBack"))
            throw new InvalidOperationException("Pending transactions must be recovered before archiving.");
        var archive = _journalPath + ".history-" + Current.Id;
        File.Move(_journalPath, archive);
        _journal = null;
        return archive;
    }

    private void Validate(Journal journal)
    {
        if (journal.Version != 1 || !Guid.TryParseExact(journal.Id, "N", out _) ||
            journal.InstallName != _installName || string.IsNullOrEmpty(journal.StageName) || journal.RegistrationSnapshot is null ||
            !journal.StageName.StartsWith("SteamWatch.staging-", StringComparison.Ordinal) ||
            !Guid.TryParseExact(journal.StageName["SteamWatch.staging-".Length..], "N", out _) ||
            string.Equals(journal.StageName, _installName, StringComparison.OrdinalIgnoreCase) ||
            string.Equals("SteamWatch.previous-" + journal.Id, _installName, StringComparison.OrdinalIgnoreCase) ||
            journal.Phase is not ("Prepared" or "OldMoved" or "Switched" or "Committed" or "RolledBack"))
            throw new InvalidDataException("Invalid upgrade journal; recovery refused.");
    }

    private bool Owns(string directory)
    {
        var marker = Path.Combine(directory, Marker);
        EnsureNoLinks(marker);
        return File.Exists(marker) && File.ReadAllText(marker) == Current.Id;
    }

    private void Save(Journal journal)
    {
        Validate(journal);
        var json = JsonSerializer.Serialize(journal);
        if (System.Text.Encoding.UTF8.GetByteCount(json) > 1_048_576) throw new InvalidDataException("Upgrade journal is too large.");
        var temporary = _journalPath + ".new-" + Guid.NewGuid().ToString("N");
        WriteDurable(temporary, json);
        File.Move(temporary, _journalPath, overwrite: true);
        _journal = journal;
    }

    private static void WriteDurable(string path, string value)
    {
        using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough);
        using var writer = new StreamWriter(file, leaveOpen: true);
        writer.Write(value);
        writer.Flush();
        file.Flush(flushToDisk: true);
    }

    private static void EnsureNoLinks(string path)
    {
        for (var current = Path.GetFullPath(path); current is not null; current = Path.GetDirectoryName(current))
        {
            try
            {
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidDataException("Reparse points are not permitted in upgrade paths.");
            }
            catch (FileNotFoundException) { }
            catch (DirectoryNotFoundException) { }
        }
    }

    private static void EnsureTreeHasNoLinks(string path)
    {
        EnsureNoLinks(path);
        foreach (var entry in Directory.EnumerateFileSystemEntries(path))
        {
            EnsureNoLinks(entry);
            if (Directory.Exists(entry)) EnsureTreeHasNoLinks(entry);
        }
    }

    public void Dispose() => _lock.Dispose();
}
