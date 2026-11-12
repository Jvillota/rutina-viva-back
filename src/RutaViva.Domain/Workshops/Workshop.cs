using RutaViva.Domain.Common;

namespace RutaViva.Domain.Workshops;

/// <summary>External workshop already assigned to vehicles. Choosing or scheduling it is out of scope.</summary>
public sealed class Workshop : AggregateRoot
{
    public const int MaxNameLength = 150;

    public static readonly Error NameRequired =
        Error.Validation("workshop.name_required", $"The workshop name is required and must have at most {MaxNameLength} characters.");

    private Workshop()
    {
        Name = null!;
    }

    public string Name { get; private set; }

    public static Result<Workshop> Register(string name)
    {
        var trimmed = name?.Trim() ?? string.Empty;
        if (trimmed.Length is 0 or > MaxNameLength)
        {
            return NameRequired;
        }

        return new Workshop { Id = Guid.CreateVersion7(), Name = trimmed };
    }
}
