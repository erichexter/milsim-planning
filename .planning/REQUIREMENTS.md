# Requirements: v1.1 Registration

**Milestone:** v1.1 Registration
**Status:** Active
**Source:** PRD-REGISTRATION.md

---

## Registration — Backend

- [ ] **REG-01**: User can register with displayName, email, and password via POST /api/auth/register and receive a JWT token
- [ ] **REG-02**: Registration fails with 400 if displayName is missing or empty
- [ ] **REG-03**: Registration fails with 400 if password is fewer than 6 characters
- [ ] **REG-04**: Registration fails with 409 if email is already registered
- [ ] **REG-05**: Self-registered user is assigned the faction_commander role immediately (no activation required)

## Registration — Frontend

- [ ] **REG-06**: /auth/register page renders with Display Name, Email, Password, and Confirm Password fields
- [ ] **REG-07**: Successful registration stores JWT and redirects user to /dashboard
- [ ] **REG-08**: Password mismatch shows client-side error without making an API call
- [ ] **REG-09**: Duplicate email (409) shows "An account with this email already exists"
- [ ] **REG-10**: API validation errors (400) display the specific message from the server

## Auth Integration

- [ ] **REG-11**: LoginPage shows "Don't have an account? Create one" link to /auth/register
- [ ] **REG-12**: Authenticated users visiting /auth/register are redirected to /dashboard

---

## Future Requirements

*(None identified — PRD scope is complete for this milestone)*

## Out of Scope

- Email verification — no confirmation required for MVP (PRD explicit exclusion)
- Invite flow changes — existing invite system untouched
- Role selection during registration — all self-registered users get faction_commander

---

## Traceability

| REQ-ID | Phase | Plan | Status |
|--------|-------|------|--------|
| REG-01 | — | — | Pending |
| REG-02 | — | — | Pending |
| REG-03 | — | — | Pending |
| REG-04 | — | — | Pending |
| REG-05 | — | — | Pending |
| REG-06 | — | — | Pending |
| REG-07 | — | — | Pending |
| REG-08 | — | — | Pending |
| REG-09 | — | — | Pending |
| REG-10 | — | — | Pending |
| REG-11 | — | — | Pending |
| REG-12 | — | — | Pending |
