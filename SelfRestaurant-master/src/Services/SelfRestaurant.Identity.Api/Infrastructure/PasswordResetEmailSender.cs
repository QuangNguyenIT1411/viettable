using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;

namespace SelfRestaurant.Identity.Api.Infrastructure;

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
        string resetToken,
        string? resetLink,
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
                Body = BuildHtmlBody(toName, resetToken, resetLink),
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

    private static string BuildHtmlBody(string? customerName, string resetToken, string? resetLink)
    {
        var displayName = string.IsNullOrWhiteSpace(customerName) ? "Bạn" : customerName.Trim();
        var safeLink = string.IsNullOrWhiteSpace(resetLink) ? null : WebUtility.HtmlEncode(resetLink);

        if (!string.IsNullOrWhiteSpace(safeLink))
        {
            return $@"
<div style=""font-family:Segoe UI,Arial,sans-serif;line-height:1.6;color:#1f2937"">
  <h2 style=""margin-bottom:12px"">Đặt lại mật khẩu Self Restaurant</h2>
  <p>Xin chào {WebUtility.HtmlEncode(displayName)},</p>
  <p>Chúng tôi đã nhận được yêu cầu đặt lại mật khẩu cho tài khoản của bạn.</p>
  <p>Vui lòng nhấn vào nút bên dưới để mở trang đặt lại mật khẩu:</p>
  <p style=""margin:24px 0"">
    <a href=""{safeLink}"" style=""display:inline-block;background:#d9534f;color:#ffffff;text-decoration:none;padding:12px 20px;border-radius:8px;font-weight:600"">Mở trang đặt lại mật khẩu</a>
  </p>
  <p>Nếu nút không hoạt động, bạn có thể sao chép đường dẫn sau và mở trong trình duyệt:</p>
  <p style=""word-break:break-all"">{safeLink}</p>
  <p>Liên kết này sẽ hết hạn sau 30 phút.</p>
  <p>Nếu bạn không yêu cầu thao tác này, hãy bỏ qua email này.</p>
</div>";
        }

        return $@"
<div style=""font-family:Segoe UI,Arial,sans-serif;line-height:1.6;color:#1f2937"">
  <h2 style=""margin-bottom:12px"">Đặt lại mật khẩu Self Restaurant</h2>
  <p>Xin chào {WebUtility.HtmlEncode(displayName)},</p>
  <p>Nhập mã sau để đặt lại mật khẩu của bạn:</p>
  <p style=""font-size:18px;font-weight:700;letter-spacing:1px"">{WebUtility.HtmlEncode(resetToken)}</p>
  <p>Nếu bạn không yêu cầu thao tác này, hãy bỏ qua email này.</p>
</div>";
    }
}
