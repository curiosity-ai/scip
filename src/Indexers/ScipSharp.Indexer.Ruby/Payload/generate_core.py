#!/usr/bin/env python3
"""Dev-time generator for Payload/core.txt, the compact Ruby core/stdlib payload embedded in ScipSharp.Indexer.Ruby.

It reads the upstream Sorbet RBIs (.reference/scip-ruby/rbi) and keeps only what the native indexer needs:
class/module names, superclasses, includes/extends, method names (instance/singleton) with their simple return
type, and constants. The indexer itself never runs this (or any Ruby/Python): the output is committed.

Usage: generate_core.py <rbi-dir> > ../CorePayload.g.cs

Format (one record per line):
  C <Name> [<Super>]      class        M <Name>   module
  I <Name>                include of the current type     E <Name>   extend of the current type
  i <method> [<ret>]      instance method                 s <method> [<ret>]   singleton method
  k <Name>                constant (term) of the current type
<ret> is a class name, '@' for T.self_type, or '^' for T.attached_class.
"""
import os
import re
import sys

CORE_STDLIB = {"set", "forwardable", "json", "time", "date", "securerandom", "singleton", "open_struct", "pathname",
               "stringio", "logger", "tempfile", "fileutils", "timeout", "delegator", "benchmark", "base64", "yaml",
               "optparse", "monitor", "shellwords", "observable", "pp", "digest", "erb", "uri", "open3", "csv"}
SORBET = {"t", "enum", "tprops", "ttypes", "sorbet", "builder"}

CLASS_RE = re.compile(r"^(\s*)(class|module)\s+([A-Z][\w:]*)(?:\s*<\s*([A-Z][\w:]*))?")
DEF_RE = re.compile(r"^(\s*)def\s+(self\.)?([^\s(;]+)")
CONST_RE = re.compile(r"^(\s*)([A-Z]\w*(?:::[A-Z]\w*)*)\s*=\s*")
INCLUDE_RE = re.compile(r"^\s*(include|extend)\s+([A-Z][\w:]*)")
RET_RE = re.compile(r"\.?returns\(")


def balanced(s, start):
    depth = 0
    for i in range(start, len(s)):
        if s[i] in "([":
            depth += 1
        elif s[i] in ")]":
            depth -= 1
            if depth == 0:
                return s[start:i + 1]
    return s[start:]


def simple_type(expr):
    expr = expr.strip()
    m = re.match(r"T\.nilable\((.*)\)$", expr, re.S)
    if m:
        return simple_type(m.group(1))
    if expr == "T.self_type":
        return "@"
    if expr == "T.attached_class":
        return "^"
    m = re.match(r"T::(Array|Hash|Set|Range|Enumerator|Enumerable)\[", expr)
    if m:
        return m.group(1)
    if re.fullmatch(r"[A-Z][\w:]*", expr) and not expr.startswith("T::") and expr not in ("Elem", "K", "V", "U", "T", "Type", "Boolean"):
        return expr
    return None


def sig_return(sig):
    m = RET_RE.search(sig)
    if not m:
        return None
    start = sig.index("(", m.start())
    body = balanced(sig, start)[1:-1]
    return simple_type(body)


def process(path, out):
    stack = []  # (indent, full name)
    sig_lines = []
    in_sig = False
    sing_indent = None
    with open(path, encoding="utf-8") as f:
        lines = f.read().split("\n")
    for line in lines:
        stripped = line.strip()
        if stripped.startswith("#") or not stripped:
            continue
        indent = len(line) - len(line.lstrip())
        if re.match(r"^(sig\b)", stripped):
            in_sig = True
            sig_lines = []
        if in_sig:
            sig_lines.append(stripped)
            joined = " ".join(sig_lines)
            if (stripped == "end" or stripped.endswith("}")) and joined.count("{") == joined.count("}") and \
                    (stripped.endswith("}") or stripped == "end"):
                in_sig = False
            continue
        if stripped == "end":
            if sing_indent is not None and indent == sing_indent:
                sing_indent = None
            elif stack and indent == stack[-1][0]:
                stack.pop()
            continue
        if re.match(r"^class\s*<<\s*self", stripped):
            sing_indent = indent
            continue
        m = CLASS_RE.match(line)
        if m:
            kind, name, sup = m.group(2), m.group(3), m.group(4)
            full = name if not stack or "::" in name and False else name
            if stack and not name.startswith("::"):
                outer = stack[-1][1]
                full = name if name.startswith(outer + "::") else outer + "::" + name
            if re.search(r"\bend\s*$", stripped) and ";" in stripped:
                pass
            out.append(("C " if kind == "class" else "M ") + full + (" " + sup if sup else ""))
            stack.append((indent, full))
            continue
        if not stack:
            m = CONST_RE.match(line)
            if m and "::" in m.group(2):
                owner, _, cname = m.group(2).rpartition("::")
                out.append(f"X {owner} {cname}")
            elif m:
                out.append(f"X - {m.group(2)}")
            continue
        cur = stack[-1][1]
        m = INCLUDE_RE.match(line)
        if m:
            out.append(f"{'I' if m.group(1) == 'include' else 'E'} {cur} {m.group(2)}")
            continue
        m = DEF_RE.match(line)
        if m:
            single = bool(m.group(2)) or sing_indent is not None
            ret = sig_return(" ".join(sig_lines)) if sig_lines else None
            sig_lines = []
            out.append(f"{'s' if single else 'i'} {cur} {m.group(3)}" + (f" {ret}" if ret else ""))
            continue
        m = CONST_RE.match(line)
        if m:
            out.append(f"k {cur} {m.group(2)}")


def main():
    root = sys.argv[1]
    out = []
    for sub, allow in (("core", None), ("sorbet", SORBET), ("stdlib", CORE_STDLIB)):
        d = os.path.join(root, sub)
        for fn in sorted(os.listdir(d)):
            if not fn.endswith(".rbi"):
                continue
            if allow is not None and fn[:-4] not in allow:
                continue
            process(os.path.join(d, fn), out)
    # Compact: group by owner.
    types = {}
    order = []
    extra = []
    for rec in out:
        parts = rec.split(" ")
        tag = parts[0]
        if tag in ("C", "M"):
            name = parts[1]
            if name not in types:
                types[name] = {"tag": tag, "super": parts[2] if len(parts) > 2 else None, "lines": []}
                order.append(name)
            elif len(parts) > 2 and not types[name]["super"]:
                types[name]["super"] = parts[2]
        elif tag in ("I", "E", "i", "s", "k"):
            owner = parts[1]
            if owner in types:
                rest = " ".join([tag] + parts[2:])
                if rest not in types[owner]["lines"]:
                    types[owner]["lines"].append(rest)
        elif tag == "X":
            extra.append(parts)
    lines = []
    for name in order:
        t = types[name]
        lines.append(f"{t['tag']} {name}" + (f" {t['super']}" if t["super"] else ""))
        lines.extend(t["lines"])
    for _, owner, cname in extra:
        if owner == "-":
            continue
        if owner in types and f"k {cname}" not in types[owner]["lines"]:
            lines.append(f"@ {owner}")
            lines.append(f"k {cname}")
    sys.stdout.write("// <auto-generated> by Payload/generate_core.py from the upstream Sorbet RBIs; do not edit. </auto-generated>\n"
                     "namespace ScipSharp.Indexer.Ruby;\n\ninternal static class CorePayloadData\n{\n    public const string Text = \"\"\"\n"
                     + "\n".join(lines) + "\n\"\"\";\n}\n")


main()
