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
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TableCreater");

        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        _securityFilePath = Path.Combine(dir, "security.json");
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
        if (!File.Exists(dbFilePath))
            return; // EF Core will create it encrypted on first access

        if (string.IsNullOrEmpty(_activeDek))
            return;

        try
        {
            // Check if file is unencrypted (starts with ASCII "SQLite format 3")
            using (var fs = new FileStream(dbFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                byte[] header = new byte[16];
                int bytesRead = fs.Read(header, 0, 16);
                if (bytesRead >= 15)
                {
                    string headerStr = Encoding.ASCII.GetString(header, 0, 15);
                    if (!headerStr.StartsWith("SQLite format 3"))
                    {
                        // Already encrypted!
                        return;
                    }
                }
            }

            // It's unencrypted! Migrate to SQLCipher encrypted file.
            string tempEncryptedPath = Path.Combine(
                Path.GetDirectoryName(dbFilePath) ?? ".",
                $"enc_temp_{Guid.NewGuid():N}.db");

            if (File.Exists(tempEncryptedPath))
                File.Delete(tempEncryptedPath);

            // Connect to unencrypted database and export to encrypted database
            using (var conn = new SqliteConnection($"Data Source={dbFilePath};"))
            {
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = $@"
                    ATTACH DATABASE '{tempEncryptedPath}' AS encrypted KEY '{_activeDek}';
                    SELECT sqlcipher_export('encrypted');
                    DETACH DATABASE encrypted;
                ";
                cmd.ExecuteNonQuery();
            }

            // Create a safety backup of the original unencrypted DB before replacing
            string bakPath = dbFilePath + ".unencrypted.bak";
            File.Copy(dbFilePath, bakPath, overwrite: true);

            // Replace original with encrypted version
            File.Copy(tempEncryptedPath, dbFilePath, overwrite: true);
            File.Delete(tempEncryptedPath);
        }
        catch (Exception ex)
        {
            // If migration fails (e.g., in testing environment), log and keep going safely
            System.Diagnostics.Debug.WriteLine($"DB Encryption check note: {ex.Message}");
        }
    }

    public string GetConnectionString(string dbFilePath)
    {
        if (!string.IsNullOrEmpty(_activeDek))
        {
            return $"Data Source={dbFilePath};Password={_activeDek};";
        }

        return $"Data Source={dbFilePath};";
    }
}
