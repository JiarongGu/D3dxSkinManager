using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using D3dxSkinManager.Modules.Core.Models;
using D3dxSkinManager.Modules.Core.Services;
using D3dxSkinManager.Modules.D3DMigoto.Models;
using D3dxSkinManager.Modules.D3DMigoto.Services;

namespace D3dxSkinManager.Modules.D3DMigoto;

/// <summary>
/// Facade for 3DMigoto management operations
/// Responsibility: 3DMigoto version management and launching
/// IPC Prefix: D3DMIGOTO_*
/// </summary>
public class D3DMigotoFacade : ID3DMigotoFacade
{
    private readonly I3DMigotoService _d3dMigotoService;

    public D3DMigotoFacade(I3DMigotoService d3dMigotoService)
    {
        _d3dMigotoService = d3dMigotoService ?? throw new ArgumentNullException(nameof(d3dMigotoService));
    }

    public async Task<MessageResponse> HandleMessageAsync(MessageRequest request)
    {
        try
        {
            Console.WriteLine($"[D3DMigotoFacade] Handling message: {request.Type}");

            object? responseData = request.Type switch
            {
                "GET_VERSIONS" or "GET_VERSIONS" => await GetAvailableVersionsAsync(),
                "GET_CURRENT" or "GET_CURRENT" => await GetCurrentVersionAsync(),
                "DEPLOY" or "DEPLOY" => await DeployVersionAsync(request),
                "LAUNCH" or "LAUNCH" => await LaunchAsync(),
                _ => throw new InvalidOperationException($"Unknown message type: {request.Type}")
            };

            return MessageResponse.CreateSuccess(request.Id, responseData);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[D3DMigotoFacade] Error handling message: {ex.Message}");
            return MessageResponse.CreateError(request.Id, ex.Message);
        }
    }

    public async Task<List<D3DMigotoVersion>> GetAvailableVersionsAsync()
    {
        return await _d3dMigotoService.GetAvailableVersionsAsync();
    }

    public async Task<string?> GetCurrentVersionAsync()
    {
        return await _d3dMigotoService.GetCurrentVersionAsync();
    }

    public async Task<DeploymentResult> DeployVersionAsync(string versionName)
    {
        return await _d3dMigotoService.DeployVersionAsync(versionName);
    }

    public async Task<bool> LaunchAsync()
    {
        return await _d3dMigotoService.LaunchAsync();
    }

    private async Task<DeploymentResult> DeployVersionAsync(MessageRequest request)
    {
        var versionName = PayloadHelper.GetRequiredValue<string>(request.Payload, "versionName");
        return await DeployVersionAsync(versionName);
    }
}
