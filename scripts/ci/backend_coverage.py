#!/usr/bin/env python3
"""Summarize meaningful backend coverage and enforce the current CI floor.

The Cobertura root provides the application-wide totals. Scope totals are
calculated from class-level line data so CI makes controller, service, and
security-path coverage visible instead of hiding everything behind one number.
"""

from __future__ import annotations

import argparse
import os
import re
import sys
import xml.etree.ElementTree as ET
from dataclasses import dataclass
from pathlib import Path
from typing import Callable, Iterable, Sequence


BRANCH_COUNTS = re.compile(r"\((\d+)/(\d+)\)")


@dataclass(frozen=True)
class Coverage:
    covered_lines: int
    total_lines: int
    covered_branches: int
    total_branches: int

    @property
    def line_percent(self) -> float:
        return _percent(self.covered_lines, self.total_lines)

    @property
    def branch_percent(self) -> float:
        return _percent(self.covered_branches, self.total_branches)


def _percent(covered: int, total: int) -> float:
    return 100.0 * covered / total if total else 100.0


def _application_path(filename: str) -> str:
    normalized = filename.replace("\\", "/")
    marker = "/CoffeeShopApi/"
    return normalized.rsplit(marker, 1)[-1] if marker in normalized else normalized


def _is_generated(filename: str) -> bool:
    application_path = _application_path(filename)
    return application_path.startswith("Migrations/") or application_path.endswith(
        ".Designer.cs"
    )


def _class_coverage(classes: Iterable[ET.Element]) -> Coverage:
    covered_lines = total_lines = covered_branches = total_branches = 0
    for class_element in classes:
        lines = class_element.find("lines")
        if lines is None:
            continue
        for line in lines.findall("line"):
            total_lines += 1
            covered_lines += int(line.get("hits", "0")) > 0
            if line.get("branch", "False").lower() != "true":
                continue
            match = BRANCH_COUNTS.search(line.get("condition-coverage", ""))
            if match:
                covered_branches += int(match.group(1))
                total_branches += int(match.group(2))
    return Coverage(covered_lines, total_lines, covered_branches, total_branches)


def _root_coverage(root: ET.Element) -> Coverage:
    return Coverage(
        int(root.get("lines-covered", "0")),
        int(root.get("lines-valid", "0")),
        int(root.get("branches-covered", "0")),
        int(root.get("branches-valid", "0")),
    )


def _format_percent(covered: int, total: int) -> str:
    return f"{_percent(covered, total):.2f}% ({covered}/{total})" if total else "n/a"


def _scope_rows(root: ET.Element) -> list[tuple[str, Coverage]]:
    classes = root.findall(".//class")
    generated = sorted(
        {
            element.get("filename", "")
            for element in classes
            if _is_generated(element.get("filename", ""))
        }
    )
    if generated:
        raise ValueError(
            "Generated migration/designer files are present in coverage: "
            + ", ".join(generated)
        )

    scopes: Sequence[tuple[str, Callable[[str], bool]]] = (
        ("Controllers", lambda path: path.startswith("Controllers/")),
        ("Services", lambda path: path.startswith("Services/")),
        (
            "Security paths",
            lambda path: path in {"JwtTokenSettings.cs", "SecurityConfiguration.cs"}
            or path.startswith("Middleware/"),
        ),
    )

    rows = [("Application", _root_coverage(root))]
    for name, matches in scopes:
        rows.append(
            (
                name,
                _class_coverage(
                    element
                    for element in classes
                    if matches(_application_path(element.get("filename", "")))
                ),
            )
        )
    missing_scopes = [name for name, coverage in rows if coverage.total_lines == 0]
    if missing_scopes:
        raise ValueError(
            "Coverage contains no lines for required scope(s): "
            + ", ".join(missing_scopes)
        )
    return rows


def _markdown(rows: Sequence[tuple[str, Coverage]]) -> str:
    table = [
        "## Backend coverage",
        "",
        "Generated EF migrations, snapshots, and designer files are excluded.",
        "",
        "| Scope | Line coverage | Branch coverage |",
        "| --- | ---: | ---: |",
    ]
    table.extend(
        f"| {name} | "
        f"{_format_percent(coverage.covered_lines, coverage.total_lines)} | "
        f"{_format_percent(coverage.covered_branches, coverage.total_branches)} |"
        for name, coverage in rows
    )
    return "\n".join(table) + "\n"


def _parse_args(argv: Sequence[str]) -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument(
        "report",
        type=Path,
        help="Cobertura XML report or results directory containing one report",
    )
    parser.add_argument("--minimum-line", type=float, default=70.0)
    parser.add_argument("--minimum-branch", type=float, default=50.0)
    parser.add_argument(
        "--summary-file",
        type=Path,
        default=Path(os.environ["GITHUB_STEP_SUMMARY"])
        if os.environ.get("GITHUB_STEP_SUMMARY")
        else None,
        help="Optional Markdown file to append (CI uses GITHUB_STEP_SUMMARY)",
    )
    return parser.parse_args(argv)


def _resolve_report(path: Path) -> Path:
    if path.is_file():
        return path
    if not path.is_dir():
        raise ValueError(f"Coverage path does not exist: {path}")

    reports = sorted(path.rglob("coverage.cobertura.xml"))
    if not reports:
        raise ValueError(f"No coverage.cobertura.xml found below: {path}")
    if len(reports) > 1:
        raise ValueError(f"Multiple coverage.cobertura.xml reports found below: {path}")
    return reports[0]


def main(argv: Sequence[str] | None = None) -> int:
    args = _parse_args(argv or sys.argv[1:])
    try:
        rows = _scope_rows(ET.parse(_resolve_report(args.report)).getroot())
    except (ET.ParseError, OSError, ValueError) as error:
        print(f"Coverage report error: {error}", file=sys.stderr)
        return 2

    summary = _markdown(rows)
    print(summary, end="")
    if args.summary_file:
        args.summary_file.parent.mkdir(parents=True, exist_ok=True)
        with args.summary_file.open("a", encoding="utf-8") as output:
            output.write(summary)

    application = rows[0][1]
    failures = []
    if application.line_percent < args.minimum_line:
        failures.append(
            f"line coverage {application.line_percent:.2f}% is below "
            f"{args.minimum_line:.2f}%"
        )
    if application.branch_percent < args.minimum_branch:
        failures.append(
            f"branch coverage {application.branch_percent:.2f}% is below "
            f"{args.minimum_branch:.2f}%"
        )
    if failures:
        print("Coverage gate failed: " + "; ".join(failures), file=sys.stderr)
        return 1

    print(
        f"Coverage gate passed (line >= {args.minimum_line:.2f}%, "
        f"branch >= {args.minimum_branch:.2f}%)."
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
