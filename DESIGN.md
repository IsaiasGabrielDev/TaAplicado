---
name: Tá Aplicado
description: A registered-mail counter for job applications; every application is an object with a code, a trail and a receipt.
colors:
  paper: "#F4F1E8"
  paper-2: "#EAE4D3"
  sheet: "#FFFDF7"
  field: "#FBF9F3"
  kraft: "#B98B52"
  kraft-soft: "#E7D9C1"
  kraft-ink: "#7A5427"
  meter-ink: "#3D3322"
  meter-label: "#5A3E1C"
  ink: "#1C2B4A"
  ink-2: "#4A5670"
  ink-3: "#5E677C"
  red: "#C8102E"
  red-soft: "#F6DADF"
  red-on-ink: "#FFB3BF"
  green: "#2F7D4F"
  red-deep: "#9E0C24"
  red-on-soft: "#7D0A1D"
  meter-time: "#6E5634"
  ink-deep: "#111C33"
typography:
  display:
    fontFamily: "Segoe UI Variable Display, Segoe UI, system-ui, sans-serif"
    fontSize: "5.5rem"
    fontWeight: 600
    lineHeight: 0.9
    letterSpacing: "-0.04em"
  headline:
    fontFamily: "Segoe UI Variable Display, Segoe UI, system-ui, sans-serif"
    fontSize: "1.75rem"
    fontWeight: 600
    lineHeight: 1.1
    letterSpacing: "-0.02em"
  title:
    fontFamily: "Segoe UI Variable Display, Segoe UI, system-ui, sans-serif"
    fontSize: "1.1rem"
    fontWeight: 600
    lineHeight: 1.35
  body:
    fontFamily: "Segoe UI Variable Text, Segoe UI, system-ui, sans-serif"
    fontSize: "14px"
    fontWeight: 400
    lineHeight: 1.45
    fontFeature: "tnum"
  label:
    fontFamily: "Segoe UI Variable Text, Segoe UI, system-ui, sans-serif"
    fontSize: "0.8rem"
    fontWeight: 400
    lineHeight: 1.45
  stamp:
    fontFamily: "Segoe UI Variable Display, Segoe UI, system-ui, sans-serif"
    fontSize: "0.72rem"
    fontWeight: 700
    lineHeight: 1.2
    letterSpacing: "0.06em"
  mono:
    fontFamily: "Cascadia Mono, Cascadia Code, Consolas, monospace"
    fontSize: "0.78rem"
    fontWeight: 500
    lineHeight: 1
  counter:
    fontFamily: "Cascadia Mono, Cascadia Code, Consolas, monospace"
    fontSize: "1.35rem"
    fontWeight: 500
    lineHeight: "2.3rem"
rounded:
  mark: "2px"
  r: "3px"
  stamp: "4px"
  bar: "1px"
  pill: "999px"
spacing:
  xs: "0.25rem"
  sm: "0.5rem"
  md: "0.75rem"
  lg: "1rem"
  xl: "1.25rem"
  2xl: "1.5rem"
  3xl: "2rem"
  row: "2.25rem"
