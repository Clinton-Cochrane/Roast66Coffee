import importlib.util
import io
import sys
import tempfile
import unittest
from contextlib import redirect_stderr, redirect_stdout
from pathlib import Path


SCRIPT = Path(__file__).parents[1] / "backend_coverage.py"
SPEC = importlib.util.spec_from_file_location("backend_coverage", SCRIPT)
assert SPEC and SPEC.loader
backend_coverage = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = backend_coverage
SPEC.loader.exec_module(backend_coverage)


def report_xml(filename: str = "Controllers/OrderController.cs") -> str:
    return f"""<?xml version="1.0"?>
<coverage lines-covered="7" lines-valid="10" branches-covered="2" branches-valid="4">
  <packages><package><classes>
    <class filename="{filename}"><lines>
      <line number="1" hits="1" branch="True" condition-coverage="50% (1/2)" />
      <line number="2" hits="1" branch="False" />
    </lines></class>
    <class filename="Services/OrderService.cs"><lines>
      <line number="1" hits="1" branch="True" condition-coverage="50% (1/2)" />
      <line number="2" hits="0" branch="False" />
    </lines></class>
    <class filename="SecurityConfiguration.cs"><lines>
      <line number="1" hits="1" branch="False" />
    </lines></class>
  </classes></package></packages>
</coverage>"""


class BackendCoverageTests(unittest.TestCase):
    def run_report(self, xml: str, *extra_args: str):
        temporary = tempfile.TemporaryDirectory()
        self.addCleanup(temporary.cleanup)
        directory = Path(temporary.name)
        report = directory / "coverage.xml"
        summary = directory / "summary.md"
        report.write_text(xml, encoding="utf-8")
        with redirect_stdout(io.StringIO()), redirect_stderr(io.StringIO()):
            result = backend_coverage.main(
                [str(report), "--summary-file", str(summary), *extra_args]
            )
        return result, summary

    def test_publishes_scoped_summary_and_passes_at_floor(self):
        result, summary = self.run_report(report_xml())

        self.assertEqual(0, result)
        text = summary.read_text(encoding="utf-8")
        self.assertIn("| Application | 70.00% (7/10) | 50.00% (2/4) |", text)
        self.assertIn("| Controllers | 100.00% (2/2) | 50.00% (1/2) |", text)
        self.assertIn("| Services | 50.00% (1/2) | 50.00% (1/2) |", text)
        self.assertIn("| Security paths | 100.00% (1/1) | n/a |", text)

    def test_fails_when_a_gate_regresses(self):
        result, _ = self.run_report(report_xml(), "--minimum-line", "70.01")

        self.assertEqual(1, result)

    def test_rejects_generated_source_in_meaningful_report(self):
        result, _ = self.run_report(
            report_xml("/checkout/CoffeeShopApi/Migrations/Generated.cs")
        )

        self.assertEqual(2, result)

    def test_rejects_report_when_a_published_scope_disappears(self):
        result, _ = self.run_report(report_xml("Other/OrderController.cs"))

        self.assertEqual(2, result)


if __name__ == "__main__":
    unittest.main()
