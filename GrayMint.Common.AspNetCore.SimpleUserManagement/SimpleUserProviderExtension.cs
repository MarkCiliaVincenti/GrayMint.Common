﻿using GrayMint.Common.AspNetCore.SimpleRoleAuthorization;
using GrayMint.Common.AspNetCore.SimpleUserManagement.Persistence;
using GrayMint.Common.EntityFrameworkCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GrayMint.Common.AspNetCore.SimpleUserManagement;

public static class SimpleUserProviderExtension
{
    public static void AddSimpleUserProvider(this IServiceCollection services, Action<DbContextOptionsBuilder>? optionsAction = null)
    {
        services.AddDbContext<SimpleUserDbContext>(optionsAction);
        services.AddScoped<ISimpleRoleProvider, SimpleUserProvider>();
        services.AddScoped<SimpleUserProvider>();
        services.AddScoped<SimpleRoleProvider>();
    }

    public static async Task UseSimpleUserProvider(this WebApplication webApplication)
    {
        await using var scope = webApplication.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SimpleUserDbContext>();
        await EfCoreUtil.EnsureTablesCreated(dbContext.Database, SimpleUserDbContext.Schema, nameof(SimpleUserDbContext.Users));
    }
}