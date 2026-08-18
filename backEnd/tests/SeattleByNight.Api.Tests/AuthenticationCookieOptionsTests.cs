using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace SeattleByNight.Api.Tests;

public sealed class AuthenticationCookieOptionsTests : IClassFixture<ApiTestFactory>
{
    private readonly ApiTestFactory _factory;

    public AuthenticationCookieOptionsTests(ApiTestFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public void NonDevelopmentCookie_IsSecureAndDoesNotSlide()
    {
        var options = _factory.Services
            .GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(IdentityConstants.ApplicationScheme);

        Assert.Equal(CookieSecurePolicy.Always, options.Cookie.SecurePolicy);
        Assert.False(options.SlidingExpiration);

        var stampOptions = _factory.Services
            .GetRequiredService<IOptions<SecurityStampValidatorOptions>>()
            .Value;
        Assert.Equal(TimeSpan.Zero, stampOptions.ValidationInterval);
    }
}
