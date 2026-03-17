# Milestones

## v1.0 MVP — ✅ SHIPPED 2026-03-17

**Phases:** 1–4 | **Plans:** 20 | **Tests:** 108 backend, 60 frontend

**Delivered:** Full MVP of the RP0 milsim planning platform — auth, roster import, hierarchy builder, briefings, maps, notifications, and mobile player experience — deployed to production on Azure.

**Key accomplishments:**
1. Secure JWT auth with email/password, magic link, and 5-role RBAC (Phase 1)
2. Event management and two-phase CSV roster import with per-row validation (Phase 2)
3. Platoon/squad hierarchy builder with bulk assign and callsign precedence rule (Phase 2)
4. Briefing content (markdown + attachments), map resources (R2 private storage), and async notification blasts via Resend (Phase 3)
5. Mobile-first player dashboard with assignment view, roster, documents, and change requests (Phase 4)
6. Production deployment: Azure Container Apps + Static Web Apps + Neon + Cloudflare R2 (~$1–3/mo)

**Known gaps at archive:**
- RCHG-01..05 and PLAY-01..06 checkboxes in REQUIREMENTS.md were not updated to reflect Phase 4 completion. Features were shipped; traceability table showed "Pending" as a tracking gap only.

**Archive:**
- Roadmap: `.planning/milestones/v1.0-ROADMAP.md`
- Requirements: `.planning/milestones/v1.0-REQUIREMENTS.md`
