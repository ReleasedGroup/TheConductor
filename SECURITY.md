# 🔐 AGENTS.md – Security Standards for .NET Core Applications

## Purpose

This document defines **mandatory security practices** for all agents, developers, and automated systems working on this repository.

Security is **not optional**. Any code that violates these rules must be rejected.

This standard is aligned with:

* OWASP Top 10 (industry baseline) ([OWASP Foundation][1])
* Microsoft ASP.NET Core security guidance ([Microsoft Learn][2])

---

## 🚫 Absolute Rules (Non-Negotiable)

1. **Never trust input**

   * All input is untrusted
   * Validate server-side only
   * Reject or sanitise everything ([OWASP Foundation][3])

2. **No secrets in code**

   * No API keys, passwords, connection strings in:

     * source code
     * config files
     * logs
   * Use secure secret stores (Azure Key Vault)

3. **Authentication must be standard**

   * No custom auth
   * Use:

     * OpenID Connect
     * OAuth2
     * Azure AD / Entra ID

4. **All traffic must be encrypted**

   * HTTPS only
   * Enforce HSTS
   * No insecure protocols ([Escape][4])

5. **Principle of least privilege**

   * Every user, service, and API gets minimal access
   * Enforce authorisation on every request ([Hicron Software][5])

---

## 🔑 Authentication & Authorisation

* Use ASP.NET Core Identity or external providers
* Enforce:

  * MFA for privileged users
  * Strong password policies
* Always use:

  * `[Authorize]` attributes
  * Claims-based or role-based access

❌ Do NOT:

* Build custom login systems
* Store passwords in plain text

---

## 🛡️ Input Validation & Output Encoding

* Validate:

  * DTOs using DataAnnotations or FluentValidation
* Use:

  * Whitelisting over blacklisting
* Encode output:

  * Razor auto-encoding is mandatory

Protect against:

* XSS
* Injection attacks
* Logic abuse ([C# Corner][6])

---

## 💾 Data Protection

* Encrypt sensitive data:

  * At rest (AES-256 or platform equivalent)
  * In transit (TLS 1.2+)
* Use:

  * ASP.NET Core Data Protection APIs
* Never log:

  * PII
  * credentials
  * tokens

---

## 🧱 Database Security

* Always use:

  * Entity Framework or parameterised queries
* Never:

  * concatenate SQL strings

Example (safe):

```csharp
var user = db.Users.FirstOrDefault(u => u.Id == id);
```

---

## 🌐 API Security

* Require authentication for all endpoints by default

* Explicitly allow anonymous only where justified

* Implement:

  * Rate limiting
  * API keys or tokens
  * Request size limits

* Validate:

  * JSON payloads
  * headers
  * query parameters

---

## 🍪 Session & Cookie Security

* Cookies must be:

  * Secure
  * HttpOnly
  * SameSite=Strict

* Do NOT:

  * store tokens in localStorage (for browser apps)
  * expose session IDs

---

## 🧾 Security Headers

All responses must include:

* `Content-Security-Policy`
* `X-Content-Type-Options`
* `X-Frame-Options`
* `Strict-Transport-Security`

(Security headers reduce browser-based attacks) ([OWASP Foundation][7])

---

## 🔄 Dependency & Supply Chain Security

* Scan dependencies continuously

* Block builds if:

  * known vulnerabilities exist

* Tools:

  * Snyk / Dependabot / OSS Index

---

## 🧪 Logging & Monitoring

* Log:

  * authentication failures
  * access violations
  * suspicious activity

* Never log:

  * secrets
  * tokens
  * full request bodies

* Ensure logs are:

  * tamper-resistant
  * centrally stored

---

## 🚀 Deployment Security

* Enforce:

  * environment separation (dev/test/prod)

* Disable:

  * debug mode in production

* Restrict:

  * IP access where possible

* Use:

  * Managed identities instead of credentials

---

## ⚙️ Secure Coding Requirements

Do NOT use:

* BinaryFormatter
* .NET Remoting
* DCOM
* Partial trust code ([Microsoft Learn][8])

---

## 🧠 Threat Model Requirements

All new features must:

* Identify:

  * attack surface
  * data flows
* Consider:

  * OWASP Top 10 risks
* Document:

  * mitigation strategies

---

## 🧩 CI/CD Security Gates

Every PR must pass:

* Static analysis (SonarQube or equivalent)
* Dependency vulnerability scan
* Secret scan

❌ Auto-reject if:

* Critical vulnerabilities detected
* Secrets exposed

---

## 🔍 Testing Requirements

Minimum:

* Authentication tests
* Authorisation tests
* Input validation tests

Recommended:

* Automated security scans
* Pen testing for major releases

---

## ⚠️ Common Failure Patterns (Seen Too Often)

* “We’ll fix security later” → you won’t
* Custom auth → always broken
* Logging everything → data breach waiting to happen
* Over-permissive APIs → easiest exploit path

---

## 🧱 Security Philosophy

* Secure by default
* Deny by default
* Explicitly allow only what is required

---

## ✅ Final Rule

If there is uncertainty:

> **Fail closed, not open**

---

If you want, I can tighten this further into:

* **CI/CD YAML enforcement**
* **Azure-native version (Key Vault, Managed Identity, App Gateway WAF)**
* **Sonar ruleset + policy as code**

That’s where this really becomes enforceable rather than just documentation.

[1]: https://owasp.org/www-project-top-ten/?utm_source=chatgpt.com "OWASP Top Ten Web Application Security Risks"
[2]: https://learn.microsoft.com/en-us/aspnet/core/security/?view=aspnetcore-10.0&utm_source=chatgpt.com "ASP.NET Core security topics"
[3]: https://owasp.org/www-project-secure-coding-practices-quick-reference-guide/stable-en/02-checklist/05-checklist?utm_source=chatgpt.com "Secure Coding Practices Checklist"
[4]: https://escape.tech/blog/asp-dot-net-security/?utm_source=chatgpt.com "ASP.NET security best practices"
[5]: https://hicronsoftware.com/blog/web-app-security-checklist-owasp-top-10/?utm_source=chatgpt.com "Complete Web App Security Checklist Using the OWASP ..."
[6]: https://www.c-sharpcorner.com/article/application-security-checklist-for-c-sharp-developers/?utm_source=chatgpt.com "Application Security Checklist for C# Developers"
[7]: https://owasp.org/www-project-secure-headers/?utm_source=chatgpt.com "OWASP Secure Headers Project"
[8]: https://learn.microsoft.com/en-us/dotnet/standard/security/secure-coding-guidelines?utm_source=chatgpt.com "Secure coding guidelines for .NET"
