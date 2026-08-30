using System.Security.Cryptography;
using System.Xml.Linq;
using Kontainr.Services;

namespace Kontainr.Tests;

public class PassphraseKeyProtectionTests
{
    /// <summary>Minimal service provider — the decryptor only ever resolves the passphrase.</summary>
    private sealed class Services(KeyPassphrase? passphrase) : IServiceProvider
    {
        public object? GetService(Type serviceType) =>
            serviceType == typeof(KeyPassphrase) ? passphrase : null;
    }

    private static readonly XElement SampleKey = XElement.Parse(
        "<key id='abc'><descriptor><secret>s3cret-master-key</secret></descriptor></key>");

    [Fact]
    public void Round_trips_a_key_ring_element()
    {
        var encryptor = new PassphraseXmlEncryptor(new KeyPassphrase("correct horse battery staple"));
        var decryptor = new PassphraseXmlDecryptor(new Services(new KeyPassphrase("correct horse battery staple")));

        var encrypted = encryptor.Encrypt(SampleKey);
        var decrypted = decryptor.Decrypt(encrypted.EncryptedElement);

        Assert.Equal(SampleKey.ToString(SaveOptions.DisableFormatting), decrypted.ToString(SaveOptions.DisableFormatting));
    }

    [Fact]
    public void Encrypted_element_does_not_contain_the_plaintext()
    {
        var encryptor = new PassphraseXmlEncryptor(new KeyPassphrase("a passphrase"));

        var encrypted = encryptor.Encrypt(SampleKey).EncryptedElement.ToString();

        Assert.DoesNotContain("s3cret-master-key", encrypted);
    }

    [Fact]
    public void Encrypting_the_same_element_twice_produces_different_ciphertext()
    {
        var encryptor = new PassphraseXmlEncryptor(new KeyPassphrase("a passphrase"));

        var first = (string)encryptor.Encrypt(SampleKey).EncryptedElement.Element("value")!;
        var second = (string)encryptor.Encrypt(SampleKey).EncryptedElement.Element("value")!;

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Rejects_the_wrong_passphrase()
    {
        var encrypted = new PassphraseXmlEncryptor(new KeyPassphrase("right")).Encrypt(SampleKey);
        var decryptor = new PassphraseXmlDecryptor(new Services(new KeyPassphrase("wrong")));

        var ex = Assert.Throws<CryptographicException>(() => decryptor.Decrypt(encrypted.EncryptedElement));
        Assert.Contains("does not match", ex.Message);
    }

    [Fact]
    public void Rejects_tampered_ciphertext()
    {
        var encryptor = new PassphraseXmlEncryptor(new KeyPassphrase("a passphrase"));
        var element = encryptor.Encrypt(SampleKey).EncryptedElement;

        // Flip a byte of the ciphertext — AES-GCM's tag should catch it.
        var bytes = Convert.FromBase64String((string)element.Element("value")!);
        bytes[0] ^= 0xFF;
        element.Element("value")!.Value = Convert.ToBase64String(bytes);

        var decryptor = new PassphraseXmlDecryptor(new Services(new KeyPassphrase("a passphrase")));
        Assert.Throws<CryptographicException>(() => decryptor.Decrypt(element));
    }

    [Fact]
    public void Explains_itself_when_the_passphrase_is_missing_entirely()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => new PassphraseXmlDecryptor(new Services(null)));
        Assert.Contains("KONTAINR_KEY_PASSPHRASE", ex.Message);
    }
}
