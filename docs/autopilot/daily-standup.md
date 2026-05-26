# Autopilot: Daily Standup

## Purpose

Run a daily standup round across all active team members. Each member posts a comment on the day's standup issue with observations and a curated news digest. This is not a work log. It is a signal.

## Trigger

Post once per workday, at the start of the day.

## Instructions for the Agent

Create a single standup issue for the day. @mention all active team members and instruct each to post their standup entry as a **comment on that issue** — not as a new issue.

---

## Rules

1. **Read before you write.** Before posting, read all existing comments on the issue. Do not repeat an observation already made by another member. If something has already been named, it's been named — move on. Agreement is not a contribution.
2. **Observations must be grounded in something specific.** No generic status. If you noticed something — a pattern, a friction point, something that went well — tie it to a concrete example from the codebase, the process, or the team.
3. **News items must include a source URL.** No URL, no entry.
4. **Special topics must be real.** If you're bringing a skill, library, or practice, explain concretely why it would help this team — not teams in general.

### News Digest Rules

- Search for **2–3 news items published since the last standup** (strictly — no recycling older items).
- Every item **must include a source URL**.
- Filter by: topics the team actively uses, your role in the project, programming languages the team actively uses.
- **Maximum one AI item**, and only if it is genuinely relevant to our work.
- For each item: write a short content summary, then explain concretely how it affects our system or team — tie it to something real in the codebase or process, not a generic observation.
- **Bring one topic of your own choice** — a skill, library, or practice the team has not yet applied that would improve our work or the team.

---

## Template

```markdown
# Daily <Name: Role>

## Observations

<What went right, what can be improved — grounded in something specific that happened>

## News

<Content summary>

<URL>

> How this helps us / What this could improve in our system or team — with a concrete example from our work

<!-- If code is involved -->
\```
<Code sample>
\```

## Special Topic

<Your chosen topic — a skill, library, or practice that would make the team better>
```

---

## Weekly Synthesis (Fridays)

On Fridays, the standup manager reviews all entries from the week and posts a short synthesis covering:

- Patterns observed across the team's observations
- Anything that looks like an emerging bottleneck
- Any topic that deserves a conversation before Monday

This is not a report card. It reads the sequence, not individual frames — looking for what the week's entries reveal together that no single entry shows alone.

---

## Notes

- "Since last standup" covers the weekend on Mondays.
- The news digest should take ten minutes, not twenty. One real insight beats three filler links.
- The standup is a signal, not a ritual. If writing it wouldn't change how anyone works today, the format has drifted into decoration. Flag it so the format can be fixed.
