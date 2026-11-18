import { isTokenExpired, type TokenResponse } from "./types";

function tokenIssuedSecondsAgo(secondsAgo: number, expiresIn: number): TokenResponse {
  return {
    access_token: "a",
    token_type: "Bearer",
    expires_in: expiresIn,
    scope: "openid",
    issued_at: Date.now() - secondsAgo * 1000,
  };
}

describe("isTokenExpired", () => {
  it("is false well within the token's lifetime", () => {
    expect(isTokenExpired(tokenIssuedSecondsAgo(10, 3600))).toBe(false);
  });

  it("is true once past expiry", () => {
    expect(isTokenExpired(tokenIssuedSecondsAgo(3700, 3600))).toBe(true);
  });

  it("is true inside the 30-second refresh margin, even if not technically expired yet", () => {
    expect(isTokenExpired(tokenIssuedSecondsAgo(3590, 3600))).toBe(true);
  });
});
