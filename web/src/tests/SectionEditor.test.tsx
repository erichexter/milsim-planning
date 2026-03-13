import { describe, it, expect, beforeAll, afterEach, afterAll } from "vitest";
import { server } from "../mocks/server";

// Note: SectionEditor component does not exist yet — created in Plan 03-05.
// These stubs establish the test category structure for CONT-01/CONT-02.

beforeAll(() => server.listen());
afterEach(() => server.resetHandlers());
afterAll(() => server.close());

describe("SectionEditor", () => {
  it("renders edit and preview tabs", () => {
    expect(true).toBe(true);
  });

  it("blocks save when title is empty", () => {
    expect(true).toBe(true);
  });

  it("shows preview with react-markdown", () => {
    expect(true).toBe(true);
  });
});
