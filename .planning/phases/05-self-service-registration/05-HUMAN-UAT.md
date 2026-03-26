---
status: partial
phase: 05-self-service-registration
source: [05-VERIFICATION.md]
started: 2026-03-26T18:35:00Z
updated: 2026-03-26T18:35:00Z
---

## Current Test

[awaiting human testing]

## Tests

### 1. "Create an account" link visible on login page (AC-8)
expected: "Don't have an account? Create one" link visible in CardFooter at /auth/login; clicking navigates to /auth/register
result: [pending]

### 2. Registration form renders all 4 fields (AC-6)
expected: Card page at /auth/register shows Display Name, Email, Password, Confirm Password inputs + "Create Account" button
result: [pending]

### 3. Password mismatch shows client-side error without API call (AC-10)
expected: "Passwords do not match" error under Confirm Password; DevTools Network shows no POST to /api/auth/register
result: [pending]

### 4. Successful registration redirects to /dashboard (AC-7)
expected: Valid submission navigates browser to /dashboard with user authenticated
result: [pending]

### 5. Authenticated users redirected away from /auth/register (AC-9)
expected: Logged-in user navigating to /auth/register is immediately redirected to /dashboard
result: [pending]

## Summary

total: 5
passed: 0
issues: 0
pending: 5
skipped: 0
blocked: 0

## Gaps
