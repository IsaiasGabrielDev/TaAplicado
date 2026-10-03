# Product

<!-- impeccable:product-schema 1 -->

## Platform

web

## Stack
Desktop app on Photino.NET (a native Windows window with WebView2) hosting local HTML/CSS/JS, running in the same C# process as the automation engine (Playwright + ClaudeCodeBridge). No server, no Node. Chosen by the user.

## Users
Primary user: a job seeker (the app started with a Brazilian .NET/C# developer, and works for any field). Other people may use the same computer: each is a local "remetente" (no login, no password) picked from a menu, with fully separate data (profile, answers, ledger, Chrome/LinkedIn session). The user starts a run and goes off to do something else; on returning, the user wants to know what happened and to clear what's waiting on them. Technical, uses the tool frequently (daily runs), not a beginner.

## Product Purpose
Automate LinkedIn Easy Apply applications (recommended jobs and keyword searches). LinkedIn already prefills most fields; the product solves the new questions: Claude answers from the user's profile, and only uncertain questions go to the user. Success = many applications sent with correct answers, with minimal intervention from the user.

## Positioning
It isn't a blind job bot: every answer comes from a profile the user writes and from a bank of answers the user audits. Claude answers in the user's name, and the user stays in control of what is uncertain.

## Operating Context
- A run can last hours (random 20–60 s between applications, default cap of 25).
- A separate Chrome window (Playwright, persistent profile) is visible while the automation runs; the app is the control panel, not the browser.
- Uncertain questions pile up while the user is away (confirmed use: "I leave it running and come back later").
- Data on disk: `answers.json` (question → answer cache), `applied.json` (job id → status), `agent/CLAUDE.md` (rules + user profile).

## Capabilities and Constraints
- v1 screens (confirmed): live run (start/stop, recommended vs search, dry-run, max, log), uncertain questions, answer bank (view/edit/delete), application history, and a screen with the user's data/information (the profile Claude uses).
- Application statuses: applied, dry-run, skip (no Easy Apply), failed (with reason).
- Terminology: LinkedIn terms stay as-is ("Easy Apply").
- UI language: Brazilian Portuguese.
- Open decision: behavior when an uncertain question comes up while the user is away (discard and re-queue the job after the answer vs. wait).

## Evidence on Hand
No real data yet: the tool hasn't run against LinkedIn. Example data in the UI must be labeled synthetic.

## Product Principles
- The user is away most of the time: the app has to summarize "what happened / what depends on me" at a glance.
- Nothing is sent in the user's name without traceability: every answer used is visible and editable.
- Uncertainty is surfaced, never hidden.
- The profile is the source of truth; better profile = fewer questions.
