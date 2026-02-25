using D3dxSkinManager.Modules.Context.Services;
using Microsoft.Extensions.DependencyInjection;

namespace D3dxSkinManager.Modules.Context
{
    public static class ContextServiceExtensions
    {
        public static IServiceCollection AddContextServices(this IServiceCollection services, string profileId)
        {
            var profileContext = new ProfileContext(profileId);
            services.AddSingleton<IProfileContext>(profileContext);
            services.AddSingleton<IProfilePathService, ProfilePathService>();
            services.AddSingleton<IProfileServerService, ProfileServerService>();
            services.AddSingleton<IImageService, ImageService>();
            return services;
        }
    }
}
