/**
 * PLAY-06: Callsign is displayed prominently in all roster views.
 *
 * Tests the real PlayerOverviewTab (not mocked) to verify callsign
 * rendering meets the requirement.
 */
import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { http, HttpResponse } from "msw";
import { server } from "../mocks/server";
import { PlayerOverviewTab } from "../components/player/PlayerOverviewTab";

function renderOverviewTab(eventId = "evt-123") {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter>
        <PlayerOverviewTab eventId={eventId} onNavigate={vi.fn()} />
      </MemoryRouter>
    </QueryClientProvider>
  );
}

describe("PLAY-06 callsign display", () => {
  beforeEach(() => {
    server.use(
      http.get("/api/events", () =>
        HttpResponse.json([
          {
            id: "evt-123",
            name: "Op Thunder",
            status: "Published",
            location: null,
            description: null,
            startDate: null,
            endDate: null,
          },
        ])
      ),
      http.get("/api/events/:id/my-assignment", () =>
        HttpResponse.json({
          id: "player-1",
          name: "John Smith",
          callsign: "VIPER",
          teamAffiliation: "Bravo",
          role: "player",
          platoon: { id: "p1", name: "1st Platoon" },
          squad: { id: "s1", name: "Alpha Squad" },
          isAssigned: true,
        })
      ),
      http.get("/api/events/:id/roster-change-requests/mine", () =>
        HttpResponse.json(null, { status: 204 })
      ),
      http.get("/api/events/:id/info-sections", () => HttpResponse.json([])),
      http.get("/api/events/:id/map-resources", () => HttpResponse.json([])),
    );
  });

  it("callsign_displays_prominently_in_player_overview", async () => {
    renderOverviewTab();
    const callsign = await screen.findByText(/VIPER/);
    expect(callsign).toBeInTheDocument();
    // Rendered in monospace font
    expect(callsign.className).toMatch(/font-mono/);
    // Rendered at a large size (text-2xl or similar)
    expect(callsign.className).toMatch(/text-\d/);
  });

  it("unassigned_player_sees_unassigned_notice", async () => {
    server.use(
      http.get("/api/events/:id/my-assignment", () =>
        HttpResponse.json({
          id: "",
          name: "",
          callsign: null,
          teamAffiliation: null,
          role: null,
          platoon: null,
          squad: null,
          isAssigned: false,
        })
      )
    );
    renderOverviewTab();
    const el = await screen.findByText(/Unassigned/);
    expect(el).toBeInTheDocument();
  });
});
