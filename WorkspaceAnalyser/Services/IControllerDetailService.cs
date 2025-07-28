using WorkspaceAnalyser.Models;

namespace WorkspaceAnalyser.Services;

public interface IControllerDetailService
{
    Task<ControllerDetails> GetControllerDetailsAsync(string ustPath);
}