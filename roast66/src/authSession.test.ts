import { beforeEach, describe, expect, it } from "vitest";
import { clearAdminSession, getAdminToken, setAdminSession } from "./authSession";

const validToken = "x.eyJleHAiOjQxMDI0NDQ4MDB9.x";
const expiredToken = "x.eyJleHAiOjF9.x";

describe("authSession", () => {
  beforeEach(() => localStorage.clear());

  it("returns a token whose expiry is still in the future", () => {
    setAdminSession(validToken);
    expect(getAdminToken()).toBe(validToken);
  });

  it("clears expired and malformed tokens", () => {
    localStorage.setItem("token", expiredToken);
    expect(getAdminToken()).toBeNull();
    expect(localStorage.getItem("token")).toBeNull();

    localStorage.setItem("token", "not-a-jwt");
    expect(getAdminToken()).toBeNull();
    expect(localStorage.getItem("token")).toBeNull();
  });

  it("clears an active session on logout", () => {
    setAdminSession(validToken);
    clearAdminSession();
    expect(getAdminToken()).toBeNull();
  });
});
