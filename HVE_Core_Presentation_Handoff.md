# HVE Core presentation handoff

## Instructions for the receiving Copilot session

Help Julio Diaz continue a one-hour introduction and demonstration of HVE Core for his Market Risk Platforms crew at UBS. Read this handoff and the accompanying three-slide PowerPoint before making changes.

Continue incrementally, one slide at a time. Preserve editable text and the established visual style. Explain concepts before the demo. Use simple language and connect the framework to the team's existing Agile process. Do not introduce FDE or staffing reductions. Verify current HVE Core capabilities and command syntax against the actual installed version before relying on them.

## Presenter, audience, and objective

- Presenter: Julio Diaz, Chapter Lead for Data Aggregation, Market Risk Platforms, UBS.
- Audience: More than 50 colleagues, including developers and colleagues involved in requirements, delivery, and testing.
- They are unfamiliar with HVE Core and are learning GitHub Copilot, mostly as a code generator.
- They attended a Spec Kit introduction last week.
- Some developers are concerned about AI affecting their jobs. Focus on engineering support and responsibility, not team-size reduction.
- Objective: Explain what HVE Core is, how it works, its potential strengths and limitations, and how to evaluate a shared AI-assisted software development process within existing Agile practices.
- Duration: 60 minutes including a demonstration and questions.

## Agreed positioning

### Hypervelocity Engineering: the methodology

Presentation wording:

> A software development methodology that uses AI throughout the lifecycle to accelerate delivery through structured workflows, shared context, and reusable engineering practices. Engineers remain responsible for decisions and quality.

### HVE Core: the framework

Presentation wording:

> Microsoft's framework for putting Hypervelocity Engineering into practice. It provides reusable agents, skills, instructions, and workflows that help teams apply those practices through GitHub Copilot.

Core distinction: HVE describes the approach; HVE Core provides building blocks to apply it.

These are audience-facing summaries, not verbatim official definitions. Validate them against current documentation when refining the deck.

### Scope and tone

- Do not include Forward-Deployed Engineering (FDE) in the main presentation. The team is not proposing that operating model. Explain the distinction only if asked.
- Avoid unsupported claims such as 2–3 times faster or weeks reduced to days.
- Treat faster delivery, consistency, and less rework as benefits to evaluate in a pilot.
- Do not describe HVE Core as a replacement for Agile, existing roles, QA, or engineering judgment.
- Do not claim security, compliance, or quality is automatically guaranteed.
- Keep the comparison with Spec Kit short and fair. Both provide structure for AI-assisted work; do not characterize Spec Kit as only for individuals and HVE Core as only for teams.

## Current team process and tools

1. Market Risk Officers (MROs) raise a business need.
2. The product owner enriches the request and sets priority.
3. Business analysts, project managers, and technical colleagues analyze it.
4. Requirements documentation often includes a proposed solution and expected outcome.
5. Developers implement the changes.
6. QA and business users validate the results.

Suggested terminology:

Business request → backlog refinement and prioritization → requirements and solution analysis → implementation → validation and review.

Requirements describe needed behavior and outcomes. Solution design describes how to achieve them.

The team uses GitLab for source control and issue tracking. Julio has configured GitLab MCP in Copilot and confirmed it works. Do not assume this receiving session has access until verified.

## Presentation angle

Use the team's familiar process as the backbone, then show how HVE Core can support it.

> We already have a development process. HVE Core gives us reusable ways for AI to support that process, preserve context between activities, and apply engineering practices more consistently.

Show contributions from the PO, BA, developers, QA, and MROs. Include developers early enough to assess feasibility and assumptions. Avoid a rigid document handover where business finishes a solution before engineering participates.

## Agenda and timing

| Time | Topic | Purpose |
|---|---|---|
| 0–5 min | Introduction and problem | Connect current Copilot usage to a shared development process |
| 5–12 min | HVE and HVE Core | Distinguish methodology from framework |
| 12–17 min | Main building blocks | Explain agents, skills, instructions, and tools/MCP |
| 17–22 min | Design thinking and DT Coach | Explain problem clarification and engineering input |
| 22–30 min | RPI and our process | Explain engineering work and context continuity |
| 30–50 min | Practical demo | Follow one bounded GitLab requirement |
| 50–55 min | Benefits, limitations, pilot | Explain what to evaluate |
| 55–60 min | Questions | Discuss fit and next steps |

