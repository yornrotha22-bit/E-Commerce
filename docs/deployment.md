# KShop — Deployment guide

This document covers everything needed to run the store on a real server.
It describes the configuration that the code actually reads; nothing here is
speculative.

---

## 1. Requirements

| Component | Version used during development |
| --- | --- |
| .NET SDK / runtime | 10.0.401 SDK, 10.0.12 runtime |
| Database | PostgreSQL 14+ (the EF Core provider is `Npgsql.EntityFrameworkCore.PostgreSQL`) |
| OS | Windows (IIS) or Linux (Kestrel + reverse proxy) |
| HTTPS certificate | Required — the app calls `UseHsts()` and `UseHttpsRedirection()` outside Development |

---

## 2. Configuration reference

All keys live in `appsettings.json` and can be overridden by environment
variables (`Section__Key`) or user secrets. **Never commit real secrets.**

| Key | Purpose | Production value |
| --- | --- | --- |
| `ConnectionStrings:DefaultConnection` | PostgreSQL connection | `Host=db;Port=5432;Database=RothaShopDb;Username=rothashop;Password=…;SSL Mode=Require` |
| `Database:ApplyMigrationsOnStartup` | Runs `Database.MigrateAsync()` at boot | `false` — run migrations as a deployment step |
| `Seed:Enabled` | Creates roles, SuperAdmin, store settings, demo catalog | `false` after the first run |
| `SuperAdmin:Email` / `SuperAdmin:Password` | Bootstrap SuperAdmin, only used when `Seed:Enabled` | Provide once, then remove |
| `Store:Name`, `Store:Currency`, `Store:DefaultShippingFee` | Storefront branding and shipping | Adjust as needed |
| `Payments:Online:Enabled` | Turns on the manual bank-transfer option in checkout | `true` only when the bank details are filled |
| `Payments:BankTransfer:*` | Bank name, account name, account number, instructions | Real bank details |
| `Email:Enabled` | Enables real SMTP delivery for the password-reset email | `true` once SMTP is configured — see `docs/email.md` |
| `Email:Host`, `Port`, `UseStartTls`, `UserName`, `Password` | SMTP server and credentials | Provider values (secret) |
| `Email:FromAddress`, `FromName` | Sender shown to customers | Verified sender address |
| `Cloudinary:CloudName`, `ApiKey`, `ApiSecret` | Stores product images, profile photos and brand logos in Cloudinary | Set all three as environment variables; keep the API secret private |
| `Logging:LogLevel:*` | Log verbosity | Keep `Microsoft.EntityFrameworkCore.Database.Command` at `Warning` |

Notes:

- `FreeShippingThreshold` and the other store settings are stored in the
  `StoreSettings` table (editable in the database) and are seeded from
  `DbInitializer`.
- Payment secrets or gateway keys are **not** part of the current code base;
  a real gateway would add its own configuration section.
- If Cloudinary credentials are unset, uploads use local `wwwroot/uploads`. When all three
  Cloudinary values are set, images are uploaded to Cloudinary and their HTTPS URLs are
  saved in the database.

---

## 3. Environment setup

### Windows / IIS

```powershell
dotnet publish RothaShop.csproj -c Release -o C:\sites\rothashop
```

1. Create an application pool (.NET CLR = "No Managed Code") and an IIS site
   pointing at the publish folder.
2. Enable the **ASP.NET Core Module v2** (installed with the Hosting Bundle).
3. Set the environment variable `ASPNETCORE_ENVIRONMENT=Production` for the pool.
4. Give the app pool identity **Modify** permission on
   `wwwroot\uploads\products` — product images are written there at runtime.
5. Bind an HTTPS certificate and remove the HTTP binding (or redirect to HTTPS
   at the IIS level as well).

### Linux (Kestrel + nginx)

```bash
dotnet publish RothaShop.csproj -c Release -o /var/www/rothashop
```

Run as a `systemd` service with `Environment=ASPNETCORE_ENVIRONMENT=Production`
and `ASPNETCORE_URLS=http://127.0.0.1:5000`. Terminate TLS in nginx and pass
`X-Forwarded-Proto` / `X-Forwarded-For` headers.

> The app does not currently call `UseForwardedHeaders()`; if you terminate TLS
> at a proxy, add it before `UseAuthentication()` so `Request.Scheme` is `https`.
> Without it, cookies marked `SameAsRequest` stay non-secure behind the proxy.

---

## 4. Database deployment

The project uses PostgreSQL. Create the database and a dedicated role first
(the application needs `CREATE TABLE` rights on its own schema):

```sql
CREATE ROLE rothashop LOGIN PASSWORD '...';
CREATE DATABASE "RothaShopDb" OWNER rothashop;
```

Then apply the migrations:

```bash
# from the repository root
dotnet ef database update --connection "Host=localhost;Port=5432;Database=RothaShopDb;Username=rothashop;Password=..."
```

Recommended order:

