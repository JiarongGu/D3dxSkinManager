using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using D3dxSkinManager.Modules.Core.Models;

namespace D3dxSkinManager.Composition;

/// <summary>
/// Simple service container for managing application services
/// </summary>
public class ServiceContainer
{
    private readonly ServiceCollection _services;
    private ServiceProvider? _serviceProvider;
    private bool _isBuilt = false;

    public ServiceContainer()
    {
        _services = new ServiceCollection();

        // Register AppEnvironment as the first service (bootstrap values)
        var appEnvironment = new AppEnvironment
        {
            BaseDirectory = AppDomain.CurrentDomain.BaseDirectory
        };
        _services.AddSingleton(appEnvironment);

        // Register logging
        _services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Debug);
        });
    }

    /// <summary>
    /// Get the service collection for registration
    /// </summary>
    public IServiceCollection Services => _services;

    /// <summary>
    /// Build the service provider
    /// </summary>
    public ServiceProvider Build()
    {
        if (_isBuilt)
            throw new InvalidOperationException("Service provider has already been built");

        _serviceProvider = _services.BuildServiceProvider();
        _isBuilt = true;

        Console.WriteLine($"[ServiceContainer] Built service provider with {_services.Count} services");
        return _serviceProvider;
    }

    /// <summary>
    /// Get the built service provider
    /// </summary>
    public ServiceProvider GetServiceProvider()
    {
        if (!_isBuilt || _serviceProvider == null)
            throw new InvalidOperationException("Service provider has not been built yet");

        return _serviceProvider;
    }

    /// <summary>
    /// Get a required service from the container
    /// </summary>
    public T GetRequiredService<T>() where T : notnull
    {
        return GetServiceProvider().GetRequiredService<T>();
    }

    /// <summary>
    /// Get an optional service from the container
    /// </summary>
    public T? GetService<T>() where T : class
    {
        return GetServiceProvider().GetService<T>();
    }
}