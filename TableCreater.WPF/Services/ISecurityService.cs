namespace TableCreater.WPF.Services;

/// <summary>
/// Security service interface for PIN authentication and database envelope encryption.
/// Prevents plain text secrets in source code and ensures local database is encrypted.
/// </summary>
public interface ISecurityService
{
    /// <summary>
    /// Gets whether security envelope is initialized.
    /// </summary>
    bool IsConfigured { get; }

    /// <summary>
    /// Active in-memory Database Encryption Key (DEK). Never persisted in plain text to disk.
    /// </summary>
    string? ActiveDek { get; }

    /// <summary>
    /// Verifies the entered PIN against the cryptographic verification hash.
    /// On success, decrypts the DEK and keeps it in memory for the active session.
    /// </summary>
    bool VerifyAndUnlock(string enteredPin);

    /// <summary>
    /// Changes the user's PIN by verifying old PIN and re-encrypting the DEK with the new PIN.
    /// Preserves all existing and future database records seamlessly.
    /// </summary>
    bool ChangePin(string oldPin, string newPin);

    /// <summary>
    /// Ensures the SQLite database on disk is encrypted with SQLCipher using the active DEK.
    /// If the file is unencrypted, migrates it to an encrypted container.
    /// </summary>
    void EnsureDatabaseEncrypted(string dbFilePath);

    /// <summary>
    /// Returns the connection string for SQLite, with Password if encrypted.
    /// </summary>
    string GetConnectionString(string dbFilePath);
}
