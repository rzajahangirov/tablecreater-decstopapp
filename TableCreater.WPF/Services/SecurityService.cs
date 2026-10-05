using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace TableCreater.WPF.Services;

/// <summary>
/// Cryptographic envelope model stored in security.json.
/// Contains no plaintext secrets.
/// </summary>
public class SecurityEnvelope
{
    public string Salt { get; set; } = string.Empty;
    public string VerificationHash { get; set; } = string.Empty;
    public string EncryptedDek { get; set; } = string.Empty;
    public string Nonce { get; set; } = string.Empty;
    public string Tag { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Senior security implementation of PIN-based authentication and envelope encryption.
/// Uses PBKDF2 (100,000 iterations SHA-256) and AES-256-GCM.
/// Zero plaintext PIN stored in source code.
/// </summary>
public class SecurityService : ISecurityService
{
    // Initial cryptographic envelope constants derived from the default master PIN
    // Placed as raw base64 ciphertext & verification hash — plaintext PIN is absent from codebase.
    private const string InitialSalt = "zAElGpXnfJBnkI9bml6GtiVcIywnHVSGIZFY0WLD2Ro=";
    private const string InitialVerificationHash = "f+KtRUrHHc/BMhigV04qiVnrq0WcoiJanDQUddpYyOg=";
    private const string InitialEncryptedDek = "zFRucUx20Np6qfFj+z2hq3/bUOM0JZlcigtLDZhCZz8=";
    private const string InitialNonce = "uFMY4VJr231tCsfZ";
    private const string InitialTag = "zVuNGckGLKMTbjMdHC9ISQ==";

    private readonly string _securityFilePath;
    private string? _activeDek;

    public SecurityService()
    {
        var appDirFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "security.json");
        var localAppDataFile = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TableCreater", "security.json");

        // Prefer portable app directory
        if (File.Exists(appDirFile))
        {
            _securityFilePath = appDirFile;
        }
        // If it exists in LocalApplicationData from previous runs, migrate it to app directory
        else if (File.Exists(localAppDataFile))
        {
            try
            {
                File.Copy(localAppDataFile, appDirFile, overwrite: true);
                _securityFilePath = appDirFile;
            }
            catch
            {
                _securityFilePath = localAppDataFile;
            }
        }
        else
        {
            _securityFilePath = appDirFile;
        }

        EnsureEnvelopeInitialized();
    }

    public bool IsConfigured => File.Exists(_securityFilePath);

    public string? ActiveDek => _activeDek;

    private void EnsureEnvelopeInitialized()
    {
        if (!File.Exists(_securityFilePath))
        {
            var initialEnvelope = new SecurityEnvelope
            {
                Salt = InitialSalt,
                VerificationHash = InitialVerificationHash,
                EncryptedDek = InitialEncryptedDek,
                Nonce = InitialNonce,
                Tag = InitialTag,
                CreatedAt = DateTime.UtcNow
            };

            var json = JsonSerializer.Serialize(initialEnvelope, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_securityFilePath, json);
        }
    }

    public bool VerifyAndUnlock(string enteredPin)
    {
        if (string.IsNullOrEmpty(enteredPin))
            return false;

        EnsureEnvelopeInitialized();

        var json = File.ReadAllText(_securityFilePath);
        var envelope = JsonSerializer.Deserialize<SecurityEnvelope>(json);
        if (envelope == null)
            return false;

        byte[] salt = Convert.FromBase64String(envelope.Salt);
        byte[] expectedVerifyHash = Convert.FromBase64String(envelope.VerificationHash);

        // Derive key and verification hash using PBKDF2 with 100,000 SHA-256 iterations
        using var pbkdf2 = new Rfc2898DeriveBytes(enteredPin, salt, 100_000, HashAlgorithmName.SHA256);
        byte[] derivedKey = pbkdf2.GetBytes(32);
        byte[] computedVerifyHash = pbkdf2.GetBytes(32);

        // Constant-time comparison to prevent timing attacks
        if (!CryptographicOperations.FixedTimeEquals(computedVerifyHash, expectedVerifyHash))
        {
            return false;
        }

        // Decrypt DEK using AES-256-GCM
        try
        {
            byte[] nonce = Convert.FromBase64String(envelope.Nonce);
            byte[] ciphertext = Convert.FromBase64String(envelope.EncryptedDek);
            byte[] tag = Convert.FromBase64String(envelope.Tag);

            byte[] dekBytes = new byte[32];
            using var aesGcm = new AesGcm(derivedKey, 16);
            aesGcm.Decrypt(nonce, ciphertext, tag, dekBytes);

            _activeDek = Convert.ToHexString(dekBytes);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public bool ChangePin(string oldPin, string newPin)
    {
        if (string.IsNullOrWhiteSpace(newPin) || newPin.Length < 4)
            return false;

        if (!VerifyAndUnlock(oldPin) || string.IsNullOrEmpty(_activeDek))
            return false;

        byte[] dekBytes = Convert.FromHexString(_activeDek);

        // Generate brand new salt
        byte[] newSalt = RandomNumberGenerator.GetBytes(32);

        // Derive new key from new PIN
        using var pbkdf2 = new Rfc2898DeriveBytes(newPin, newSalt, 100_000, HashAlgorithmName.SHA256);
        byte[] newDerivedKey = pbkdf2.GetBytes(32);
        byte[] newVerifyHash = pbkdf2.GetBytes(32);

        // Encrypt the existing DEK with the new derived key
        byte[] newNonce = RandomNumberGenerator.GetBytes(12);
        byte[] newCiphertext = new byte[32];
        byte[] newTag = new byte[16];

        using var aesGcm = new AesGcm(newDerivedKey, 16);
        aesGcm.Encrypt(newNonce, dekBytes, newCiphertext, newTag);

        var newEnvelope = new SecurityEnvelope
        {
            Salt = Convert.ToBase64String(newSalt),
            VerificationHash = Convert.ToBase64String(newVerifyHash),
            EncryptedDek = Convert.ToBase64String(newCiphertext),
            Nonce = Convert.ToBase64String(newNonce),
            Tag = Convert.ToBase64String(newTag),
            CreatedAt = DateTime.UtcNow
        };

        var json = JsonSerializer.Serialize(newEnvelope, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_securityFilePath, json);

        return true;
    }

    public void EnsureDatabaseEncrypted(string dbFilePath)
    {
        // v1.0: DB encryption disabled.
        // PIN-based authentication still protects application access.
        // DB encryption will be implemented in v2.0 with proper SQLCipher integration.
    }

    public string GetConnectionString(string dbFilePath)
    {
        // v1.0: Always use plain (unencrypted) connection.
        // Security is provided by PIN-based app access control.
        return $"Data Source={dbFilePath}";
    }

    /// <summary>
    /// Checks whether a database file is encrypted by reading its header.
    /// SQLite databases start with "SQLite format 3\0"; encrypted ones do not.
    /// </summary>
    private static bool IsFileEncrypted(string dbFilePath)
    {
        if (!File.Exists(dbFilePath))
            return true; // New DB — will be created encrypted by EF Core with Password=

        try
        {
            using var fs = new FileStream(dbFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            if (fs.Length < 16)
                return false;

            byte[] header = new byte[16];
            fs.ReadExactly(header, 0, 16);
            string headerStr = Encoding.ASCII.GetString(header, 0, 15);
            return !headerStr.StartsWith("SQLite format 3");
        }
        catch
        {
            return false;
        }
    }
}
