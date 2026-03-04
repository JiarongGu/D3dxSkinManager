using D3dxSkinManager.Modules.Core.Utilities;

namespace D3dxSkinManager.Modules.Workflow;

/// <summary>
/// Helper methods for creating structured workflow errors
/// </summary>
public static class WorkflowErrorHelper
{
    /// <summary>
    /// Create a structured error message (JSON) for workflow failures
    /// </summary>
    public static string CreateErrorMessage(string code, Dictionary<string, string>? parameters = null)
    {
        var error = new WorkflowError
        {
            Code = code,
            Parameters = parameters
        };

        return JsonHelper.Serialize(error);
    }

    /// <summary>
    /// Create error message with a single parameter
    /// </summary>
    public static string CreateErrorMessage(string code, string paramKey, string paramValue)
    {
        return CreateErrorMessage(code, new Dictionary<string, string> { { paramKey, paramValue } });
    }
}
