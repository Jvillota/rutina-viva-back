using RutaViva.Application.Abstractions;
using RutaViva.Application.Abstractions.Persistence;
using RutaViva.Domain.Common;
using RutaViva.Domain.Workshops;

namespace RutaViva.Application.Fleet;

public sealed record RegisterWorkshopRequest(string Name);

public sealed class RegisterWorkshopUseCase(IWorkshopRepository workshops, IUnitOfWork unitOfWork)
    : IUseCase<RegisterWorkshopRequest, WorkshopView>
{
    public async Task<Result<WorkshopView>> ExecuteAsync(RegisterWorkshopRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var workshop = Workshop.Register(request.Name);
        if (workshop.IsFailure)
        {
            return workshop.Error;
        }

        workshops.Add(workshop.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return workshop.Value.ToView();
    }
}

public sealed record ListWorkshopsRequest;

public sealed class ListWorkshopsUseCase(IWorkshopRepository workshops)
    : IUseCase<ListWorkshopsRequest, IReadOnlyList<WorkshopView>>
{
    public async Task<Result<IReadOnlyList<WorkshopView>>> ExecuteAsync(ListWorkshopsRequest request, CancellationToken cancellationToken = default)
    {
        var all = await workshops.ListAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success<IReadOnlyList<WorkshopView>>(all.Select(w => w.ToView()).ToList());
    }
}
