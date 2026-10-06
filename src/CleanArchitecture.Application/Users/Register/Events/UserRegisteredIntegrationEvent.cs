using CleanArchitecture.Shared;

namespace CleanArchitecture.Application.Users.Register.Events;

public sealed record UserRegisteredIntegrationEvent(Guid UserId) : IIntegrationEvent
{
    // "identity" = bounded context, "user-registered" = event adı, "v1" = şema versiyonu.
    // Sınıf rename/move'dan bağımsız; mevcut tablodaki legacy satırlar migration'da güncelleniyor.
    public static string EventType => "identity.user-registered.v1";
}