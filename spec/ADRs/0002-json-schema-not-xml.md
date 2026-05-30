# ADR-0002: JSON Schema for scenario files (not legacy XML)

**Status:** Accepted
**Date:** 2026-05-29

## Context

The legacy GuideArch persisted scenarios as hand-rolled XML (e.g., `SAS.xml`, `EDS.xml`). XML works but is verbose, lacks first-class schema tooling across our three target languages, and the legacy format was undocumented.

## Decision

Adopt JSON as the scenario file format, with a JSON Schema at `spec/domain/scenario.schema.json` as the validation contract. Use string identifiers (e.g., `"d-database"`) instead of indices for cross-entity references. Provide a one-shot Python converter at `tools/import-legacy-xml.py` for porting legacy scenarios into the seed conformance corpus.

## Consequences

- All three implementations get free validation via mature JSON Schema libraries (`ajv`, `JsonSchema.Net`, `jsonschema`).
- The conformance corpus speaks a single format readable by all three impls without special handling.
- Legacy XML is read-only and only via the converter — the apps never load XML directly.
