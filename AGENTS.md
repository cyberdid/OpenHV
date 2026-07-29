# Universe RTS Project Memory

This repository uses a persistent, agent-maintained project wiki inspired by
Karpathy's LLM Wiki pattern. The code remains the executable source of truth;
the wiki compiles the project's intent, evidence, decisions, and current
understanding so that work compounds across sessions.

## Start of Every Task

1. Read `knowledge/wiki/index.md`.
2. Read the wiki pages linked from the index that are relevant to the task.
3. Read the latest entries in `knowledge/wiki/log.md`.
4. Inspect the working tree before changing files.
5. If code and wiki disagree, trust verified code or test output, then update
   the wiki and explicitly record the correction.

## Three Layers

### Raw sources: `knowledge/raw/`

- Raw sources and source records are immutable after they are committed.
- Never rewrite, normalize, or delete an existing raw source.
- Add a new dated source when information changes or a corrected source arrives.
- Preserve provenance: origin, retrieval date, version or seed, and exact
  commands when applicable.

### Compiled wiki: `knowledge/wiki/`

- The agent owns the maintenance of this layer; the user reads and directs it.
- Integrate new evidence into existing pages instead of merely adding isolated
  notes.
- Keep pages small, focused, linked, and useful without chat history.
- Mark uncertainty, contradictions, and unverified hypotheses explicitly.
- Use repository-relative links for code and wiki references.

### Schema: this file

- These rules define how project memory is maintained.
- Evolve them when the workflow changes, and record the reason in
  `knowledge/wiki/log.md`.

## Required Workflows

### Ingest

When a source, experiment, design reference, or external project is added:

1. Preserve it or its immutable source record under `knowledge/raw/`.
2. Extract the facts relevant to this project.
3. Update every affected wiki page and cross-reference.
4. Add or revise a decision record when direction changes.
5. Update `knowledge/wiki/index.md`.
6. Append an `ingest` entry to `knowledge/wiki/log.md`.

### Build or change

After a meaningful code change:

1. Verify it in proportion to risk.
2. Update architecture, behavior, roadmap, or decision pages affected by it.
3. Record test commands and results.
4. Append a `change` entry to the log.
5. Stage only the files that belong to the completed change.
6. Create a focused local commit after validation passes.
7. Push the current feature branch to the configured user-owned GitHub remote.

Do not leave meaningful completed work only in chat history or an unpushed
working tree. Do not push failing, secret-bearing, or unrelated changes merely
to satisfy the publication rule.

### Experiment

Every simulation or balancing experiment must record:

- date and purpose;
- code commit;
- map set, bot composition, seeds, duration, and commands;
- metric definition;
- raw result location;
- observed result and interpretation;
- limitations and the next experiment.

Do not call a timed score leader a combat winner without stating that the match
ended on a time limit.

### Query

Answer project questions from the wiki first, then inspect code or raw sources
to fill gaps. Durable new conclusions should be filed back into the wiki.

### Lint

Periodically check for:

- stale claims contradicted by code or newer experiments;
- orphan or unindexed pages;
- broken links;
- decisions without evidence;
- roadmap items that are already implemented;
- experiments that cannot be reproduced;
- missing cross-references and unresolved contradictions.

Record lint passes in the log.

## Wiki Conventions

- Every normal wiki page begins with YAML frontmatter containing `title`,
  `status`, `updated`, `sources`, and `tags`.
- Use ISO dates (`YYYY-MM-DD`).
- Decision files use `decisions/NNNN-short-name.md`.
- Experiment files use `experiments/YYYY-MM-DD-short-name.md`.
- `knowledge/wiki/index.md` is the content catalog and must be updated whenever
  pages are added or renamed.
- `knowledge/wiki/log.md` is append-only. Entries begin with:
  `## [YYYY-MM-DD] operation | title`.
- Avoid copying large source passages into the wiki. Summarize and link to the
  source.

## Git Publication

- `upstream` is the source project and may be read-only.
- `origin` should be the user-owned fork used for feature-branch pushes.
- Keep meaningful work on a named feature branch, not directly on upstream
  `main`.
- After relevant checks pass and the wiki is current, commit and push the
  feature branch.
- Never force-push or rewrite published history unless the user explicitly
  authorizes it.
- Opening or merging a pull request is a separate action and requires an
  explicit request or an agreed release workflow.
