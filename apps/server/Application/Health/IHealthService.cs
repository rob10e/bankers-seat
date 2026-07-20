using BankersSeat.Server.Api.V1.Contracts;

namespace BankersSeat.Server.Application.Health;

public interface IHealthService
{
    Task<HealthLiveResponse> GetLiveStatusAsync();
    Task<HealthReadyResponse> GetReadyStatusAsync(CancellationToken cancellationToken);
    Task<HealthTemplatesResponse> GetTemplatesStatusAsync(CancellationToken cancellationToken);
    Task<HealthVersionResponse> GetVersionStatusAsync(CancellationToken cancellationToken);
}