Target around 10–12 slides, adjusting for readability. Definitions come before the demo.

## Current PowerPoint and design

Latest deck: `HVE_Core_Introduction_v5.pptx`.

It contains exactly three slides. Bring this file into the office session with this Markdown document. File paths from the previous session will not necessarily be available in the office.

Design:

- Widescreen 16:9.
- Dark navy background.
- Teal and blue abstract fiber-optic connections on the right.
- White headings, pale body text, teal emphasis.
- Aptos typography.
- Text is editable, not flattened into slide images.
- Original Microsoft logo on slide 3, with white backing for readability.
- Keep text in the clear left area.
- Use generous spacing and restrained visuals.
- The background repeats across the first three slides. Future compositions can vary to suit their content.

### Slide 1: Title

Small heading: HYPERVELOCITY ENGINEERING

Title: HVE Core

Subtitle: AI-assisted software development across the lifecycle

Presenter: Julio Diaz / Market Risk Platforms

### Slide 2: The methodology

Section label: THE METHODOLOGY

Title: Hypervelocity Engineering

Definition:

> A software development methodology that uses AI throughout the lifecycle to accelerate delivery with structured engineering practices.

Supporting statements:

> Shared context and reusable workflows guide the work.

> Engineers own the decisions and quality of the result.

### Slide 3: The framework

Section label: THE FRAMEWORK

Title: HVE Core

Definition:

> Microsoft's framework for putting Hypervelocity Engineering into practice through GitHub Copilot.

Supporting statements:

> Reusable agents, skills, instructions, and workflows bring engineering guidance into everyday work.

> Teams adapt the guidance to their projects and standards.

Includes the Microsoft logo.

## Next slides proposed, not yet created

4. Problems HVE Core helps address: repeated prompting, inconsistent standards, missing context, unclear assumptions, and difficult handovers.
5. Main building blocks: agents, skills, instructions, tools/MCP, with concrete examples.
6. Design thinking and DT Coach: clarify the underlying problem before committing to a solution.
7. RPI workflow: Research, Plan, Implement, Review, plus Follow-up.
8. Context continuity: requirements, findings, plans, changes, and validation evidence.
9. Fit with our existing process: proposed contributions from MROs, PO, BA, developers, QA.
10. Demo introduction: bounded scenario and acceptance criteria.
11. Benefits and practical limitations.
12. Proposed pilot and questions.

Combine or split topics as needed. Preserve time for the demo. The immediate next step is slide 4, explaining the problems the framework helps address.

## Main concepts

| Component | Plain explanation |
|---|---|
| Agent | Defines a role, behavior, and available tools |
| Skill | Provides a reusable procedure, potentially with scripts and templates |
| Instructions | Supply project rules and engineering guidance |
| Slash command | Invokes a prompt or skill |
| Tools/MCP | Let the agent interact with systems such as GitLab |

Keep YAML, installation details, and the full catalog in backup material. Configuration differs by Copilot client, version, and harness. Earlier conversations included version-sensitive examples, so do not copy tool names or configuration fields without checking the office setup.

## Design thinking section

Keep to about five minutes.

- Design thinking is the approach to understanding users, investigating problems, and exploring solutions.
- DT Coach is the HVE Core agent that guides those activities.
- It needs real stakeholder input. AI cannot substitute for user evidence.

Focus on the first three methods:

| Method | Meaning |
|---|---|
| Scope Conversations | Clarify challenge, stakeholders, goals, constraints |
| Design Research | Gather evidence about users' actual work and needs |
| Input Synthesis | Combine findings into a clear understanding of the problem |

Mention nine methods overall without teaching all nine.

Transition:

> Once we have enough understanding of the problem and desired outcome, we can bring that evidence into RPI to guide engineering work.

Design thinking is not a mandatory preliminary phase for every task. Clear requirements or well-defined bugs may provide enough context already.

DT Coach is not the only requirements tool. HVE Core also includes BRD/PRD and requirements-authoring capabilities. Verify current names before showing them.

## RPI section

Authoritative reference: https://github.com/microsoft/hve-core/blob/main/docs/rpi/README.md

The README inspected during preparation described Research, Plan, Implement, Review, plus Follow-up. Earlier discussion omitted Review from the name; use the current README rather than repeating that earlier simplification.

