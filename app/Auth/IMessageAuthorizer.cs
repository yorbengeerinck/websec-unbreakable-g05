namespace Unbreakable.Auth;

// De policy enforcement point (PEP) praat enkel met deze interface. In productie is dat OpenFGA
// (OpenFgaMessageAuthorizer); in de tests een in-memory versie. (Bewust niet "IAuthorizationService":
// die naam bestaat al in Microsoft.AspNetCore.Authorization.)
public interface IMessageAuthorizer
{
    Task<bool> CanReadAsync(string sub, int messageId, CancellationToken cancellationToken);

    Task<bool> CanDeleteAsync(string sub, int messageId, CancellationToken cancellationToken);

    // Alle berichten die deze gebruiker mag lezen (ListObjects op can_read).
    Task<IReadOnlyCollection<int>> ReadableMessageIdsAsync(string sub, CancellationToken cancellationToken);

    Task WriteMessageTuplesAsync(int messageId, string senderSub, string recipientSub, CancellationToken cancellationToken);

    Task DeleteMessageTuplesAsync(int messageId, string senderSub, string recipientSub, CancellationToken cancellationToken);
}
