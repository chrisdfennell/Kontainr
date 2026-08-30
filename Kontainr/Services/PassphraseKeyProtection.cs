using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using Microsoft.AspNetCore.DataProtection.XmlEncryption;

namespace Kontainr.Services;

/// <summary>
/// The operator-supplied passphrase used to encrypt the Data Protection key ring at rest.
/// </summary>
/// <remarks>
/// Without this, the key ring sits in the same volume as the secrets it protects, so read
/// access to that one volume (a backup, a stray bind mount, another container) yields
/// plaintext for every stored SSH, registry and git credential. With it, the on-disk keys
/// are inert unless the attacker also has the passphrase, which lives in the environment.
/// </remarks>
public sealed class KeyPassphrase(string value)
{
    public string Value { get; } = value;
}

/// <summary>
/// Encrypts Data Protection key ring elements with AES-256-GCM under a key derived from
/// <see cref="KeyPassphrase"/> via PBKDF2-HMAC-SHA256.
/// </summary>
public sealed class PassphraseXmlEncryptor(KeyPassphrase passphrase) : IXmlEncryptor
{
    internal const int Iterations = 600_000;
    internal const int SaltBytes = 32;
    internal const int KeyBytes = 32;

    public EncryptedXmlInfo Encrypt(XElement plaintextElement)
    {
        ArgumentNullException.ThrowIfNull(plaintextElement);

        var plaintext = Encoding.UTF8.GetBytes(plaintextElement.ToString(SaveOptions.DisableFormatting));
        var salt = RandomNumberGenerator.GetBytes(SaltBytes);
        var nonce = RandomNumberGenerator.GetBytes(AesGcm.NonceByteSizes.MaxSize);
        var tag = new byte[AesGcm.TagByteSizes.MaxSize];
        var ciphertext = new byte[plaintext.Length];

        var key = DeriveKey(passphrase.Value, salt, Iterations);
        try
        {
            using var aes = new AesGcm(key, tag.Length);
            aes.Encrypt(nonce, plaintext, ciphertext, tag);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
            CryptographicOperations.ZeroMemory(plaintext);
        }

        var element = new XElement("kontainrEncryptedKey",
            new XComment(" Encrypted with KONTAINR_KEY_PASSPHRASE. Losing that passphrase makes every " +
                         "stored SSH, registry and git credential unrecoverable. "),
            new XElement("iterations", Iterations),
            new XElement("salt", Convert.ToBase64String(salt)),
            new XElement("nonce", Convert.ToBase64String(nonce)),
            new XElement("tag", Convert.ToBase64String(tag)),
            new XElement("value", Convert.ToBase64String(ciphertext)));

        return new EncryptedXmlInfo(element, typeof(PassphraseXmlDecryptor));
    }

    internal static byte[] DeriveKey(string passphrase, byte[] salt, int iterations) =>
        Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(passphrase), salt, iterations, HashAlgorithmName.SHA256, KeyBytes);
}

/// <summary>
/// Counterpart to <see cref="PassphraseXmlEncryptor"/>. Data Protection activates this by
/// type name, handing it the application's service provider.
/// </summary>
public sealed class PassphraseXmlDecryptor : IXmlDecryptor
{
    private readonly KeyPassphrase _passphrase;

    public PassphraseXmlDecryptor(IServiceProvider services)
    {
        _passphrase = services.GetService(typeof(KeyPassphrase)) as KeyPassphrase
            ?? throw new InvalidOperationException(
                "The Data Protection key ring is encrypted but KONTAINR_KEY_PASSPHRASE is not set. " +
                "Set it to the same value used when the keys were created — without it, stored SSH, " +
                "registry and git credentials cannot be decrypted.");
    }

    public XElement Decrypt(XElement encryptedElement)
    {
        ArgumentNullException.ThrowIfNull(encryptedElement);

        var iterations = (int?)encryptedElement.Element("iterations")
            ?? throw new CryptographicException("Encrypted key ring element is missing its iteration count.");
        var salt = ReadBase64(encryptedElement, "salt");
        var nonce = ReadBase64(encryptedElement, "nonce");
        var tag = ReadBase64(encryptedElement, "tag");
        var ciphertext = ReadBase64(encryptedElement, "value");

        var plaintext = new byte[ciphertext.Length];
        var key = PassphraseXmlEncryptor.DeriveKey(_passphrase.Value, salt, iterations);
        try
        {
            using var aes = new AesGcm(key, tag.Length);
            aes.Decrypt(nonce, ciphertext, tag, plaintext);
        }
        catch (CryptographicException ex)
        {
            throw new CryptographicException(
                "Could not decrypt the Data Protection key ring. KONTAINR_KEY_PASSPHRASE does not match the " +
                "passphrase the keys were encrypted with.", ex);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }

        try
        {
            return XElement.Parse(Encoding.UTF8.GetString(plaintext));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }

    private static byte[] ReadBase64(XElement parent, string name)
    {
        var value = (string?)parent.Element(name)
            ?? throw new CryptographicException($"Encrypted key ring element is missing '{name}'.");
        return Convert.FromBase64String(value);
    }
}
