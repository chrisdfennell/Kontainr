using Kontainr.Services;

namespace Kontainr.Tests;

public class BasicAuthTests
{
    [Fact]
    public void Matches_when_both_credentials_correct()
    {
        Assert.True(BasicAuthMiddleware.CredentialsMatch("admin", "s3cret", "admin", "s3cret"));
    }

    [Fact]
    public void Fails_on_wrong_password()
    {
        Assert.False(BasicAuthMiddleware.CredentialsMatch("admin", "wrong", "admin", "s3cret"));
    }

    [Fact]
    public void Fails_on_wrong_username()
    {
        Assert.False(BasicAuthMiddleware.CredentialsMatch("root", "s3cret", "admin", "s3cret"));
    }

    [Fact]
    public void Comparison_is_case_sensitive()
    {
        Assert.False(BasicAuthMiddleware.CredentialsMatch("Admin", "S3cret", "admin", "s3cret"));
    }

    [Fact]
    public void Fails_when_supplied_credentials_empty()
    {
        Assert.False(BasicAuthMiddleware.CredentialsMatch("", "", "admin", "s3cret"));
    }

    [Fact]
    public void Handles_unicode_credentials()
    {
        Assert.True(BasicAuthMiddleware.CredentialsMatch("üser", "pä55", "üser", "pä55"));
    }
}
