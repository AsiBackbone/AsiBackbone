# Intent to Execution: An Accountability Pattern

*A stack-neutral way to think about the path from a proposed action to a performed one — and what it would take to account for that path afterward.*

> **Status:** conceptual primer. This describes a *pattern*, not a product. A concrete .NET reference implementation is linked at the end, but the pattern is meant to be portable to any stack and useful even if you never adopt that code.

## The gap between logging and accountability

Most systems can answer two questions well. *Was the caller allowed to do this?* — that is authorization. *Did something happen?* — that is logging. Between those two questions sits a third that far fewer systems can answer: **can we reconstruct why this action was permitted, whether anyone affirmed it, under which policy, with what scoped authority, and whether what actually executed matched what was authorized?**

"We logged it" is not the same as "we can account for it." A log line tells you an event occurred. Accountability tells you the event was the result of a decision you can still explain — reasons, constraints, the responsible party, the version of the rules in force at the time.

That gap used to be tolerable because the thing proposing an action was usually a person who could later be asked to explain themselves. As more consequential actions are proposed by services, automated workflows, and agents, the proposer increasingly *cannot* explain itself after the fact. The record has to carry the explanation, or the explanation is gone.

## The pattern

Think of the path from intent to execution as a sequence of responsibilities, each answering one question. It is a *logical* sequence: in a single process it may be a call chain; across services or in an event-driven system it may be spread across components and time. The stages are the same either way.

```
Intent              a proposed action, captured as data — not yet performed
  |
Policy context      who or what is asking, against what resource,
  |                 under which active constraints
Constraint eval     the rules in force narrow what is permissible
  |
Decision            an explicit outcome, with reasons: allow / warn /
  |                 deny / defer / acknowledge / escalate
Acknowledgment      when stakes warrant, a recorded affirmation of intent
  |                 before the act
Audit residue       the durable trace: reasons, policy version + hash,
  |                 actor, correlation, time
Capability scoping  follow-on authority as a short-lived, bounded grant
  |                 rather than ambient permission
Execution           performed by the host — outside the pattern
  |
Reconciliation      did what executed match what was decided?
```

Two things are worth noticing about the shape. First, **intent is captured as data before it executes** — the proposal and the act are separated, which is what makes the rest possible. Second, **execution sits deliberately outside** the spine. The pattern governs the approach to the decision boundary; it does not perform the action. That separation is a feature, not a gap to be filled: the system that performs side effects stays in control of them.

## The link most systems skip: acknowledgment of intent

Most audit trails record *outcomes* — that an action ran, and perhaps that it succeeded. Far fewer record that the intent was **affirmed before the act**: that a responsible actor saw the proposed action, its risk, and its consequences, and assented to it.

This is the difference between "the system did X" and "X was authorized, knowingly, by a party who can be named." For a routine low-stakes action, the acknowledgment may be implicit in policy. For a high-stakes action — a destructive administrative operation, a sensitive data access, an agent about to call a real-world tool — it may be an explicit human-in-the-loop step. Either way, the pattern's contribution is to make the *presence or absence* of acknowledgment a recorded fact rather than a later assumption.

It is the least common stage in practice and, arguably, the one most worth discussing. A trail that captures affirmation of intent answers a question after an incident that ordinary logs cannot: not just *what happened*, but *who stood behind it, and on what understanding*.

## Where the trail tends to go cold

The honest parts of the pattern are the unsolved ones. These are not weaknesses to hide; they are the substance of the conversation.

**Execution closure.** The pattern can produce a clean record right up to the decision and the capability grant — and then the host executes, and the trail frequently stops. Binding the granted authority to the actual side effect, and then *reconciling* "what ran" against "what was decided," is the least-solved part. Most implementations, reference ones included, leave it to the host. It may be the most interesting open problem in the whole shape.

**Trustworthiness of the record.** An audit trail is only as good as your confidence that it was not altered. Reasons, hashes, and timestamps are not tamper-evidence on their own. Where the trust line sits — signing, append-only storage, external anchoring — is a deliberate choice, and *"we have an audit record"* should never be mistaken for *"the record is trustworthy."*

**Distribution.** In one process the spine is a call sequence you can read top to bottom. Across services, or in event-driven systems, there is no shared stack: intent, decision, and execution may live in different components at different times. The correlation that stitches them back together becomes the hard engineering, and the part most likely to be incomplete when you need it.

## What this pattern is *not*

- It does **not** replace authentication or authorization. It sits alongside them and carries their answer forward in a reasoned, recorded form.
- It is **not** an enforcement engine. It informs the host's decision to execute; it does not execute.
- It is **not** compliance. Producing a defensible trail is not the same as satisfying any specific law, regulation, or audit framework.
- It is **not** tamper-evidence by default. See above.
- It is **not** an intelligence layer. It governs *decision flow*; it does not make the underlying judgment smarter.

## How it relates to things you already have

Most of this is recombination, and it is more honest to say so plainly than to claim novelty.

- **Authorization** answers "may this caller do this?" The pattern wraps that answer in a recorded, reasoned decision and carries it forward instead of discarding it.
- **Policy engines** (rule evaluation, decision-as-data) can *be* the constraint-evaluation stage. The pattern is about what surrounds the evaluation — acknowledgment, residue, scoping, reconciliation — not the evaluation itself.
- **Audit logging** records that events occurred. The pattern asks the records to carry enough structure — reasons, policy version, acknowledgment, correlation — to *account for* the event rather than merely note it.
- **Supply-chain provenance** (build attestation, the instinct behind frameworks like SLSA) is the same impulse — a verifiable trail of how something came to be — applied to build pipelines rather than runtime actions.

The emphasis that is less common across all of these is treating **acknowledgment of intent** and **execution reconciliation** as first-class, recorded stages rather than afterthoughts.

## Open questions

This is a conversation, not a manual, because the interesting parts are unsettled.

- How much of the spine should be explicit and visible, and how much quietly becomes ceremony that people route around? Where is the line between accountability and theater?
- Where should the trust boundary sit for a record to be worth trusting — without over-engineering every routine action into a notarized event?
- How do you close the execution loop — show that what ran matched what was decided — without coupling a governance layer to every side-effecting system in the estate?
- In distributed and event-driven systems, what is the *minimum* correlation needed to reconstruct intent-to-execution after the fact?
- Which actions actually deserve acknowledgment, and who — or what — is a valid acknowledger when the proposer is an automated agent?

None of these has a settled answer. That is the point of writing the pattern down: to argue about the answers against something concrete.

## A concrete specimen

[AsiBackbone](https://github.com/AsiBackbone/AsiBackbone) is one .NET-native implementation of this pattern — a governance spine of framework-neutral primitives (policy context, constraint evaluation, explicit decisions, acknowledgment, audit residue, capability tokens) with host-owned execution and optional persistence, signing, and telemetry seams. It is offered as something to read and react to as much as something to install: a worked example of the stages above, with deliberate boundaries about what it does and does not do.

If the pattern is useful to you and the specimen is not, that is a perfectly good outcome. The map matters more than any single road drawn on it.

## A note on provenance

This pattern grew out of a broader conceptual exploration of how open possibility narrows into realized action. That lineage is offered as inspiration and a thinking lens — not as a scientific or physical claim — and nothing in the pattern depends on it. The software question on its own is enough: *can we account for the path from intent to execution?*
