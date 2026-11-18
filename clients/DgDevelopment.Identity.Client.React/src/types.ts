// Mirrors the shapes DgDevelopment.Identity.Client.Core's OidcOptions/TokenResponse/UserInfo use
// server-side, adapted for a browser SPA: no ClientSecret (a React app is a public OAuth client,
// using PKCE instead), and PublicAuthority is dropped since a browser-only client is never behind
// a server-to-server address its own requests need to resolve differently.

export interface OidcOptions {
  authority: string;
  clientId: string;
  redirectUri: string;
  postLogoutRedirectUri?: string;
  scopes?: string[];
}

export interface TokenResponse {
  access_token: string;
  token_type: string;
  expires_in: number;
  id_token?: string;
  refresh_token?: string;
  scope: string;
  /** Set client-side on receipt - the token response itself carries no timestamp. */
  issued_at: number;
}

export interface UserInfo {
  sub: string;
  name?: string;
  email?: string;
  email_verified?: boolean;
  permissions?: string[];
}

export function isTokenExpired(token: TokenResponse): boolean {
  return Date.now() >= (token.issued_at + (token.expires_in - 30) * 1000);
}
