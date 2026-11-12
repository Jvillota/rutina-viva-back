namespace RutaViva.Domain.Common;

/// <summary>Category of a business error. Hosts translate it to a transport status (400/404/409).</summary>
public enum ErrorType
{
    None = 0,
    Validation = 1,
    NotFound = 2,
    Conflict = 3,
}
