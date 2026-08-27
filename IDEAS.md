# Mergewell Ideas

This document collects possible improvements for future consideration. Items are proposals, not commitments or scheduled work.

## Reliability

- Add integration fixtures for mixed Word/PDF jobs, ZIP and RAR extraction, cancellation, and malformed inputs.
- Detect encrypted or damaged documents before a long merge begins.
- Add resumable jobs and cleanup policies for interrupted work.
- Validate free disk space and path-length constraints before import.
- Add structured diagnostic logs with a privacy-aware support export.

## Workflow

- Add an export action for copying a completed PDF to a user-selected location.
- Add retry and partial-merge options for failed items.
- Add page-range selection, reordering, and exclusions before merging.
- Add searchable history with retention controls.
- Support saved merge presets for repeated folder structures.

## Conversion

- Evaluate a non-COM Word conversion backend for environments without Microsoft Office.
- Add image and text-file inputs where conversion quality can be guaranteed.
- Support bookmarks or a generated table of contents based on the input tree.
- Add optional PDF compression and metadata editing.

## Distribution

- Sign application binaries and installers with a trusted code-signing certificate.
- Publish checksums and a software bill of materials with each release.
- Add ARM64 installer releases after validating Word automation and native dependencies.
- Evaluate MSIX and Windows Package Manager distribution.
- Add automated dependency and security scanning.

## Accessibility And Localization

- Complete keyboard-only and screen-reader testing.
- Add high-contrast validation and scalable layout tests.
- Externalize user-facing strings and add localization support.