| Activity | Meaning |
|---|---|
| Research | Investigate missing information, existing code, dependencies, constraints |
| Plan | Define changes, sequence, and checkable requirements |
| Implement | Execute approved work and record validation results |
| Review | Compare implementation and evidence against requirements |
| Follow-up | Route defects, decision gaps, evidence gaps, and remaining work |

Nuances:

- Every task does not require every activity.
- Reuse adequate evidence rather than repeat research.
- RPI Agent coordinates applicable skills. Do not imply a dedicated autonomous agent for each phase.
- Saved artifacts preserve context between activities and conversations.
- Implementation complete does not automatically mean accepted.
- Artifacts live under `.copilot-tracking`, including research, plans, changes, and review records.
- GitLab synchronization needs an explicit workflow. Reading an issue via MCP does not automatically publish local findings back to it.

## Running example and demo

Use one scenario throughout:

> MROs need to identify which portfolios explain the risk change between two business dates.

Hypothetical discovery story, not a claim about measured user behavior:

- Initial request: We need another dashboard.
- Investigate the actual difficulty.
- Identify the need to see portfolios contributing to daily risk changes.
- Agree expected behavior, acceptance criteria, constraints, and open questions.

Demo setup:

- Small existing .NET application.
- Synthetic data.
- Bounded feature that can complete predictably.
- Include portfolios present on only one of the two dates.
- Prepare discovery and requirements outputs beforehand; focus live time on engineering.

| Time | Action | Point |
|---|---|---|
| 0–3 min | Read GitLab issue via MCP | Start from shared requirements |
| 3–7 min | Inspect code and findings | Existing behavior informs the approach |
| 7–11 min | Inspect plan, resolve ambiguity | People make business decisions |
| 11–17 min | Implement bounded work, run tests | Changes follow the plan |
| 17–20 min | Review diff, validation, remaining work | Completion needs evidence |

Prepare checkpoints and a backup recording. Clearly identify prepared results when using them. Do not let long generation waits consume the session.

## Benefits and limitations

Potential benefits to evaluate:

- Consistent application of project standards.
- Less repeated investigation and prompting.
- Clearer requirements and assumptions.
- Better context continuity and handovers.
- Reusable engineering knowledge.
- Explicit validation and review evidence.

Limitations:

- Requires setup and adaptation.
- Depends on available tools, permissions, and good input.
- AI output still needs review.
- Does not automatically guarantee security, compliance, or quality.
- Evolves rapidly. Pin and rehearse the actual demo version.

## Proposed pilot

One repository, a few volunteers, several representative changes.

Measure elapsed time, review effort, rework, defects, requirements and handover quality, AI usage/cost, and developer feedback. Use results to judge fit before expanding adoption.

## Opening script

Good morning, everyone.

Many of us are starting to use GitHub Copilot to generate code, write tests, and understand unfamiliar parts of our applications. As we gain experience, the next question is: How can we use AI consistently across our development process?

Last week's Spec Kit presentation introduced the value of clear specifications and structured AI-assisted development. Today, we'll build on that foundation by exploring HVE Core, Hypervelocity Engineering Core.

HVE Core provides reusable agents, skills, instructions, and workflows that help us apply AI throughout software development. It brings engineering guidance and project context into the work, while we remain responsible for decisions, tradeoffs, and reviewing the results.

I'll first explain Hypervelocity Engineering as a methodology, then HVE Core as the framework that puts it into practice. We'll look at the problems it helps address, its main components, and how design thinking connects to the RPI workflow.

After that, I'll demonstrate a practical example using Copilot and GitLab. We'll finish by discussing where this approach could help us and what we might evaluate in a pilot.

## Sources

- HVE Core: https://github.com/microsoft/hve-core
- RPI: https://github.com/microsoft/hve-core/blob/main/docs/rpi/README.md
- Design thinking: https://microsoft.github.io/hve-core/docs/design-thinking/
- DT Coach: https://microsoft.github.io/hve-core/docs/design-thinking/dt-coach/
- DT-to-RPI handoff: https://microsoft.github.io/hve-core/docs/design-thinking/tutorial-handoff-to-rpi/
- Spec Kit: https://github.com/github/spec-kit
- Microsoft logo used: https://uhf.microsoft.com/images/microsoft/RE1Mu3b.png

The decorative fiber-optic background is AI-generated. It is not an official Microsoft visual asset. Preserve the original Microsoft logo proportions.
