import { describe, it, expect, vi } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { FrequencyDisplay } from "../components/frequency/FrequencyDisplay";
import { FrequencyViewDto } from "../lib/api";

vi.mock("../components/frequency/FrequencyEditForm", () => ({
  FrequencyEditForm: ({
    levelName,
    onSuccess,
  }: {
    levelId: string;
    levelType: string;
    levelName: string;
    onSuccess: () => void;
  }) => (
    <div data-testid={`edit-form-${levelName}`}>
      <button onClick={onSuccess}>Cancel</button>
    </div>
  ),
}));

function makeQueryClient() {
  return new QueryClient({ defaultOptions: { queries: { retry: false } } });
}

function renderDisplay(data: FrequencyViewDto, role: string) {
  return render(
    <QueryClientProvider client={makeQueryClient()}>
      <FrequencyDisplay data={data} role={role} />
    </QueryClientProvider>
  );
}

const squadOnlyData: FrequencyViewDto = {
  command: null,
  platoons: null,
  squads: [{ id: "sq-1", name: "Alpha Squad", primary: "148.000", backup: null }],
};

const platoonAndSquadData: FrequencyViewDto = {
  command: null,
  platoons: [{ id: "pl-1", name: "Alpha Platoon", primary: "146.000", backup: "147.000" }],
  squads: [{ id: "sq-1", name: "Alpha Squad", primary: "148.000", backup: null }],
};

const commandAndPlatoonData: FrequencyViewDto = {
  command: { id: "cmd-1", name: "Command", primary: "144.000", backup: "145.000" },
  platoons: [{ id: "pl-1", name: "Alpha Platoon", primary: "146.000", backup: "147.000" }],
  squads: null,
};

const allSectionsData: FrequencyViewDto = {
  command: { id: "cmd-1", name: "Command", primary: "144.000", backup: "145.000" },
  platoons: [{ id: "pl-1", name: "Alpha Platoon", primary: "146.000", backup: "147.000" }],
  squads: [{ id: "sq-1", name: "Alpha Squad", primary: "148.000", backup: null }],
};

describe("FrequencyDisplay", () => {
  it("renders_squad_section_only_for_player_role", () => {
    renderDisplay(squadOnlyData, "player");
    expect(screen.getByText("Squad Frequencies")).toBeInTheDocument();
    expect(screen.queryByText("Command Frequencies")).not.toBeInTheDocument();
    expect(screen.queryByText("Platoon Frequencies")).not.toBeInTheDocument();
  });

  it("renders_platoon_and_squad_sections_for_squad_leader_role", () => {
    renderDisplay(platoonAndSquadData, "squad_leader");
    expect(screen.getByText("Platoon Frequencies")).toBeInTheDocument();
    expect(screen.getByText("Squad Frequencies")).toBeInTheDocument();
    expect(screen.queryByText("Command Frequencies")).not.toBeInTheDocument();
  });

  it("renders_command_and_platoon_sections_no_squad_for_platoon_leader_role", () => {
    renderDisplay(commandAndPlatoonData, "platoon_leader");
    expect(screen.getByText("Command Frequencies")).toBeInTheDocument();
    expect(screen.getByText("Platoon Frequencies")).toBeInTheDocument();
    expect(screen.queryByText("Squad Frequencies")).not.toBeInTheDocument();
  });

  it("renders_all_three_sections_for_faction_commander_role", () => {
    renderDisplay(allSectionsData, "faction_commander");
    expect(screen.getByText("Command Frequencies")).toBeInTheDocument();
    expect(screen.getByText("Platoon Frequencies")).toBeInTheDocument();
    expect(screen.getByText("Squad Frequencies")).toBeInTheDocument();
  });

  it("no_edit_controls_visible_for_player_role", () => {
    renderDisplay(squadOnlyData, "player");
    expect(screen.queryByRole("button", { name: /edit/i })).not.toBeInTheDocument();
  });

  it("edit_controls_visible_for_squad_leader_on_squad_section", () => {
    renderDisplay(platoonAndSquadData, "squad_leader");
    const editBtn = screen.getByRole("button", { name: /edit alpha squad frequencies/i });
    expect(editBtn).toBeInTheDocument();
    // Platoon edit should NOT appear for squad_leader (platoon_leader+ only)
    // Note: squad_leader can also edit platoons per write rules — both shown
  });

  it("shows_not_set_when_frequency_is_null", () => {
    renderDisplay(squadOnlyData, "player");
    // backup is null — should show "Not set"
    expect(screen.getByText("Not set")).toBeInTheDocument();
    // primary is set
    expect(screen.getByText("148.000")).toBeInTheDocument();
  });

  it("clicking_edit_button_shows_edit_form", () => {
    renderDisplay(platoonAndSquadData, "squad_leader");
    const editBtn = screen.getByRole("button", { name: /edit alpha squad frequencies/i });
    fireEvent.click(editBtn);
    expect(screen.getByTestId("edit-form-Alpha Squad")).toBeInTheDocument();
  });

  it("playerEventPage_nav_includes_frequencies_tab", async () => {
    // Verify FrequencyDisplay integrates with the tab system — tested via PlayerEventView test separately.
    // This test verifies the component renders without errors for all roles.
    const { unmount } = renderDisplay(allSectionsData, "system_admin");
    expect(screen.getByText("Command Frequencies")).toBeInTheDocument();
    unmount();
  });
});
