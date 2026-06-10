# AI Tooling Transcript

This project was built with **Claude Code** (an AI pair-programming tool), as the
assessment requires. This folder is the required full transcript of that work.

| File | What it is |
| --- | --- |
| [`transcript.md`](./transcript.md) | Human-readable transcript — every human prompt and assistant response in order, with tool calls summarized and large tool outputs truncated. |
| [`../../scripts/build_transcript.py`](../../scripts/build_transcript.py) | The script that renders `transcript.md` from the Claude Code session logs. |

## Secret scrubbing

The transcript was passed through an automated scrubber that redacts passwords in
connection strings, bearer tokens, API keys, and email addresses. The
application's SQL connection uses `Trusted_Connection=True` (Windows auth, no
password), so no database credentials appear.
