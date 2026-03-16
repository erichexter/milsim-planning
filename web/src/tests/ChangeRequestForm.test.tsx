import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { ChangeRequestForm } from "../components/player/ChangeRequestForm";
import { PendingRequestCard } from "../components/player/PendingRequestCard";
import { ChangeRequestsPage } from "../pages/events/ChangeRequestsPage";
import { MemoryRouter, Route, Routes } from "react-router";

// Mock api module
vi.mock("../lib/api", () => ({
  api: {
    get: vi.fn(),
    post: vi.fn().mockResolvedValue({}),
    delete: vi.fn().mockResolvedValue(undefined),
    getRoster: vi.fn().mockResolvedValue({ platoons: [], unassignedPlayers: [] }),
    getEvents: vi.fn().mockResolvedValue([]),
  },
}));

function makeQueryClient() {
  return new QueryClient({ defaultOptions: { queries: { retry: false } } });
}

function wrapWithProviders(ui: React.ReactElement, eventId = "event-1") {
  return render(
    <QueryClientProvider client={makeQueryClient()}>
      <MemoryRouter initialEntries={[`/events/${eventId}/change-requests`]}>
        <Routes>
          <Route path="/events/:id/change-requests" element={ui} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>
  );
}

describe("ChangeRequestForm", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("renders_changeRequest_form_when_no_pending_request", () => {
    render(
      <QueryClientProvider client={makeQueryClient()}>
        <ChangeRequestForm eventId="event-1" />
      </QueryClientProvider>
    );
    expect(screen.getByPlaceholderText("Describe your request...")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /submit request/i })).toBeInTheDocument();
  });

  it("renders_pendingRequest_card_when_request_is_pending", () => {
    const pendingRequest = {
      id: "req-1",
      note: "Move me to Alpha",
      status: "Pending",
      commanderNote: null,
      createdAt: new Date().toISOString(),
    };
    render(
      <QueryClientProvider client={makeQueryClient()}>
        <PendingRequestCard eventId="event-1" request={pendingRequest} />
      </QueryClientProvider>
    );
    expect(screen.getByText("Pending")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /cancel request/i })).toBeInTheDocument();
    expect(screen.queryByPlaceholderText("Describe your request...")).not.toBeInTheDocument();
  });

  it("submit_button_calls_submit_mutation", async () => {
    const { api } = await import("../lib/api");
    render(
      <QueryClientProvider client={makeQueryClient()}>
        <ChangeRequestForm eventId="event-1" />
      </QueryClientProvider>
    );
    const textarea = screen.getByPlaceholderText("Describe your request...");
    fireEvent.change(textarea, { target: { value: "Please move me" } });
    const submitBtn = screen.getByRole("button", { name: /submit request/i });
    fireEvent.click(submitBtn);
    await waitFor(() => {
      expect(api.post).toHaveBeenCalledWith(
        "/events/event-1/roster-change-requests",
        { note: "Please move me" }
      );
    });
  });

  it("cancel_button_calls_cancel_mutation", async () => {
    const { api } = await import("../lib/api");
    const pendingRequest = {
      id: "req-42",
      note: "Move me to Alpha",
      status: "Pending",
      commanderNote: null,
      createdAt: new Date().toISOString(),
    };
    render(
      <QueryClientProvider client={makeQueryClient()}>
        <PendingRequestCard eventId="event-1" request={pendingRequest} />
      </QueryClientProvider>
    );
    const cancelBtn = screen.getByRole("button", { name: /cancel request/i });
    fireEvent.click(cancelBtn);
    await waitFor(() => {
      expect(api.delete).toHaveBeenCalledWith(
        "/events/event-1/roster-change-requests/req-42"
      );
    });
  });

  it("commander_approve_dialog_shows_platoon_squad_dropdowns", async () => {
    const { api } = await import("../lib/api");
    vi.mocked(api.get).mockResolvedValue([
      {
        id: "req-1",
        note: "Move me",
        createdAt: new Date().toISOString(),
        player: { name: "Test Player", callsign: "WOLF-01", platoonId: null, squadId: null },
      },
    ]);
    vi.mocked(api.getRoster).mockResolvedValue({
      platoons: [
        { id: "p-1", name: "Alpha Platoon", isCommandElement: false, hqPlayers: [], squads: [{ id: "s-1", name: "Alpha-1", players: [] }] },
      ],
      unassignedPlayers: [],
    });

    wrapWithProviders(<ChangeRequestsPage />);

    // Wait for data to load
    await waitFor(() => {
      expect(screen.getByText("Approve")).toBeInTheDocument();
    });

    // Click Approve button
    fireEvent.click(screen.getByText("Approve"));

    // Dialog should now be open with platoon/squad selects
    await waitFor(() => {
      expect(screen.getByText("Approve Change Request")).toBeInTheDocument();
      expect(screen.getByText("Platoon")).toBeInTheDocument();
      expect(screen.getByText("Squad")).toBeInTheDocument();
    });
  });
});
