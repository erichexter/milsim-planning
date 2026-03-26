---
focus: quality
generated: 2026-03-25
---

# Coding Conventions

## Summary

The project is a full-stack application with two codebases: a React/TypeScript frontend (`web/`) and a .NET 10 C# backend (`milsim-platform/`). Each follows its own language-idiomatic conventions. The frontend enforces strict TypeScript compilation and uses ESLint with react-hooks and react-refresh plugins. No Prettier config is present — formatting is not auto-enforced, but the actual files show consistent 2-space indentation and single-quotes (frontend) or PascalCase/C# standard formatting (backend).

---

## Frontend (web/)

### Tooling

- **Linter:** ESLint v9 (flat config) — `web/eslint.config.js`
  - Extends: `@eslint/js` recommended, `typescript-eslint` recommended, `eslint-plugin-react-hooks` flat recommended, `eslint-plugin-react-refresh` vite preset
  - Target files: `**/*.{ts,tsx}` only
  - Ignored: `dist/`
- **TypeScript:** Strict mode — `web/tsconfig.app.json`
  - `strict: true`, `noUnusedLocals: true`, `noUnusedParameters: true`, `noFallthroughCasesInSwitch: true`
  - `erasableSyntaxOnly: true` (no decorators/namespaces), `noUncheckedSideEffectImports: true`
  - Target: `ES2023`, module: `ESNext`, moduleResolution: `bundler`
- **Formatter:** No Prettier config detected. Use 2-space indentation and single-quote strings (consistent with all existing source files).

### Naming Patterns

**Files:**
- Pages: `PascalCase` with descriptive suffix — `EventList.tsx`, `NotificationBlastPage.tsx`, `MagicLinkConfirmPage.tsx`
- Hooks: `camelCase` with `use` prefix — `useAuth.ts`, `useTheme.ts`
- Library utilities: `camelCase` — `api.ts`, `auth.ts`, `utils.ts`
- UI components (shadcn/ui): `camelCase` filename, named export — `badge.tsx`, `button.tsx`, `dialog.tsx`
- Test files: `PascalCase` matching the component, with `.test.tsx` or `.test.ts` suffix

**Functions/Variables:**
- React components: `PascalCase` named function exports — `export function EventList() { ... }`
- Hooks: `camelCase` with `use` prefix — `export function useAuth() { ... }`
- Utility functions: `camelCase` — `export function cn(...) { ... }`, `export function getToken() { ... }`
- Local variables: `camelCase` — `const queryClient = ...`, `const [open, setOpen] = useState(...)`

**Types/Interfaces:**
- Interfaces: `PascalCase` with descriptive name — `interface AuthUser`, `interface EventDto`, `interface Props`
- Inline prop interfaces in component files use `interface Props { ... }` (no component name prefix)
- Type aliases: `PascalCase` — `type ClassValue`

### Import Organization

No enforced order, but consistent pattern observed:

```typescript
// 1. External libraries
import { useState, useCallback } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Link } from 'react-router';

// 2. Internal modules via @ alias or relative path
import { api } from '../../lib/api';
import { CreateEventDialog } from './CreateEventDialog';
import { Badge } from '../../components/ui/badge';
```

**Path Alias:** `@/*` maps to `./src/*` — use for deep imports across directory boundaries. Short relative paths (`../`, `./`) used within the same feature area.

### Component Design

- Named function exports (not `export default`) — `export function EventList() { ... }`
- Props interfaces defined inline in the same file as the component
- Tailwind CSS utility classes for all styling — no CSS modules or styled-components
- `cn()` utility from `src/lib/utils.ts` for conditional class merging (clsx + tailwind-merge)
- shadcn/ui components in `src/components/ui/` — wrap Radix UI primitives, use `cva` for variants

### State & Data Fetching

- Server state: `@tanstack/react-query` with `useQuery` / `useMutation`
- Query keys are string arrays: `queryKey: ['events']`, `queryKey: ['event', id]`
- `QueryClient` is created with `{ defaultOptions: { queries: { retry: false } } }` in tests
- All API calls go through `src/lib/api.ts` — a typed wrapper around `fetch`
- Auth state: custom `useAuth` hook reading from `localStorage` via `src/lib/auth.ts`

### Error Handling

- API errors: `api.ts` throws `Error` objects with a `.status` property for non-2xx responses
- 401 responses redirect to `/auth/login` automatically inside `api.ts`
- Component-level errors: caught in `onError` callbacks of `useMutation`
- No global React error boundary detected

### Comments

- Inline comments explain non-obvious domain logic or requirement traceability (e.g., `// AUTH-04 session persistence`, `// EVNT-06`)
- No JSDoc/TSDoc on React components or utility functions — types carry the documentation burden

---

## Backend (milsim-platform/)

### Tooling

- **Language:** C# with `<Nullable>enable</Nullable>` and `<ImplicitUsings>enable</ImplicitUsings>` in all project files
- **Target Framework:** `net10.0`
- **Formatting:** C# standard formatting — PascalCase types/methods, `_camelCase` private fields
- No additional linter or formatter config files detected

### Naming Patterns

**Files:**
- Controllers: `PascalCase` + `Controller` suffix — `EventsController.cs`, `AuthController.cs`
- Services: `PascalCase` + `Service` suffix — `EventService.cs`, `AuthService.cs`
- Models/DTOs: `PascalCase` + descriptive suffix — `EventDto.cs`, `CreateEventRequest.cs`, `UpdateEventRequest.cs`
- Test classes: `PascalCase` + `Tests` suffix — `EventTests.cs`, `AuthorizationTests.cs`

**Types:**
- Classes/Records: `PascalCase`
- Interfaces: `I` prefix + `PascalCase` — `IEmailService`
- Private fields: `_camelCase` — `private readonly EventService _eventService;`
- Method parameters: `camelCase`

### Controller Design

- Inherit from `ControllerBase`, decorated with `[ApiController]` and `[Route("api/...")]`
- Route template: `api/{resource}` for top-level, `api/events/{id:guid}/{sub-resource}` for nested
- `[Authorize]` on the controller class, `[Authorize(Policy = "...")]` on specific actions
- `[AllowAnonymous]` overrides for public endpoints
- Return `ActionResult<T>` for typed responses
- Error pattern: catch typed exceptions and return appropriate HTTP status:
  ```csharp
  catch (KeyNotFoundException) { return NotFound(); }
  catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
  catch (ForbiddenException) { return Forbid(); }
  ```

### Service Design

- Services injected via constructor into controllers
- Service methods are `async Task<T>` for data operations
- Domain logic lives in services, not controllers

### Inline Comments

- Requirement references as inline comments on action methods: `// EVNT-01: Create event`, `// EVNT-03: List events`
- XML summary `<summary>` on test classes to describe what they cover

---

## Shared Conventions (Both Codebases)

- Requirement IDs (e.g., `EVNT-01`, `AUTH-04`, `AUTHZ-06`) appear in both code comments and test names — use these identifiers when referencing features
- REST-style naming: plural nouns for resource collections (`/api/events`, `/api/platoons`)
- Date strings: ISO 8601 `YYYY-MM-DD` for date-only fields; ISO 8601 datetime with `Z` suffix for timestamps
- UUIDs as string IDs on the frontend (`string`), `Guid` on the backend