components:
  button-primary:
    backgroundColor: "{colors.ink}"
    textColor: "{colors.paper}"
    rounded: "{rounded.r}"
    padding: "0 1rem"
    height: "2.35rem"
  button-primary-hover:
    backgroundColor: "{colors.ink-deep}"
  button-outline:
    backgroundColor: "transparent"
    textColor: "{colors.ink}"
    rounded: "{rounded.r}"
    padding: "0 1rem"
    height: "2.35rem"
  button-outline-hover:
    backgroundColor: "{colors.paper-2}"
  input:
    backgroundColor: "{colors.field}"
    textColor: "{colors.ink}"
    rounded: "{rounded.r}"
    padding: "0.45rem 0.6rem"
  tab:
    textColor: "{colors.ink-2}"
    padding: "0 0.85rem"
    height: "3.25rem"
  tab-active:
    textColor: "{colors.ink}"
  badge-waiting:
    backgroundColor: "{colors.red}"
    textColor: "#FFFFFF"
    rounded: "{rounded.mark}"
    typography: "{typography.mono}"
    padding: "0 0.35rem"
  code-chip:
    backgroundColor: "{colors.sheet}"
    textColor: "{colors.ink}"
    rounded: "{rounded.mark}"
    typography: "{typography.mono}"
  stamp:
    textColor: "{colors.ink}"
    rounded: "{rounded.stamp}"
    typography: "{typography.stamp}"
    padding: "0.12rem 0.45rem"
  stamp-waiting:
    textColor: "{colors.red}"
  stamp-applied:
    textColor: "{colors.green}"
  stamp-skip:
    textColor: "{colors.kraft-ink}"
  digit:
    backgroundColor: "{colors.ink}"
    textColor: "{colors.paper}"
    rounded: "{rounded.mark}"
    typography: "{typography.counter}"
    width: "1.55rem"
    height: "2.3rem"
  digit-waiting:
    textColor: "{colors.red-on-ink}"
  slip:
    backgroundColor: "{colors.sheet}"
    textColor: "{colors.ink}"
    padding: "1rem 1.25rem 1.1rem"
  meter:
    backgroundColor: "{colors.kraft-soft}"
    textColor: "{colors.meter-ink}"
    rounded: "{rounded.r}"
    padding: "1.25rem"
  choice-selected:
    backgroundColor: "{colors.ink}"
    textColor: "{colors.paper}"
    rounded: "{rounded.r}"
    padding: "0.4rem 0.75rem"
  filter-pressed:
    backgroundColor: "{colors.ink}"
    textColor: "{colors.paper}"
    rounded: "{rounded.r}"
    padding: "0.3rem 0.7rem"
---

# Design System: Tá Aplicado

## Overview

**Creative North Star: "Objeto Registrado"**

The app is a registered-mail counter. Every job application is a registered object: it gets a code on a registered label, an event trail as it moves, a rubber-stamped status, and a receipt of exactly what was sent in the user's name. The window is a sheet of ruled form paper in navy ink, with kraft card for the machinery (the franking meter, the tracking log) and one registration red that means only "this is waiting on you".

Density is real: the ledger runs at a 2.25rem row, counts are tabular monospace, and nothing is padded into cards for decoration. Scale follows urgency instead of hierarchy: the number of avisos waiting is the largest thing on screen, and it goes grey when it hits zero. The system refuses the generic dark sidebar, KPI-card and table dashboard; its structure comes from postal objects (slips, labels, stamps, counters, receipts), not from panels.

**Words stay plain.** The postal world is visual only. Screens and actions use everyday pt-BR, so a non-technical user reads them without a metaphor to decode: Candidatar, Avisos, Histórico, Respostas salvas, Perfil, Usuário; Responder / Respondido; Parada por você. Stamps keep the plain status words (ENVIADA, AGUARDANDO VOCÊ, PULADA, FALHOU, TESTE).

**Key Characteristics:**
- Light ruled paper ground, navy ink for text and every primary action.
- Registration red reserved for things waiting on the user.
- Postal objects as components: registered-label code chips, rubber stamps, a torn aviso slip, a franking counter with digit windows.
- Tabular numerals everywhere; Cascadia Mono for codes, counts, times.
- Small, near-square corners (2-4px); flat surfaces with one soft paper shadow on slips.
- Diagonal stripe is the only disabled signal.

## Colors

Paper and ink carry the whole interface; kraft marks the machine layer; red and green are status, never decoration.

### Primary
- **Ledger Navy** (ink): body text, headings, the primary button fill, selected segments/choices/filters, the active tab underline, code-chip borders and "R" block, the brand mark, franking digit windows, focus rings.
- **Faded Ink** (ink-2): secondary text, labels, inactive tabs, table headers, company names.
- **Pencil Grey Ink** (ink-3): placeholders, tertiary notes, the zero pile count, input bottom rule, disabled text.

