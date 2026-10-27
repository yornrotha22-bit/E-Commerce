# Email and password reset

The store sends one transactional message today: the **password reset link**.
Everything is configurable; email delivery is **off by default** so the app runs
without an SMTP server.

---

## 1. Configuration

`appsettings.json`:

```json
"Email": {
  "Enabled": false,
  "Host": "",
  "Port": 587,
  "UseStartTls": true,
  "UserName": "",
  "Password": "",
  "FromAddress": "no-reply@rothashop.local",
  "FromName": "KShop"
}
```

| Key | Meaning |
| --- | --- |
| `Enabled` | Must be `true` for real delivery. Takes effect only when `Host` and `FromAddress` are also set. |
| `Host` / `Port` | SMTP server. `587` with `UseStartTls` is the common choice; `465` servers usually need `UseStartTls=false` (implicit TLS). |
| `UseStartTls` | Passed to `SmtpClient.EnableSsl`. |
| `UserName` / `Password` | SMTP credentials. Leave empty for an unauthenticated relay (e.g. MailHog). |
| `FromAddress` | Envelope/from address; most providers require a verified sender. |
| `FromName` | Display name in the inbox. |

Production values belong in environment variables or a secret store:

```
Email__Enabled=true
Email__Host=smtp.provider.example
Email__Port=587
Email__UserName=...
Email__Password=...
Email__FromAddress=no-reply@yourdomain.com
```

---

## 2. Behaviour when email is disabled

| Environment | What happens |
| --- | --- |
| Development | The message is written to the application log, including the reset link, so the flow can be tested without a server. |
| Production / Staging | Only a warning is logged (`message was not sent`); the body and link are **never** logged. |

The forgot-password page always shows the same confirmation, so an operator
without SMTP does not expose anything, but users cannot complete a reset — set
up SMTP before going live.

---

## 3. Password reset flow

| Step | Endpoint | Notes |
| --- | --- | --- |
| 1 | `GET /Account/ForgotPassword` | Form; signed-in users are redirected to their profile. |
| 2 | `POST /Account/ForgotPassword` | Looks up the account, generates an Identity reset token and emails a link. Always redirects to the confirmation page. |
| 3 | `GET /Account/ForgotPasswordConfirmation` | Generic "check your email" page (no account enumeration). |
| 4 | `GET /Account/ResetPassword?email=…&token=…` | Shows the new-password form; missing parameters redirect back to step 1. |
| 5 | `POST /Account/ResetPassword` | `ResetPasswordAsync` enforces the password policy and rotates the security stamp, then redirects to sign-in. |

Security properties:

- No account enumeration: unknown, inactive and valid addresses produce the same response.
- A best-effort cooldown of **60 seconds per session** prevents repeated requests from one browser; there is no global rate limiter yet.
- Reset tokens are single use: consuming one rotates the security stamp, so reusing the link fails with "This reset link is invalid or has expired."
- Tokens expire after the ASP.NET Core Identity default lifespan (**24 hours**). Lower it with `builder.Services.Configure<DataProtectionTokenProviderOptions>(...)` if needed.
- The password policy from `Program.cs` applies (8+ characters, upper, lower, digit, symbol).
- Successful resets are written to `AuditLogs` as `PasswordResetCompleted`.
- Inactive accounts cannot request or complete a reset.

---

## 4. Local testing recipes

**Log only (default)**

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Development'
dotnet run --no-launch-profile --urls http://localhost:5081
```

Request a reset, then read the link from the console output.

**Local SMTP catcher**

Run [MailHog](https://github.com/mailhog/MailHog) or
[Papercut SMTP](https://github.com/ChangemakerStudios/Papercut-SMTP), then:

```powershell
$env:Email__Enabled = 'true'
$env:Email__Host = 'localhost'
$env:Email__Port = '1025'
$env:Email__UseStartTls = 'false'
$env:Email__FromAddress = 'no-reply@rothashop.local'
dotnet run --no-launch-profile --urls http://localhost:5080
```

**Provider notes**

- Gmail requires an app password and `smtp.gmail.com:587`.
- Most transactional providers (SendGrid, Mailgun, Amazon SES, Brevo) expose an
  SMTP endpoint and require a verified `FromAddress`; use their API keys as
  `Email:Password` where SMTP credentials are offered.

---

## 5. Not implemented yet

- Email confirmation for new accounts (`EmailConfirmed` is set to `true` on
  registration, and `SignIn.RequireConfirmedAccount` is `false`).
- Order confirmation / shipping notification emails.
- Bounce handling, unsubscribe lists and delivery webhooks.
- A global rate limiter for the forgot-password endpoint (only the per-session
  cooldown above exists).
