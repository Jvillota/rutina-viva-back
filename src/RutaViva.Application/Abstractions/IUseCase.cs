using RutaViva.Domain.Common;

namespace RutaViva.Application.Abstractions;

/// <summary>One application use case. Hosts (API, Worker) only depend on these contracts.</summary>
public interface IUseCase<in TRequest, TResponse>
{
    Task<Result<TResponse>> ExecuteAsync(TRequest request, CancellationToken cancellationToken = default);
}
