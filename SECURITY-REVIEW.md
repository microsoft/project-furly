# Security Review

Date: 2026-07-08

Scope: full `Furly` monorepo (all production libraries plus test projects). This review focused on high-confidence, genuinely exploitable issues across injection, authn/authz, cryptography, secrets, deserialization, SSRF, path traversal, XXE, transport/TLS, unsafe reflection, and input validation.

Both findings below are **pre-existing** — neither was introduced by the recent .NET 10 migration or dependency upgrades. The recommended remediations change library behavior for all downstream consumers, so they are documented here for maintainer sign-off rather than applied unilaterally.

## Findings

| # | Severity | File | Lines | Vulnerability | Confidence |
|---|----------|------|-------|---------------|------------|
| 1 | MEDIUM | `src/Furly.Extensions.AspNetCore/src/Hosting/Runtime/HeadersConfig.cs` | 43-49 | Forwarded-headers processing clears both `KnownIPNetworks` and `KnownProxies`, disabling trusted-proxy validation → source-IP and scheme spoofing | 7/10 |
| 2 | LOW | `src/Furly.Extensions.CouchDb/src/Clients/CouchDbClient.cs` | 41, 56-58, 70, 79-82 | CouchDB connection scheme hard-coded to `http://` while Basic-auth credentials are sent base64-encoded → credentials transmitted in cleartext; scheme not configurable | 6/10 |

### 1. Forwarded-headers trust model disables known-proxy validation (MEDIUM)

When forwarded-header processing is enabled, `HeadersConfig.Configure` unconditionally clears both the `KnownIPNetworks` and `KnownProxies` collections:

```csharp
options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
// Only loopback proxies are allowed by default.
// Clear that restriction because forwarders are enabled by explicit
// configuration.
options.KnownIPNetworks.Clear();
options.KnownProxies.Clear();
```

ASP.NET Core's `ForwardedHeadersMiddleware` only performs the trusted-proxy check when at least one known network or proxy is registered (`checkKnownIps = KnownIPNetworks.Count > 0 || KnownProxies.Count > 0`). With both collections empty, the middleware **skips proxy validation entirely** and applies `X-Forwarded-For` / `X-Forwarded-Proto` from any remote source (up to `ForwardLimit`).

Impact when the app is directly reachable by untrusted clients and relies on the forwarded values for a security decision:

- **Source-IP spoofing** via `X-Forwarded-For`: `HttpContext.Connection.RemoteIpAddress` becomes attacker-controlled, defeating IP allow-lists, rate-limit exemptions, and forging audit logs.
- **Scheme spoofing** via `X-Forwarded-Proto: https`: the app believes a plaintext request arrived over TLS, undermining HTTPS-redirection logic, `Secure`-cookie enforcement, and generated absolute URLs.

The feature is opt-in (`HeadersOptions.ForwardingEnabled` defaults to `false`; set via `FORWARDEDHEADERSENABLED`). Because the library hard-codes the clearing, a consumer that enables forwarding has no supported way to restrict trust to its actual reverse proxy / load balancer.

**Recommended remediation:** expose configuration for trusted proxies/networks (a CIDR / IP list, e.g. via a new `EnvironmentVariable` such as `FORWARDEDHEADERSKNOWNPROXIES` / `FORWARDEDHEADERSKNOWNNETWORKS`), populate `KnownProxies` / `KnownIPNetworks` from it, and only fall back to clearing (trust-all) behind an explicit opt-in flag. This aligns Furly with the framework's secure default (trust forwarded headers only from registered proxies).

Note: this is a deliberate behavior change for existing consumers who currently rely on the trust-all behavior, so it should ship with clear release notes and, ideally, a coverage test for the new configuration path.

### 2. CouchDB client uses cleartext HTTP for Basic-auth credentials (LOW)

`CouchDbClient.OpenAsync` and `CheckHealthAsync` construct the `CouchClient` with a hard-coded `http://` scheme and fixed port `5984`, then attach Basic authentication:

```csharp
var client = new CouchClient("http://" + _options.Value.HostName + ":5984",
    builder => {
        builder = builder.EnsureDatabaseExists().IgnoreCertificateValidation();
        if (_options.Value.UserName is not null && _options.Value.Key is not null)
        {
            builder = builder.UseBasicAuthentication(_options.Value.UserName, _options.Value.Key);
        }
    });
```

`UseBasicAuthentication` sends the password as `Authorization: Basic <base64>`. With a hard-coded `http://` scheme the credentials are always transmitted in cleartext, so any party able to observe traffic between the app and CouchDB can recover them. The adjacent `IgnoreCertificateValidation()` call is a no-op here because the scheme is plaintext HTTP (it would matter only under HTTPS, where disabling cert validation would itself be a MITM risk).

Real-world exploitability depends on the network path to CouchDB; deployments where CouchDB is on `localhost` or a trusted subnet are low risk, which is why this is rated LOW.

**Recommended remediation:** make the scheme (and port) configurable and default to HTTPS for non-loopback hosts; only enable `IgnoreCertificateValidation()` behind an explicit, clearly named development-only opt-in.

## Areas examined and cleared

- **MessagePack serializer** (`MessagePackSerializer.cs`): uses `MessagePackSecurity.UntrustedData`; no typeless resolver — safe against deserialization gadget attacks.
- **Newtonsoft / System.Text.Json serializers**: `TypeNameHandling` left at default `None`; no polymorphic type-name deserialization from untrusted input.
- **XML handling**: `XmlDocument.LoadXml` with the default (null) `XmlResolver` on .NET 10 — no external-entity (XXE) exposure.
- **MQTT TLS** (`MqttClient`): `AllowUntrustedCertificates` defaults to `false` and is config-driven; `MqttServer` negotiates `SslProtocols.None` (OS-chosen, not a weak fixed protocol).
- **CouchDb `ExpressionToMango`**: constants are serialized via `JsonConvert.SerializeObject` (JSON-escaped) and keys are reflected member names — no Mango/query injection.
- **Tunnel `MethodRouter` / `ChunkMethodClient`**: dispatch only to a pre-registered call table and deserialize into fixed parameter types — no arbitrary type/method instantiation. HTTP tunnel forwarding to a caller-supplied URI is the by-design function of an authenticated tunnel peer, not an open SSRF.
- **`DotHttpFileParser`** file I/O: sanitized with `Path.GetFileName` before `Path.Combine` — no path traversal.
- **Azure modules** (KeyVault / IoT / CosmosDb / EventHubs / IoT.Edge): no certificate-validation bypass, hardcoded secrets, secret logging, or unsafe deserialization observed.
- **Cryptography**: only `SHA256` in production code (`ByteArrayEx.cs`); `Random` / `Random.Shared` uses are non-security (backoff jitter) or test-only. No hardcoded secrets found in production source.
