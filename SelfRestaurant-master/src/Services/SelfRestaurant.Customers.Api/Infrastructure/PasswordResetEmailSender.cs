using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;

namespace SelfRestaurant.Customers.Api.Infrastructure;

public sealed class PasswordResetEmailSender
{
    private readonly SmtpOptions _options;
    private readonly ILogger<PasswordResetEmailSender> _logger;

    public PasswordResetEmailSender(IOptions<SmtpOptions> options, ILogger<PasswordResetEmailSender> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public bool IsEnabled =>
        _options.Enabled
        && !string.IsNullOrWhiteSpace(_options.Host)
        && _options.Port > 0
        && !string.IsNullOrWhiteSpace(_options.FromEmail);

    public async Task<bool> TrySendAsync(
        string toEmail,
        string? toName,
        string resetUrl,
        DateTime expiresAtUtc,
        CancellationToken cancellationToken)
    {
        if (!IsEnabled)
        {
            return false;
        }

        try
        {
            using var message = new MailMessage
            {
                Subject = "SelfRestaurant - Đặt lại mật khẩu",
                IsBodyHtml = true,
                Body = BuildHtmlBody(toName, resetUrl, expiresAtUtc),
            };

            message.From = new MailAddress(_options.FromEmail, _options.FromName);
            message.To.Add(new MailAddress(toEmail, toName ?? toEmail));

            using var smtp = new SmtpClient(_options.Host, _options.Port)
            {
                EnableSsl = _options.UseSsl,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                Timeout = _options.TimeoutMs,
            };

            if (!string.IsNullOrWhiteSpace(_options.Username))
            {
                smtp.Credentials = new NetworkCredential(_options.Username, _options.Password);
            }

            await smtp.SendMailAsync(message).WaitAsync(cancellationToken);
            _logger.LogInformation("Password reset email sent to {Email}", toEmail);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send password reset email to {Email}", toEmail);
            return false;
        }
    }

    private static string BuildHtmlBody(string? customerName, string resetUrl, DateTime expiresAtUtc)
    {
        var displayName = string.IsNullOrWhiteSpace(customerName) ? "Bạn" : customerName.Trim();
        var expiresText = expiresAtUtc.ToLocalTime().ToString("HH:mm dd/MM/yyyy");

        return $@"
<div style=""font-family:Segoe UI,Arial,sans-serif;line-height:1.6;color:#1f2937"">
  <h2 style=""margin-bottom:12px"">Đặt lại mật khẩu SelfRestaurant</h2>
  <p>Xin chào {WebUtility.HtmlEncode(displayName)},</p>
  <p>Bạn vừa gửi yêu cầu đặt lại mật khẩu. Nhấn nút bên dưới để tạo mật khẩu mới:</p>
  <p style=""margin:20px 0"">
    <a href=""{WebUtility.HtmlEncode(resetUrl)}"" style=""background:#d9534f;color:#fff;padding:10px 16px;border-radius:8px;text-decoration:none;font-weight:600"">Đặt lại mật khẩu</a>
  </p>
  <p>Liên kết có hiệu lực đến: <strong>{WebUtility.HtmlEncode(expiresText)}</strong>.</p>
  <p>Nếu bạn không yêu cầu thao tác này, hãy bỏ qua email này.</p>
</div>";
    }
}
