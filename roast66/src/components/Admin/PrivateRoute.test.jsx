import React from "react";
import { render, screen } from "@testing-library/react";
import { MemoryRouter, Routes, Route } from "react-router-dom";
import PrivateRoute from "./PrivateRoute";

const TestChild = () => <div>Admin Content</div>;

function renderWithRouter(initialEntries = ["/admin"], hasToken = true) {
  const getItem = jest.fn((key) => (key === "token" && hasToken ? "fake-token" : null));
  Object.defineProperty(window, "localStorage", { value: { getItem }, writable: true });

  return render(
    <MemoryRouter initialEntries={initialEntries}>
      <Routes>
        <Route
          path="/admin"
          element={
            <PrivateRoute>
              <TestChild />
            </PrivateRoute>
          }
        />
        <Route path="/admin-login" element={<div>Login Page</div>} />
      </Routes>
    </MemoryRouter>
  );
}

describe("PrivateRoute", () => {
  it("renders children when token exists", () => {
    renderWithRouter(["/admin"], true);
    expect(screen.getByText("Admin Content")).toBeInTheDocument();
  });

  it("redirects to admin-login when no token", () => {
    renderWithRouter(["/admin"], false);
    expect(screen.getByText("Login Page")).toBeInTheDocument();
    expect(screen.queryByText("Admin Content")).not.toBeInTheDocument();
  });
});
