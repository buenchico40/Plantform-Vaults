using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;
using PlatformVault.Application.Abstractions;

namespace PlatformVault.Infrastructure.Notifications;

public sealed class SmtpOptions
{
    public const string Section = "Smtp";

    /// <summary>Apagado por defecto (IMP-35). En desarrollo los mensajes quedan en la tabla como Suppressed.</summary>
    public bool Enabled { get; set; }
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public string From { get; set; } = string.Empty;
    public string? UserName { get; set; }

    /// <summary>Se carga desde el almacén de secretos del servidor, nunca desde appsettings versionado.</summary>
    public string? Password { get; set; }
}

/// <summary>Correo autenticado sobre TLS (IMP-16). El cuerpo nunca contiene valores sensibles (RN-067).</summary>
public sealed class SmtpNotificationSender(IOptions<SmtpOptions> options) : INotificationSender
{
    public bool IsEnabled => options.Value.Enabled && !string.IsNullOrWhiteSpace(options.Value.Host);

    public async Task SendAsync(string recipient, string subject, string body, CancellationToken ct)
    {
        var o = options.Value;
        using var client = new SmtpClient(o.Host, o.Port) { EnableSsl = true, DeliveryMethod = SmtpDeliveryMethod.Network };
        if (!string.IsNullOrEmpty(o.UserName))
            client.Credentials = new NetworkCredential(o.UserName, o.Password);
        using var message = new MailMessage(o.From, recipient, subject, body) { IsBodyHtml = false };
        await client.SendMailAsync(message, ct);
    }
}
