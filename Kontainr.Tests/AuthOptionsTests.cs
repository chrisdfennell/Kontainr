using Kontainr.Services;
using Microsoft.Extensions.Configuration;

namespace Kontainr.Tests;

public class AuthOptionsTests
{
    private static IConfiguration Config(params (string Key, string Value)[] entries) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(entries.Select(e => new KeyValuePair<string, string?>(e.Key, e.Value)))
            .Build();

    [Fact]
    public void Refuses_to_start_when_no_credentials_are_configured()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => AuthOptions.FromConfiguration(Config()));
        Assert.Contains("Auth__Username and Auth__Password are", ex.Message);
    }

    [Fact]
    public void Refuses_to_start_when_only_a_username_is_configured()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            AuthOptions.FromConfiguration(Config(("Auth:Username", "admin"))));
        Assert.Contains("Auth__Password is", ex.Message);
    }

    [Fact]
    public void Refuses_to_start_when_only_a_password_is_configured()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            AuthOptions.FromConfiguration(Config(("Auth:Password", "hunter2hunter2"))));
        Assert.Contains("Auth__Username is", ex.Message);
    }

    [Fact]
    public void Treats_whitespace_only_credentials_as_missing()
    {
        Assert.Throws<InvalidOperationException>(() =>
            AuthOptions.FromConfiguration(Config(("Auth:Username", "  "), ("Auth:Password", "  "))));
    }

    [Fact]
    public void Enables_auth_when_both_credentials_are_configured()
    {
        var options = AuthOptions.FromConfiguration(
            Config(("Auth:Username", "admin"), ("Auth:Password", "a-long-enough-password")));

        Assert.True(options.Enabled);
        Assert.Equal("admin", options.Username);
        Assert.Equal("a-long-enough-password", options.Password);
    }

    [Fact]
    public void Allows_an_explicit_opt_out()
    {
        var options = AuthOptions.FromConfiguration(Config(("Auth:Disabled", "true")));
        Assert.False(options.Enabled);
    }

    [Fact]
    public void Rejects_an_opt_out_that_also_supplies_credentials()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => AuthOptions.FromConfiguration(
            Config(("Auth:Disabled", "true"), ("Auth:Username", "admin"), ("Auth:Password", "secret"))));

        Assert.Contains("also supplied", ex.Message);
    }

    [Fact]
    public void Warns_about_the_password_shipped_in_the_docs()
    {
        var options = AuthOptions.Enable("admin", "changeme");
        Assert.Contains("well-known default", options.GetWeaknessWarning());
    }

    [Fact]
    public void Warns_about_short_passwords()
    {
        var options = AuthOptions.Enable("admin", "sh0rt");
        Assert.Contains("characters", options.GetWeaknessWarning());
    }

    [Fact]
    public void Does_not_warn_about_a_reasonable_password()
    {
        Assert.Null(AuthOptions.Enable("admin", "correct-horse-battery-staple").GetWeaknessWarning());
    }

    [Fact]
    public void Disabled_options_produce_no_warning()
    {
        Assert.Null(AuthOptions.Disabled().GetWeaknessWarning());
    }
}