### Secondary
- **Kraft Card** (kraft-soft): the franking meter panel behind counters, trail and tracking log.
- **Kraft** (kraft): selection tint source, scrollbar thumb, row hover (at 10%) and open-row wash (8-14%).
- **Kraft Ink** (kraft-ink): trail markers, "pulada" stamps, the demo flag's dashed border.
- **Meter Ink / Meter Label** (meter-ink, meter-label): text set on the kraft card; ink on kraft is browner than ink on paper.

### Tertiary
- **Registration Red** (red): the pile count, the Avisos tab badge, the AGUARDANDO VOCÊ stamp, the trail end marker of a waiting object, the profile-gap count, the login-needed strip border.
- **Red Wash** (red-soft): background of the login-needed strip.
- **Red on Ink** (red-on-ink): the waiting counter's non-leading digits, where full red would drown on navy.
- **Delivered Green** (green): ENVIADA and ENVIANDO DE VERDADE stamps, the big RESPONDIDO stamp, the applied trail end marker, the completed profile gauge.

### Neutral
- **Ruled Form Paper** (paper): app ground, sticky table header fill.
- **Counter Paper** (paper-2): top bar, hover fill on outline buttons and icon buttons, one band of the disabled stripe, gauge track.
- **Slip White** (sheet): aviso slips, code-chip digits, the profile sheet, focused inline answers.
- **Form Field** (field): resting fill of inputs, segments, choices and filter chips.
- **Red Deep** (red-deep): the slip error line ("O LinkedIn recusou a resposta anterior"), red text on sheet white.
- **Red on Soft** (red-on-soft): text of the login-needed strip, set on red-soft.
- **Meter Time** (meter-time): mono timestamps in the trail and the activity log on kraft.
- **Ink Deep** (ink-deep): primary button hover only.
- **Rules**: hairline `rgba(28,43,74,.10)` between ledger rows and receipt lines; stronger `rgba(28,43,74,.22)` for borders and dividers.

