import { render, screen } from "@testing-library/react";
import RouteList from "@/components/routes/RouteList";
import type { ProxyHost } from "@/types";

const makeRoute = (id: string): ProxyHost => ({
  id,
  domainNames: [`${id}.example.com`],
  destination: `http://backend-${id}:8080`,
  isEnabled: true,
});

describe("RouteList", () => {
  it("renders a RouteCard for each route in the list", () => {
    const routes = [makeRoute("a"), makeRoute("b"), makeRoute("c")];
    render(
      <RouteList
        routes={routes}
        total={3}
        page={1}
        pageSize={50}
        isAdmin={false}
        onDelete={() => {}}
      />
    );
    expect(screen.getAllByText("a.example.com").length).toBeGreaterThan(0);
    expect(screen.getAllByText("b.example.com").length).toBeGreaterThan(0);
    expect(screen.getAllByText("c.example.com").length).toBeGreaterThan(0);
  });

  it("shows empty state message when list is empty", () => {
    render(
      <RouteList
        routes={[]}
        total={0}
        page={1}
        pageSize={50}
        isAdmin={false}
        onDelete={() => {}}
      />
    );
    expect(screen.getByText(/no routes/i)).toBeInTheDocument();
  });

  it("shows 'Add Route' CTA in empty state", () => {
    render(
      <RouteList
        routes={[]}
        total={0}
        page={1}
        pageSize={50}
        isAdmin={true}
        onDelete={() => {}}
      />
    );
    expect(screen.getByRole("link", { name: /add route/i })).toBeInTheDocument();
  });

  it("shows pagination controls when total > pageSize", () => {
    const routes = Array.from({ length: 10 }, (_, i) => makeRoute(`r${i}`));
    render(
      <RouteList
        routes={routes}
        total={100}
        page={1}
        pageSize={10}
        isAdmin={false}
        onDelete={() => {}}
      />
    );
    expect(screen.getByRole("navigation", { name: /pagination/i })).toBeInTheDocument();
  });

  it("does not show pagination when total <= pageSize", () => {
    const routes = [makeRoute("only")];
    render(
      <RouteList
        routes={routes}
        total={1}
        page={1}
        pageSize={50}
        isAdmin={false}
        onDelete={() => {}}
      />
    );
    expect(screen.queryByRole("navigation", { name: /pagination/i })).not.toBeInTheDocument();
  });
});
