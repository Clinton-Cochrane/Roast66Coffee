let requestFulfilled;
let responseRejected;

jest.mock("axios", () => ({
  create: jest.fn(() => ({
    interceptors: {
      request: {
        use: jest.fn((onFulfilled) => {
          requestFulfilled = onFulfilled;
        }),
      },
      response: {
        use: jest.fn((_, onRejected) => {
          responseRejected = onRejected;
        }),
      },
    },
  })),
}));

describe("axiosConfig interceptors", () => {
  beforeEach(() => {
    jest.resetModules();
    localStorage.clear();
    requestFulfilled = undefined;
    responseRejected = undefined;
  });

  it("adds bearer token for protected requests", async () => {
    localStorage.setItem("token", "jwt-token");
    await import("./axiosConfig");

    const config = { method: "get", url: "/admin/orders", headers: {} };
    const result = requestFulfilled(config);
    expect(result.headers.Authorization).toBe("Bearer jwt-token");
  });

  it("does not add token for public order POST requests", async () => {
    localStorage.setItem("token", "jwt-token");
    await import("./axiosConfig");

    const config = { method: "post", url: "/order", headers: {} };
    const result = requestFulfilled(config);
    expect(result.headers.Authorization).toBeUndefined();
  });

  it("clears token and redirects cash users on 401", async () => {
    localStorage.setItem("token", "jwt-token");
    await import("./axiosConfig");

    const assignSpy = jest.fn();
    Object.defineProperty(window, "location", {
      configurable: true,
      value: {
        pathname: "/cash/orders",
        assign: assignSpy,
      },
    });

    await expect(responseRejected({ response: { status: 401 } })).rejects.toEqual({
      response: { status: 401 },
    });
    expect(localStorage.getItem("token")).toBeNull();
    expect(assignSpy).toHaveBeenCalledWith("/cash");
  });
});
