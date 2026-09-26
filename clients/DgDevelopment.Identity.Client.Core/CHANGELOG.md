# Changelog

## [0.2.0](https://github.com/dgastaldo/DgDevelopment.Identity/compare/client-core-v0.1.0...client-core-v0.2.0) (2026-09-26)


### Features

* add Clients/Platforms/Audit admin UI and user role/permission/group assignment ([66ce487](https://github.com/dgastaldo/DgDevelopment.Identity/commit/66ce48707c30262d3f4667cf9008400cf8264af1))
* add identity platform dashboard and user pages ([338fe60](https://github.com/dgastaldo/DgDevelopment.Identity/commit/338fe60e11f733e788637b2123d2eec7e6788fb4))
* add MAUI client with OIDC/PKCE auth and self-service MFA ([a91109b](https://github.com/dgastaldo/DgDevelopment.Identity/commit/a91109b58a7c1def86287c11a58f38691dd3bcdc))
* add Roles/Groups admin UI with permission-based nav gating ([69bcdf1](https://github.com/dgastaldo/DgDevelopment.Identity/commit/69bcdf1263bda044983e2bae7e4b3f7a2e14d609))
* add tenant switcher UI to IdentityPlatform dashboard ([fbce43c](https://github.com/dgastaldo/DgDevelopment.Identity/commit/fbce43cf77f3d20becb0dcea5689c0470702bfac))
* implement Client.Blazor SDK with AuthStateProvider, OIDC client, token storage ([0f0ca3a](https://github.com/dgastaldo/DgDevelopment.Identity/commit/0f0ca3a5fab7b7c91a9e6dfd3219e043fff189a8))
* multi-tenant foundation (Tenant model, tenant-aware auth/OAuth/Users API) ([9b67a49](https://github.com/dgastaldo/DgDevelopment.Identity/commit/9b67a4984a098e7da6dd6490422678469b972601))
* platform-scope Roles/Permissions and add tenant provisioning service ([55d80ac](https://github.com/dgastaldo/DgDevelopment.Identity/commit/55d80ac8802d114e547886a03c1b9ebf293b4b8b))
* platform-scope Roles/Permissions and add tenant provisioning service ([06ef707](https://github.com/dgastaldo/DgDevelopment.Identity/commit/06ef707662b3abb17eb83d64a0769927a4ff76c8))
* self-registration, self-service MFA, and MAUI client ([95acb0b](https://github.com/dgastaldo/DgDevelopment.Identity/commit/95acb0b78697b2c7e8503f38654252e18d808998))
* self-service MFA and profile pages ([330c665](https://github.com/dgastaldo/DgDevelopment.Identity/commit/330c6654a012cff1db13f884948e180cf6fb398f))
* self-service MFA and profile pages ([aee40a3](https://github.com/dgastaldo/DgDevelopment.Identity/commit/aee40a3d81452248bbad4ddd018bf26f82f472ad))
* session/refresh-token revocation and real-time push on password change ([67178dd](https://github.com/dgastaldo/DgDevelopment.Identity/commit/67178ddaf1cacd2a78ab1855518e4f6c97ddaeb7))
* tenant deactivate/activate + permission-catalog reconciliation ([2999b9f](https://github.com/dgastaldo/DgDevelopment.Identity/commit/2999b9f995fbb8f8dee17bec020b44c3ca82708a))
* tenant deactivate/activate + permission-catalog reconciliation ([919b60e](https://github.com/dgastaldo/DgDevelopment.Identity/commit/919b60e88ee1c06fd371301932b144c4c6ae5e69))
* wire MAUI and IdentityPlatform clients into the session-revocation push ([38998b8](https://github.com/dgastaldo/DgDevelopment.Identity/commit/38998b84f21d4036c95e73859a667c035bb23eaf))


### Bug Fixes

* align OIDC client DTOs with IDP snake_case responses ([#23](https://github.com/dgastaldo/DgDevelopment.Identity/issues/23)) ([d502bae](https://github.com/dgastaldo/DgDevelopment.Identity/commit/d502bae8abeaa33fbdfae14e338994cc377274e9))
* align OIDC client DTOs with snake_case token and userinfo responses ([7017d59](https://github.com/dgastaldo/DgDevelopment.Identity/commit/7017d594524bdb4c1060f13d8c4e36b3bd97bdcc))
* CA1725, CA1819, CA1861, CA1305 (Guid ToString) — parameter names, array properties, static arrays ([16585e8](https://github.com/dgastaldo/DgDevelopment.Identity/commit/16585e8e229f06c6dbbb586a0e16347db2f561d5))
* complete OIDC login round trip end-to-end ([3529c41](https://github.com/dgastaldo/DgDevelopment.Identity/commit/3529c41e956dc1a8bbf85532efe1c0fb0f47a16c))
* dispose Argon2id instances, suppress CA2000 false positives in HttpClient ([5e3669b](https://github.com/dgastaldo/DgDevelopment.Identity/commit/5e3669be5451306b82d738a10a8dfa08d42d5968))
* OAuth implementation hardening ([21a94ff](https://github.com/dgastaldo/DgDevelopment.Identity/commit/21a94ff2ecbad7d1da8e1f6d70b7e4d748cc02f4))
* preserve OAuth query params in login redirect via GetEncodedUrl ([2d31726](https://github.com/dgastaldo/DgDevelopment.Identity/commit/2d31726c019d37e1589bb48fa50162a72943f1f2))
* three pre-existing bugs blocking cross-origin auth, found while ([2afc067](https://github.com/dgastaldo/DgDevelopment.Identity/commit/2afc067c7a132e7fb8af539d1d65f2ca6c1c944c))
* use AdminUi base URL for OIDC redirect handling ([84a5502](https://github.com/dgastaldo/DgDevelopment.Identity/commit/84a5502ca9012ad0ffb450644dd69ec2807c7b36))
