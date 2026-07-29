---
title: "Decision 0002: Maintain Persistent Project Memory"
status: accepted
updated: 2026-07-29
sources:
  - ../../raw/karpathy-llm-wiki-2026-07-29.md
  - ../../../AGENTS.md
tags:
  - decision
  - knowledge
  - workflow
---

# Decision 0002: Maintain Persistent Project Memory

## Context

Architecture choices, experiment conditions, balance conclusions, and open
questions would otherwise remain scattered across chat history and require
rediscovery.

## Decision

Adopt a repository-local, Git-backed project wiki following the LLM Wiki
pattern:

- immutable raw sources;
- an agent-maintained compiled wiki;
- an explicit schema in `AGENTS.md`;
- content index plus append-only chronological log;
- ingest, query, experiment, and lint workflows.

## Consequences

- Future agents must read and update project memory as part of meaningful work.
- Claims and decisions gain traceable evidence.
- Experiments become reproducible and comparable.
- Wiki maintenance adds small work to each change but prevents repeated
  project rediscovery.