1. Back up the database (`pg_dump -Fc RothaShopDb > rothashop.dump`).
2. Run `dotnet ef database update` with the production connection string.
3. Start the application once with `Seed:Enabled=true` and
   `SuperAdmin:Email` / `SuperAdmin:Password` set, so roles and the first
   SuperAdmin exist.
4. Set `Seed:Enabled=false`, remove `SuperAdmin:Password`, and restart.

Migration commands require the EF Core tools:

```bash
# version 10 matches Entity Framework Core 10
dotnet tool install --global dotnet-ef --version 10.*
```

### Local development

The password is not stored in `appsettings.json`. Put the real connection string
in user secrets (it is read in Development only and never committed):

```bash
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=RothaShopDb;Username=postgres;Password=..."
```

The application creates the database and applies migrations on startup when
`Database:ApplyMigrationsOnStartup=true` (the Development default).

---

## 5. Secrets

For Cloudinary, create an account and copy the cloud name, API key and API secret from
the Cloudinary dashboard. Set these environment variables on the host (for example,
in Render's service environment settings):

```text
Cloudinary__CloudName
Cloudinary__ApiKey
Cloudinary__ApiSecret
```

Do not put the API secret in source control. Existing images already stored under
`wwwroot/uploads` are not moved automatically; upload them again after enabling Cloudinary.

| Environment | Where to store secrets |
| --- | --- |
| Local development | `dotnet user-secrets` (`UserSecretsId` = `rothashop-ecommerce`) |
| IIS | Environment variables on the application pool, or `appsettings.Production.json` outside source control |
| Linux | `systemd` `EnvironmentFile=` with `chmod 600`, or a secret manager |

Never put the database password, SuperAdmin password, SMTP password or bank details in
`appsettings.json` committed to source control.

---

## 6. Files that must be persisted and backed up

| Path | Content |
| --- | --- |
| `wwwroot/uploads/products/yyyy/MM/` | Uploaded product images (not in the database) |
| PostgreSQL database | Products, orders, payments, accounts, audit log |
| Data Protection keys | Encrypt auth cookies and password-reset tokens |

Data Protection keys default to the user profile
(`%LOCALAPPDATA%\ASP.NET\DataProtection-Keys` or
`$HOME/.aspnet/DataProtection-Keys`). In a load-balanced or containerised
deployment, persist them to a shared location, otherwise every restart signs
all users out and invalidates password-reset tokens.

---

## 7. Security checklist before going live

- [ ] HTTPS only, HTTP redirected.
- [ ] `Seed:Enabled=false` and `SuperAdmin:Password` removed after the first boot.
- [ ] SuperAdmin password changed from the seeded value.
- [ ] Database role uses least privilege (owner of the app database, not a superuser).
- [ ] `Database:ApplyMigrationsOnStartup=false`; migrations are a deployment step.
- [ ] Uploads directory is not executable and is served read-only.
- [ ] Backups scheduled (database + uploads + Data Protection keys).
- [ ] Logs are collected; the app logs sign-ins, order events and admin actions.
- [ ] Payment bank details verified before enabling `Payments:Online:Enabled`.

---

## 8. Operational behaviour to know

| Behaviour | Current implementation |
| --- | --- |
| Deactivated account with an old cookie | Blocked within ~2 minutes (checked on cookie validation, throttled per session) |
| Role change / password reset | Security stamp is refreshed; existing sessions are revalidated within ~5 minutes |
| Password reset by an administrator | Signs the user out on the next validation; the admin must share the new password securely |
| Self-service password reset | `/Account/ForgotPassword` emails a single-use link; tokens expire after 24 hours; responses never reveal whether an account exists. Details: `docs/email.md` |
| Email delivery disabled | Development logs the reset link; other environments only log a warning, so password reset needs SMTP in production |
| Last SuperAdmin protection | Self-demotion and self-deactivation are blocked; the last active SuperAdmin cannot be demoted or deactivated |
| Online payment | Manual bank transfer only; staff confirm payment in `Admin → Payments` |
| Refunds | Not implemented |
| Email confirmation | Not enforced: accounts are created with `EmailConfirmed = true` |
| Session | In-memory distributed cache — restarting the app clears carts' coupon session state |

---

## 9. Smoke test after deployment

1. `GET /` returns the storefront with products.
2. Sign in as SuperAdmin, open `Admin → Users & roles`, create a Staff account.
3. Sign in as Staff: `/Admin/Products` must redirect to Access denied,
   `/Admin/Orders` must load.
4. Add a product to the cart, place a COD order, confirm it in
   `Admin → Orders`.
5. Mark the payment as paid in `Admin → Payments`, then check
   `Admin → Reports` shows the revenue.
6. Upload a product image and verify it appears under `wwwroot/uploads`.
7. Request a password reset from `/Account/ForgotPassword` and confirm the email
   arrives (requires `Email:Enabled=true` with working SMTP).
