import { IdentityClient } from "./IdentityClient";
import type { OidcOptions } from "./types";

const options: OidcOptions = {
  authority: "https://identity.example.test",
  clientId: "test-client",
  redirectUri: "https://app.example.test/callback",
};

describe("IdentityClient (scaffold)", () => {
  it("every method throws, flagging it as not implemented yet rather than silently no-op'ing", () => {
    const client = new IdentityClient(options);

    expect(() => client.login()).toThrow(/not implemented yet/);
    expect(() => client.logout()).toThrow(/not implemented yet/);
    expect(() => client.getAccessToken()).toThrow(/not implemented yet/);
    expect(() => client.getUserInfo()).toThrow(/not implemented yet/);
  });
});
