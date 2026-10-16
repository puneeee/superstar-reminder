# Security policy

## Supported versions

Security fixes are applied to the latest main branch during active development.

## Reporting a vulnerability

Do not open a public issue for a suspected vulnerability. Contact the repository owner privately with a clear description, impact, reproducible steps, affected versions, and a suggested remediation if available.

Do not include database passwords, audio files containing personal information, or other sensitive user data in a report.

## Application security rules

- The saved MySQL connection string is protected with Windows DPAPI or macOS Keychain. The development-only SUPERSTAR_MYSQL_CONNECTION environment override must stay local. appsettings.Local.json remains ignored for compatibility; the app no longer reads it.
- Use a dedicated MySQL account with only application database privileges. Never use MySQL root credentials.
- Bind local development MySQL to loopback unless remote access is explicitly required.
- The response recovery journal contains local reminder text and response metadata, but no database credentials. It is removed after the pending write succeeds.
- Parameterize all database access. Do not concatenate user input into SQL.
- Validate imported character packages and local audio file paths before use.
- Treat character packages and audio as local content; V1 does not download executable or scriptable content.