### Named Rules
**The Waiting Red Rule.** Red (#C8102E and its red-soft / red-on-ink derivatives) marks only what is waiting on the user: the pile count, the tab badge, the AGUARDANDO VOCÊ stamp, the waiting counter digit, the profile gap count, the login-needed strip. Code chips, the brand mark and the stop button are ink. If it does not need the user, it is not red.

**The Status Ink Rule.** Status colours live in stamps and trail end markers, not in fills: green delivered, red waiting, kraft-ink skipped, ink queued/test, ink-2 failed.

## Typography

**Display Font:** Segoe UI Variable Display (with Segoe UI, system-ui)
**Body Font:** Segoe UI Variable Text (with Segoe UI, system-ui)
**Label/Mono Font:** Cascadia Mono (with Cascadia Code, Consolas)

**Character:** Native Windows sans for a Windows desktop tool, paired with the platform's own monospace for everything that is a code, a count or a timestamp. Tabular numerals are on globally.

### Hierarchy
- **Display** (600, 5.5rem, 0.9, -0.04em; 4rem under 1080px): the pile count only. One number per screen gets this size.
- **Headline** (600, 1.75rem, 1.1, -0.02em): screen titles. The profile gauge number uses the same face at 2.5rem.
- **Title** (600, 1.1rem, 1.35): the aviso question, max 60ch. Pile caption 1.25rem; panel titles 0.95rem; detail headings 0.85rem in ink-2.
- **Body** (400, 14px, 1.45): everything else; intros capped at 68ch, empty states at 52ch.
- **Label** (0.8rem, ink-2): field labels, counter labels; table headers 0.78rem 600.
- **Stamp** (700, 0.72rem, 0.06em, uppercase): rubber-stamp status text; the only uppercase in the system.
- **Mono** (500, 0.78rem): code chips, badge, times, ledger dates; counter digits 1.35rem on a 2.3rem line.

### Named Rules
**The Urgency Scale Rule.** Size follows what waits on the user, not page hierarchy: the waiting count outranks every heading.

**The Machine Type Rule.** Codes, counts, times and the profile sheet are Cascadia Mono; prose is never mono.

## Layout

Single window: a 3.25rem top bar (brand, tabs, demo flag) over one scrolling screen with 1.5rem padding (1rem under 700px). Screen heads are a wrap-flex row of title plus intro or controls, 1.25rem above content.

The Candidatar desk is a two-column grid (1.35fr / minmax(17rem, 1fr), 1.5rem gap): the aviso pile left, the kraft meter right; it stacks under 700px, where the send block (`.go-wrap`) takes the full row and the button goes full width. The run-mode strip (ENVIANDO DE VERDADE / TESTE) lives in the send block under Parar at every width, never on the meter, so it is always beside the control that stops the run. The desk is hidden while "Antes de começar" is open. The dispatch form is a wrap-flex row aligned to baseline ends, closed by a 1px rule. It opens with the site switch (LinkedIn | Indeed, a segment); Indeed hides the LinkedIn origins and keeps only the terms. The Perfil screen is sheet plus a sticky 18rem side column, stacking under 1080px.

Ledger tables are full width with a 2.25rem row, sticky headers with a 2px ink rule, and a hairline between rows. Spacing steps are 0.25 / 0.5 / 0.75 / 1 / 1.25 / 1.5 / 2rem.

## Elevation & Depth

Flat paper. Depth comes from tone (paper-2 bar, kraft-soft meter, sheet-white slips) and rules, not shadow. The one exception is the aviso slip, which sits on the paper as a torn-off notice.

### Shadow Vocabulary
- **Slip lift** (`box-shadow: 0 1px 2px rgba(0,0,0,.05), 0 6px 18px -10px rgba(0,0,0,.22)`): aviso slips only.
- **Tab underline** (`box-shadow: inset 0 -2px 0 #1C2B4A`): active tab indicator, not elevation.

### Named Rules
**The One Loose Sheet Rule.** Only the aviso slip casts a shadow, because it is the only thing the user picks up. Panels, tables and controls stay flat.

## Shapes

Near-square corners: 3px on buttons, inputs, meter, filters and choices; 2px on the small printed marks (code chip, badge, digit windows, brand mark); 4px on stamps; 1px on the profile gauge bar; pill (999px) only on suggestion chips and the quick-answer chips of a slip. Borders are 1px, except stamps (2px currentColor) and table header rules (2px ink). Slips have square tops with a perforated edge (a radial-gradient row of punched holes) and rounded bottoms. Stamps rotate -2deg (the big RESPONDIDO stamp -8deg, 1.4rem). The registered label is a bordered chip with a solid ink "R" block on its left.

### Named Rules
**The Stripe Means Disabled Rule.** A -45deg stripe of paper-2 and paper (6px bands) marks a disabled input or button, and nothing else. Never use it as texture, emphasis or progress.

## Components

### Buttons
Tactile and ink-plain.
- **Shape:** 3px corners, 2.35rem min height, 0 1rem padding, 600 0.9rem text, optional 1.15rem stroke icon.
- **Primary:** ink fill, paper text (Candidatar de verdade, Responder, Salvar perfil); hover deepens to ink-deep.
- **Armed (confirm):** the first real send of a session arms instead of sending: sheet fill, ink text, a double ink rule (2px border plus an inset 1px line), label "Confirmar envio", and the note under it turns ink 600 and restates the send (origin · Normal/Ultra · até N · em nome de <usuário>). No timeout; a second click inside 600 ms is ignored; any change to the form or a click elsewhere disarms.
- **Outline:** 1px ink border, transparent, ink text; hover paper-2. The Parar (stop) button is this outline in ink, never red.
- **Active:** translateY(1px). **Disabled:** the diagonal stripe, ink-3 text, line border.
- **Icon button:** 2rem square, borderless, ink-2, hover paper-2. Delete is two-step: first click arms it with a text confirm.

### Chips
- **Registered label (code chip):** 1px ink border, 2px corners, ink "R" block with paper letter, then the number in mono 0.78rem on sheet white, grouped in threes. An Indeed object swaps the "R" for the word "Indeed" (UI face, 600 0.7rem) and keeps its hex id ungrouped.
- **Filter chips:** field fill, line border, 0.85rem label with a mono count; pressed state inverts to ink fill, paper text.
- **Choices:** radio options as field-fill chips; checked inverts to ink.

### Stamps (signature)
Rubber-stamp statuses: 2px currentColor border, 4px corners, 700 uppercase 0.72rem with 0.06em tracking, rotated -2deg, no fill. Colour carries the status (see The Status Ink Rule). Answering an aviso drops a large green RESPONDIDO stamp onto the slip (scale 1.6 to 1, .35s) and dims the form.

### Aviso Slip (signature)
Sheet-white notice with a perforated top edge and the slip shadow: meta row (code chip, job, relative time in mono), the question at title size, Claude's suggestion note, then the answer control and a primary Responder button in a wrap row. When LinkedIn hid the option text of a radio, the slip falls back to a text input with a note to type the form's own label, plus Sim / Não / Yes / No pill chips for two-option questions.

### Franking Counter (signature)
Four fixed digit windows per counter in a 2x2 grid on the kraft meter. Windows are flat ink with paper digits in mono; leading zeros are dimmed to paper at 28% opacity. Digits roll on a vertical reel (.6s, 70ms stagger). The waiting counter's significant digits turn red-on-ink.

### Cards / Containers
- **Meter:** kraft-soft fill, 1px kraft-ink border at 35%, 3px corners, 1.25rem padding; holds counters, the object trail and the tracking log.
- **Trail:** a vertical list with square kraft-ink markers joined by a hairline; the last event is bold ink, its marker green if delivered, red if waiting.

### Inputs / Fields
- **Style:** field fill, 1px line border with an ink-3 bottom edge, 3px corners.
- **Focus:** 2px ink outline, ink border.
- **Inline answers (Respostas salvas):** borderless except a bottom line until hover/focus, so the ledger reads as text.
- **Segmented control:** joined field-fill segments; selected is ink fill. While a run locks them, unselected segments take the disabled stripe. Normal | Ultra seleção is a segment, never a checkbox.

### Navigation
Text tabs in the paper-2 bar, ink-2 at rest, ink on hover, ink 600 with a 2px ink underline when current. The Avisos tab carries the red count badge only when avisos exist.

### Ledger and Receipt
Histórico rows open in place into an object detail: trail on the left, receipt on the right. The receipt heading follows the status and never claims a send that did not happen: "Respostas enviadas em seu nome" (enviada), "Preenchida em teste (descartada)", "Não enviada: N avisos esperando você" with a Responder os avisos button, "Não enviada: na fila", "Nada foi enviado". The receipt is a two-column definition list, question beside its answer (answer 600), hairline between pairs, so each answer reads directly against the question it answered.

## Do's and Don'ts

### Do:
- **Do** keep red for what waits on the user: pile count, tab badge, AGUARDANDO VOCÊ stamp, waiting digit, profile gap count, login-needed strip.
- **Do** render every application code as a registered label (ink "R" block plus mono number).
- **Do** show status as a rotated rubber stamp, coloured by status, unfilled.
- **Do** give franking digits flat ink windows with dimmed leading zeros.
- **Do** put receipt answers directly beside their questions.
- **Do** set codes, counts and times in Cascadia Mono with tabular numerals.

### Don't:
- **Don't** make the code chip, brand mark or stop button red; they are ink.
- **Don't** use the diagonal stripe for anything but disabled.
- **Don't** give panels or tables shadows; only the aviso slip lifts.
- **Don't** build the dark sidebar, KPI-card and table dashboard this system replaces.
- **Don't** use uppercase outside stamps.
