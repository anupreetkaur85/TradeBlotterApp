"""Convert Claude Code session JSONL transcripts into a readable Markdown file.

Includes every human prompt and assistant response in order, summarizes tool
calls, and truncates large tool outputs for readability. Scrubs obvious secrets
(passwords in connection strings, bearer tokens, API keys) and email addresses.
"""
import json
import re
import sys
from datetime import datetime
from pathlib import Path

# End the transcript at the last development work (Jun 10, ~08:45 local, right
# after the inline-autocomplete change). Anything after this is the administrative
# work of producing this transcript, not part of building the app.
END_CUTOFF = datetime.fromisoformat("2026-06-10T12:46:00+00:00")

SESSIONS = [
    # (path, label) in chronological order
    (r"C:\Users\anupr\.claude\projects\C--Users-anupr-source-repos\aa749e3b-c092-4db0-ba47-bc532943b27d.jsonl", "Session 1"),
    (r"C:\Users\anupr\.claude\projects\C--Users-anupr-source-repos\e6c889fa-eb8d-41a6-ae06-a3ecfd14faba.jsonl", "Session 2"),
]
OUT = Path(r"C:\Users\anupr\source\repos\tradeblotter\docs\ai-transcript\transcript.md")

TOOL_RESULT_MAX = 600
THINKING_MAX = 800

SECRET_PATTERNS = [
    (re.compile(r"(?i)(password|pwd)\s*=\s*[^;\"'\s]+"), r"\1=[REDACTED]"),
    (re.compile(r"(?i)bearer\s+[A-Za-z0-9._\-]+"), "Bearer [REDACTED]"),
    (re.compile(r"\bsk-[A-Za-z0-9]{16,}\b"), "[REDACTED-API-KEY]"),
    (re.compile(r"\bgh[pousr]_[A-Za-z0-9]{20,}\b"), "[REDACTED-TOKEN]"),
    (re.compile(r"[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}"), "[redacted-email]"),
    # Redact references to unrelated internal repositories.
    (re.compile(r"HBC\.FlexTrade\.SecuritySyncService"), "[external-repo]"),
    (re.compile(r"HBCOrderManagement"), "[external-repo]"),
    (re.compile(r"CustomerOrderManagement"), "[external-repo]"),
    (re.compile(r"\bHBC\b"), "[external-repo]"),
]


def scrub(text):
    if not text:
        return text
    for pat, repl in SECRET_PATTERNS:
        text = pat.sub(repl, text)
    return text


def strip_system_reminders(text):
    text = re.sub(r"<system-reminder>.*?</system-reminder>", "", text, flags=re.DOTALL)
    return text.strip()


def truncate(text, limit):
    text = text.rstrip()
    if len(text) <= limit:
        return text
    return text[:limit] + f"\n… [truncated, {len(text) - limit} more chars]"


def summarize_tool(name, tool_input):
    keys = ("file_path", "command", "pattern", "path", "url", "prompt", "skill", "description")
    detail = ""
    for k in keys:
        if isinstance(tool_input, dict) and k in tool_input and tool_input[k]:
            detail = str(tool_input[k]).splitlines()[0]
            break
    detail = truncate(detail, 160)
    return f"🔧 **{name}** — {detail}" if detail else f"🔧 **{name}**"


def block_text(block):
    if isinstance(block, dict):
        return block.get("text") or block.get("content") or ""
    return str(block)


def render(out, path, label):
    out.append(f"\n\n## {label}\n")
    with open(path, encoding="utf-8") as fh:
        for line in fh:
            line = line.strip()
            if not line:
                continue
            try:
                o = json.loads(line)
            except json.JSONDecodeError:
                continue
            t = o.get("type")
            if t not in ("user", "assistant"):
                continue
            if o.get("isMeta"):
                continue
            stamp = o.get("timestamp")
            if stamp:
                try:
                    if datetime.fromisoformat(stamp.replace("Z", "+00:00")) >= END_CUTOFF:
                        continue
                except ValueError:
                    pass
            msg = o.get("message", {})
            content = msg.get("content")
            blocks = content if isinstance(content, list) else [{"type": "text", "text": content}]

            if t == "user":
                pieces = []
                for b in blocks:
                    bt = b.get("type") if isinstance(b, dict) else "text"
                    if bt == "tool_result":
                        raw = b.get("content")
                        if isinstance(raw, list):
                            raw = "\n".join(block_text(x) for x in raw)
                        snippet = truncate(scrub(str(raw or "")), TOOL_RESULT_MAX)
                        pieces.append(f"<sub>tool result:</sub>\n```\n{snippet}\n```")
                    else:
                        txt = strip_system_reminders(block_text(b))
                        if txt:
                            pieces.append(scrub(txt))
                body = "\n\n".join(p for p in pieces if p.strip())
                if body.strip():
                    out.append(f"\n### 🧑 User\n\n{body}\n")
            else:  # assistant
                pieces = []
                for b in blocks:
                    bt = b.get("type") if isinstance(b, dict) else "text"
                    if bt == "text":
                        txt = block_text(b)
                        if txt.strip():
                            pieces.append(scrub(txt))
                    elif bt == "thinking":
                        think = truncate(scrub(b.get("thinking") or block_text(b)), THINKING_MAX)
                        if think.strip():
                            pieces.append(f"<details><summary>reasoning</summary>\n\n{think}\n\n</details>")
                    elif bt == "tool_use":
                        pieces.append(scrub(summarize_tool(b.get("name", "tool"), b.get("input", {}))))
                body = "\n\n".join(p for p in pieces if p.strip())
                if body.strip():
                    out.append(f"\n### 🤖 Assistant\n\n{body}\n")


def main():
    out = [
        "# AI Tooling Transcript",
        "",
        "Full transcript of the AI-assisted development of this project, produced with",
        "**Claude Code**. Human prompts and assistant responses are included in order;",
        "tool calls are summarized and large tool outputs truncated for readability.",
        "Secrets and email addresses have been scrubbed. It covers the build of the",
        "app through the final implementation change.",
    ]
    for path, label in SESSIONS:
        if Path(path).exists():
            render(out, path, label)
        else:
            print(f"skip missing {path}", file=sys.stderr)
    OUT.parent.mkdir(parents=True, exist_ok=True)
    OUT.write_text("\n".join(out), encoding="utf-8")
    print(f"wrote {OUT} ({OUT.stat().st_size} bytes)")


if __name__ == "__main__":
    main()
