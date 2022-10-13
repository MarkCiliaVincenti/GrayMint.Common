﻿using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace GrayMint.Common.AspNetCore.Auth.BotAuthentication;

public static class BotAuthenticationExtension
{
    public static AuthenticationBuilder AddBotAuthentication(this AuthenticationBuilder authenticationBuilder, IConfiguration configuration, bool isProduction)
    {
        var botAuthenticationOptions = configuration.Get<BotAuthenticationOptions>();
        botAuthenticationOptions.Validate(isProduction);

        var securityKey = new SymmetricSecurityKey(botAuthenticationOptions.BotKey);
        authenticationBuilder
            .AddJwtBearer(BotAuthenticationDefaults.AuthenticationScheme, options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    NameClaimType = "name",
                    RequireSignedTokens = true,
                    IssuerSigningKey = securityKey,
                    ValidIssuer = botAuthenticationOptions.BotIssuer,
                    ValidAudience = botAuthenticationOptions.BotAudience ?? botAuthenticationOptions.BotIssuer,
                    ValidateAudience = true,
                    ValidateIssuerSigningKey = true,
                    ValidateIssuer = true,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(TokenValidationParameters.DefaultClockSkew.TotalSeconds)
                };
                options.Events = new JwtBearerEvents()
                {
                    OnTokenValidated = async context =>
                    {
                        await using var scope = context.HttpContext.RequestServices.CreateAsyncScope();
                        var tokenValidator = scope.ServiceProvider.GetRequiredService<BotTokenValidator>();
                        await tokenValidator.Validate(context);
                    }
                };
            });

        authenticationBuilder.Services.Configure<BotAuthenticationOptions>(configuration);
        authenticationBuilder.Services.AddScoped<BotTokenValidator>();
        authenticationBuilder.Services.AddScoped<BotAuthenticationTokenBuilder>();
        return authenticationBuilder;
    }

    private class BotTokenValidator
    {
        private readonly IBotAuthenticationProvider _botAuthenticationProvider;
        private readonly IMemoryCache _memoryCache;
        private readonly IOptions<BotAuthenticationOptions> _botAuthenticationOptions;

        public BotTokenValidator(IBotAuthenticationProvider botAuthenticationProvider, IMemoryCache memoryCache, IOptions<BotAuthenticationOptions> botAuthenticationOptions)
        {
            _botAuthenticationProvider = botAuthenticationProvider;
            _memoryCache = memoryCache;
            _botAuthenticationOptions = botAuthenticationOptions;
        }

        public async Task<string?> GetValidateError(TokenValidatedContext context)
        {
            if (context.Principal == null)
                return "Principal has not been validated.";

            var authCode = await _botAuthenticationProvider.GetAuthCode(context.Principal);
            if (string.IsNullOrEmpty(authCode))
                return $"{BotAuthenticationDefaults.AuthenticationScheme} needs authCode.";

            // deserialize access token
            var tokenAuthCode = context.Principal.Claims.SingleOrDefault(x => x.Type == "AuthCode")?.Value;
            if (string.IsNullOrEmpty(authCode))
                return "Could not find AuthCode in the token.";

            return authCode != tokenAuthCode ? "Invalid AuthCode." : null;
        }

        public async Task Validate(TokenValidatedContext context)
        {
            var jwtSecurityToken = (JwtSecurityToken)context.SecurityToken;
            var accessToken = jwtSecurityToken.RawData;
            var accessTokenHash = MD5.Create().ComputeHash(Encoding.UTF8.GetBytes(accessToken));
            var cacheKey = "BotAuthentication/" + Convert.ToBase64String(accessTokenHash);
            if (!_memoryCache.TryGetValue<string?>(cacheKey, out var error))
            {
                error = await GetValidateError(context);
                _memoryCache.Set(cacheKey, error, _botAuthenticationOptions.Value.CacheTimeout);
            }

            if (error != null)
                context.Fail(error);
        }
    }
}
