# Changelog

## [0.2.0](https://github.com/dgastaldo/DgDevelopment.Identity/compare/client-blazor-v0.1.0...client-blazor-v0.2.0) (2026-09-26)


### Features

* add Roles/Groups admin UI with permission-based nav gating ([69bcdf1](https://github.com/dgastaldo/DgDevelopment.Identity/commit/69bcdf1263bda044983e2bae7e4b3f7a2e14d609))
* AdminUi auth feedback (username + avatar + top bar) ([1254a32](https://github.com/dgastaldo/DgDevelopment.Identity/commit/1254a32de7a018477866bce7c293fbc1ed75a2b8))
* implement Client.Blazor SDK with AuthStateProvider, OIDC client, token storage ([0f0ca3a](https://github.com/dgastaldo/DgDevelopment.Identity/commit/0f0ca3a5fab7b7c91a9e6dfd3219e043fff189a8))
* multi-tenant foundation (Tenant model, tenant-aware auth/OAuth/Users API) ([9b67a49](https://github.com/dgastaldo/DgDevelopment.Identity/commit/9b67a4984a098e7da6dd6490422678469b972601))
* platform-scope Roles/Permissions and add tenant provisioning service ([55d80ac](https://github.com/dgastaldo/DgDevelopment.Identity/commit/55d80ac8802d114e547886a03c1b9ebf293b4b8b))
* session/refresh-token revocation and real-time push on password change ([67178dd](https://github.com/dgastaldo/DgDevelopment.Identity/commit/67178ddaf1cacd2a78ab1855518e4f6c97ddaeb7))
* show authenticated user in AdminUi top bar ([fd859a4](https://github.com/dgastaldo/DgDevelopment.Identity/commit/fd859a4abdd7c07ed97797ca465727805c582b46))
* thread tenant slug through the login redirect ([c6dd41e](https://github.com/dgastaldo/DgDevelopment.Identity/commit/c6dd41e360aba4bb903b22c1f25ad4189e614453))
* wire MAUI and IdentityPlatform clients into the session-revocation push ([38998b8](https://github.com/dgastaldo/DgDevelopment.Identity/commit/38998b84f21d4036c95e73859a667c035bb23eaf))


### Bug Fixes

* align OIDC client DTOs with IDP snake_case responses ([#23](https://github.com/dgastaldo/DgDevelopment.Identity/issues/23)) ([d502bae](https://github.com/dgastaldo/DgDevelopment.Identity/commit/d502bae8abeaa33fbdfae14e338994cc377274e9))
* CA1725, CA1819, CA1861, CA1305 (Guid ToString) — parameter names, array properties, static arrays ([16585e8](https://github.com/dgastaldo/DgDevelopment.Identity/commit/16585e8e229f06c6dbbb586a0e16347db2f561d5))
* complete OIDC login round trip end-to-end ([3529c41](https://github.com/dgastaldo/DgDevelopment.Identity/commit/3529c41e956dc1a8bbf85532efe1c0fb0f47a16c))
* OAuth implementation hardening ([21a94ff](https://github.com/dgastaldo/DgDevelopment.Identity/commit/21a94ff2ecbad7d1da8e1f6d70b7e4d748cc02f4))
* preserve OAuth query params in login redirect via GetEncodedUrl ([2d31726](https://github.com/dgastaldo/DgDevelopment.Identity/commit/2d31726c019d37e1589bb48fa50162a72943f1f2))
* use AdminUi base URL for OIDC redirect handling ([84a5502](https://github.com/dgastaldo/DgDevelopment.Identity/commit/84a5502ca9012ad0ffb450644dd69ec2807c7b36))
* validate parameters in services and repositories ([2ac9da7](https://github.com/dgastaldo/DgDevelopment.Identity/commit/2ac9da7169b018107bb34660c6fb9e2980c9f099))
