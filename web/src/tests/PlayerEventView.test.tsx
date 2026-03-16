import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { http, HttpResponse } from "msw";
import { server } from "../mocks/server";
import { PlayerEventPage } from "../pages/events/PlayerEventPage";

// Mock all child tabs so tests focus on PlayerEventPage navigation logic
vi.mock("../components/player/MyAssignmentTab", () => ({
  MyAssignmentTab: ({ eventId }: { eventId: string }) => (
    <div data-testid="my-assignment-tab">Assignment tab for {eventId}</div>
  ),
}));

// Mock PlayerOverviewTab — it makes 4+ API calls; not the subject of these tests
vi.mock("../components/player/PlayerOverviewTab", () => ({
  PlayerOverviewTab: ({ eventId }: { eventId: string }) => (
    <div data-testid="player-overview-tab">Overview for {eventId}</div>
  ),
}));

vi.mock("../pages/roster/RosterView", () => ({
  RosterView: () => <div data-testid="roster-view">Roster View</div>,
}));

vi.mock("../pages/events/BriefingPage", () => ({
  BriefingPage: () => <div data-testid="briefing-page">Briefing</div>,
}));

vi.mock("../pages/events/MapResourcesPage", () => ({
  MapResourcesPage: () => <div data-testid="map-resources-page">Maps</div>,
}));

function renderPlayerEventPage(eventId = "test-event-123") {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={[`/events/${eventId}/player`]}>
        <Routes>
          <Route path="/events/:id/player" element={<PlayerEventPage />} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>
  );
}

describe("PlayerEventPage", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    // PlayerEventPage queries roster-change-requests/mine directly
    server.use(
      http.get("/api/events/:id/roster-change-requests/mine", () =>
        HttpResponse.json(null, { status: 204 })
      )
    );
  });

  it("renders_overview_tab_by_default", () => {
    renderPlayerEventPage();
    expect(screen.getByTestId("player-overview-tab")).toBeInTheDocument();
  });

  it("renders_bottomTabBar_on_mobile", () => {
    renderPlayerEventPage();
    // Bottom nav is rendered as a <nav> with md:hidden class (4 tab buttons)
    const tabButtons = screen.getAllByRole("button");
    // Should have at least 4 buttons in the bottom nav (My Assignment, Roster, Briefing, Maps)
    // Desktop nav + mobile nav = 8 buttons total
    expect(tabButtons.length).toBeGreaterThanOrEqual(4);
  });

  it("tabBar_buttons_have_minimum_44px_height", () => {
    renderPlayerEventPage();
    const allButtons = screen.getAllByRole("button");
    // At least one set of buttons should have min-h-[56px] class (bottom tab bar)
    const hasMinHeight = allButtons.some((btn) =>
      btn.className.includes("min-h-[56px]")
    );
    expect(hasMinHeight).toBe(true);
  });

  it("switching_tabs_renders_correct_content", () => {
    renderPlayerEventPage();
    // Overview tab should be active by default
    expect(screen.getByTestId("player-overview-tab")).toBeInTheDocument();
    // Click "Roster" tab button (find one in mobile nav which has min-h-[56px])
    const allButtons = screen.getAllByRole("button");
    const mobileRosterBtn = allButtons.find(
      (btn) => btn.textContent === "Roster" && btn.className.includes("min-h-[56px]")
    );
    if (mobileRosterBtn) {
      fireEvent.click(mobileRosterBtn);
      expect(screen.getByTestId("roster-view")).toBeInTheDocument();
    } else {
      // Fallback: click any Roster button
      const rosterBtn = allButtons.find((btn) => btn.textContent === "Roster");
      if (rosterBtn) fireEvent.click(rosterBtn);
      expect(screen.getByTestId("roster-view")).toBeInTheDocument();
    }
  });

  it("overview_tab_renders_for_event", () => {
    // PlayerOverviewTab is mocked; verify it receives the eventId
    renderPlayerEventPage();
    expect(screen.getByTestId("player-overview-tab")).toBeInTheDocument();
    expect(screen.getByText(/Overview for test-event-123/)).toBeInTheDocument();
  });
});
