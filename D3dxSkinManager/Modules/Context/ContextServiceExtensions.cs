using D3dxSkinManager.Modules.Context.Services;
using D3dxSkinManager.Modules.Core.Event;
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

            // Register profile-scoped EventBus (filters events by profileId, auto-injects profileId on emit)
            services.AddSingleton<IProfileEventBus, ProfileEventBus>();

            return services;
        }
    }
}
