using Microsoft.Extensions.DependencyInjection;
using D3dxSkinManager.Modules.D3DMigoto.Services;
using D3dxSkinManager.Modules.D3DMigoto;
using D3dxSkinManager.Modules.Core.Services;
using D3dxSkinManager.Modules.Tools.Services;

namespace D3dxSkinManager.Modules.D3DMigoto;

/// <summary>
/// Service registration extensions for D3DMigoto module
/// Registers 3DMigoto version management services and facade
/// </summary>
public static class D3DMigotoServiceExtensions
{
    /// <summary>
    /// Register D3DMigoto module services and facade
    /// </summary>
    public static IServiceCollection AddD3DMigotoServices(this IServiceCollection services, string dataPath)
    {
        // Register 3DMigoto service
        services.AddSingleton<I3DMigotoService>(sp =>
        {
            var profileContext = sp.GetService<Profiles.Services.IProfileContext>();
            var profileDataPath = profileContext?.GetProfileDataPath() ?? System.IO.Path.Combine(dataPath, "profiles", "default");
            var fileService = sp.GetRequiredService<IFileService>();
            var configService = sp.GetRequiredService<IConfigurationService>();
            var processService = sp.GetRequiredService<IProcessService>();
            return new D3DMigotoService(profileDataPath, fileService, configService, processService);
        });

        // Register facade
        services.AddSingleton<ID3DMigotoFacade, D3DMigotoFacade>();

        return services;
    }
}
