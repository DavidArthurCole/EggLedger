namespace EggLedger.Web.Platform;

public interface IStorageManagement {
    string GetDataRootDir();

    Task BackupAsync(string destPath, bool db, bool exports, bool logs);

    Task MoveAsync(string destPath);
}
