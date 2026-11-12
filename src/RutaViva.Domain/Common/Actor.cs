namespace RutaViva.Domain.Common;

/// <summary>Who triggered a change. Authentication is out of scope, so the actor is declared by the caller.</summary>
public enum Actor
{
    System = 0,
    Workshop = 1,
    Client = 2,
}
