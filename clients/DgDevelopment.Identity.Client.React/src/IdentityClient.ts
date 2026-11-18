import type { OidcOptions, TokenResponse, UserInfo } from "./types";

/**
 * Scaffold only - the real OIDC/PKCE authorization-code flow, token storage and refresh are not
 * implemented yet (tracked as a follow-up milestone, see CONTEXT.md). This exists so the package
 * has a real public surface to build against instead of an empty src/ directory.
 */
export class IdentityClient {
  private readonly options: OidcOptions;

  constructor(options: OidcOptions) {
    this.options = options;
  }

  login(): never {
    throw new Error("IdentityClient.login is not implemented yet.");
  }

  logout(): never {
    throw new Error("IdentityClient.logout is not implemented yet.");
  }

  getAccessToken(): never {
    throw new Error("IdentityClient.getAccessToken is not implemented yet.");
  }

  getUserInfo(): never {
    throw new Error("IdentityClient.getUserInfo is not implemented yet.");
  }
}

export type { OidcOptions, TokenResponse, UserInfo };
