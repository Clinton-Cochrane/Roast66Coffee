#!/usr/bin/env python3
"""Verify the production header contract on each Render static site."""

from __future__ import annotations

import json
import re
import sys
from pathlib import Path


SERVICE_START = re.compile(r"^(?P<indent> *)- type: web$")
FIELD = re.compile(r"^(?P<indent> *)(?P<key>[A-Za-z][A-Za-z0-9]*):(?P<value>.*)$")
LIST_FIELD = re.compile(
    r"^(?P<indent> *)- (?P<key>path|name|value):(?P<value>.*)$"
)


def scalar(value: str) -> str:
    value = value.strip()
    if value.startswith('"'):
        return json.loads(value)
    if value.startswith("'") and value.endswith("'"):
        return value[1:-1].replace("''", "'")
    return value


def static_service_headers(path: Path, service_name: str) -> list[dict[str, str]]:
    lines = path.read_text(encoding="utf-8").splitlines()
    service_starts = [
        (index, len(match.group("indent")))
        for index, line in enumerate(lines)
        if (match := SERVICE_START.match(line))
    ]

    for position, (start, service_indent) in enumerate(service_starts):
        end = service_starts[position + 1][0] if position + 1 < len(service_starts) else len(lines)
        child_indent = service_indent + 2
        metadata: dict[str, str] = {"type": "web"}
        headers_start: int | None = None

        for index in range(start + 1, end):
            match = FIELD.match(lines[index])
            if not match or len(match.group("indent")) != child_indent:
                continue
            key = match.group("key")
            if key == "headers":
                headers_start = index + 1
                break
            metadata[key] = scalar(match.group("value"))

        if metadata.get("name") != service_name or metadata.get("runtime") != "static":
            continue
        if headers_start is None:
            return []

        headers: list[dict[str, str]] = []
        current: dict[str, str] | None = None
        item_indent = child_indent + 2
        field_indent = item_indent + 2
        for line in lines[headers_start:end]:
            if not line.strip() or line.lstrip().startswith("#"):
                continue
            indentation = len(line) - len(line.lstrip(" "))
            if indentation <= child_indent:
                break
            list_match = LIST_FIELD.match(line)
            if list_match and indentation == item_indent:
                if current is not None:
                    headers.append(current)
                current = {
                    list_match.group("key"): scalar(list_match.group("value"))
                }
                continue
            field_match = FIELD.match(line)
            if current is not None and field_match and indentation == field_indent:
                key = field_match.group("key")
                if key in {"path", "name", "value"}:
                    current[key] = scalar(field_match.group("value"))
        if current is not None:
            headers.append(current)
        return headers

    raise AssertionError(f"{path}: static service {service_name!r} was not found")


def verify(path: Path, service_name: str, api_origin: str) -> None:
    csp = (
        "default-src 'self'; base-uri 'self'; object-src 'none'; "
        "frame-ancestors 'none'; script-src 'self'; "
        "style-src 'self' 'unsafe-inline'; font-src 'self'; "
        "img-src 'self' data: blob:; "
        f"connect-src 'self' {api_origin}; manifest-src 'self'; "
        "worker-src 'self' blob:; form-action 'self'"
    )
    expected = {
        ("/*", "Content-Security-Policy"): csp,
        ("/*", "X-Frame-Options"): "DENY",
        ("/*", "X-Content-Type-Options"): "nosniff",
        ("/*", "Referrer-Policy"): "strict-origin-when-cross-origin",
        ("/*", "Permissions-Policy"): (
            "camera=(), geolocation=(), microphone=(), payment=(), usb=()"
        ),
        ("/*", "Strict-Transport-Security"): "max-age=31536000; includeSubDomains",
        ("/*", "Cache-Control"): "no-cache",
        ("/assets/*", "Cache-Control"): "public, max-age=31536000, immutable",
    }

    actual: dict[tuple[str, str], str] = {}
    for header in static_service_headers(path, service_name):
        if set(header) != {"path", "name", "value"}:
            raise AssertionError(f"{path}: malformed static-site header: {header}")
        key = (header["path"], header["name"])
        if key in actual:
            raise AssertionError(f"{path}: duplicate static-site header: {key}")
        actual[key] = header["value"]

    for key, expected_value in expected.items():
        if actual.get(key) != expected_value:
            raise AssertionError(
                f"{path}: expected header {key}={expected_value!r}, "
                f"found {actual.get(key)!r}"
            )


def main() -> int:
    manifests = (
        (Path("render.dev.yaml"), "roast66-web", "https://roast66coffee.onrender.com"),
        (Path("render.prod.yaml"), "roast66-web-prod", "https://roast66-api-prod.onrender.com"),
    )
    try:
        for manifest in manifests:
            verify(*manifest)
    except (AssertionError, json.JSONDecodeError) as error:
        print(error, file=sys.stderr)
        return 1

    print("Render static-site header contracts passed.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
