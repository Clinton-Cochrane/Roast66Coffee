import os
import subprocess
import tempfile
import unittest
from pathlib import Path


SCRIPT = Path(__file__).parents[1] / "local-preflight.sh"


class LocalPreflightTests(unittest.TestCase):
    def setUp(self):
        temporary = tempfile.TemporaryDirectory()
        self.addCleanup(temporary.cleanup)
        self.repository = Path(temporary.name) / "checkout"
        self.repository.mkdir()
        (self.repository / "CoffeeShopApi").mkdir()
        (self.repository / "roast66").mkdir()
        (self.repository / "env.example").write_text("POSTGRES_USER=root\n")
        (self.repository / "CoffeeShopApi" / ".env.example").write_text(
            "Admin__Password=Development1!\n"
        )
        (self.repository / "roast66" / ".env.example").write_text(
            "VITE_API_URL=http://localhost:5001/api\n"
        )
        (self.repository / "CoffeeShopApi" / "CoffeeShopApi.csproj").write_text(
            "<Project><PropertyGroup><TargetFramework>net10.0</TargetFramework>"
            "</PropertyGroup></Project>\n"
        )
        (self.repository / "CoffeeShopApi" / "SecurityConfiguration.cs").write_text(
            'public const string DevelopmentPassword = "Development1!";\n'
        )
        (self.repository / ".node-version").write_text("24.20.0\n")

        self.fake_bin = Path(temporary.name) / "bin"
        self.fake_bin.mkdir()
        self.write_executable(
            "docker",
            """#!/usr/bin/env bash
if [ "$1" = "info" ]; then exit 0; fi
if [ "$1" = "compose" ] && [ "$2" = "version" ]; then
  echo 'Docker Compose version v2.40.0'
  exit 0
fi
exit 1
""",
        )
        for command in ("npm", "npx", "python3", "rg", "curl"):
            self.write_executable(command, "#!/usr/bin/env bash\nexit 0\n")
        self.set_dotnet_version("10.0.100")
        self.set_node_version("v24.20.0")

    def write_executable(self, name: str, contents: str):
        path = self.fake_bin / name
        path.write_text(contents)
        path.chmod(0o755)

    def set_dotnet_version(self, version: str):
        self.write_executable(
            "dotnet", f"#!/usr/bin/env bash\necho '{version}'\n"
        )

    def set_node_version(self, version: str):
        self.write_executable("node", f"#!/usr/bin/env bash\necho '{version}'\n")

    def write_local_env_files(self, password: str = "ValidPassword1!"):
        (self.repository / ".env").write_text("POSTGRES_USER=root\n")
        (self.repository / "CoffeeShopApi" / ".env").write_text(
            f"Admin__Username=admin\nAdmin__Password={password}\n"
        )
        (self.repository / "roast66" / ".env").write_text(
            "VITE_API_URL=http://localhost:5001/api\n"
        )

    def run_preflight(self, scope: str):
        environment = os.environ.copy()
        environment["PATH"] = f"{self.fake_bin}:/usr/bin:/bin"
        return subprocess.run(
            [str(SCRIPT), scope, str(self.repository)],
            text=True,
            capture_output=True,
            env=environment,
            check=False,
        )

    def test_supported_dotnet_and_node_versions_pass(self):
        result = self.run_preflight("full")

        self.assertEqual(0, result.returncode, result.stderr)
        self.assertIn("Full-local-smoke preflight passed.", result.stdout)

    def test_unsupported_dotnet_version_is_actionable(self):
        self.set_dotnet_version("8.0.419")

        result = self.run_preflight("full")

        self.assertNotEqual(0, result.returncode)
        self.assertIn(".NET SDK 10 or newer", result.stderr)
        self.assertIn("active SDK is 8.0.419", result.stderr)

    def test_unsupported_node_version_is_actionable(self):
        self.set_node_version("v20.19.5")

        result = self.run_preflight("full")

        self.assertNotEqual(0, result.returncode)
        self.assertIn("Node 24.x", result.stderr)
        self.assertIn("active version is v20.19.5", result.stderr)

    def test_missing_env_file_identifies_example_and_copy_command(self):
        result = self.run_preflight("compose")

        self.assertNotEqual(0, result.returncode)
        self.assertIn("Missing required local environment file: .env", result.stderr)
        self.assertIn("cp env.example .env", result.stderr)
        self.assertIn("cp CoffeeShopApi/.env.example CoffeeShopApi/.env", result.stderr)
        self.assertIn("cp roast66/.env.example roast66/.env", result.stderr)

    def test_invalid_development_owner_password_fails_without_exposing_it(self):
        password = "private-invalid-value"
        self.write_local_env_files(password)

        result = self.run_preflight("compose")

        self.assertNotEqual(0, result.returncode)
        self.assertIn("effective local Owner password", result.stderr)
        self.assertNotIn(password, result.stdout)
        self.assertNotIn(password, result.stderr)

    def test_valid_development_owner_password_passes(self):
        self.write_local_env_files()

        result = self.run_preflight("compose")

        self.assertEqual(0, result.returncode, result.stderr)
        self.assertIn("Compose/local-stack preflight passed.", result.stdout)

    def test_development_admin_default_is_used_when_password_key_is_absent(self):
        self.write_local_env_files()
        (self.repository / "CoffeeShopApi" / ".env").write_text(
            "Admin__Username=admin\n"
        )

        result = self.run_preflight("compose")

        self.assertEqual(0, result.returncode, result.stderr)

    def test_bootstrap_password_takes_precedence_over_admin_password(self):
        self.write_local_env_files()
        backend_env = self.repository / "CoffeeShopApi" / ".env"
        backend_env.write_text(
            "Admin__Password=ValidPassword1!\n"
            "Bootstrap__Password=too-short\n"
        )

        result = self.run_preflight("compose")

        self.assertNotEqual(0, result.returncode)
        self.assertIn("Bootstrap__Password", result.stderr)
        self.assertNotIn("too-short", result.stderr)

    def test_valid_bootstrap_password_overrides_invalid_admin_password(self):
        self.write_local_env_files("too-short")
        backend_env = self.repository / "CoffeeShopApi" / ".env"
        backend_env.write_text(
            "Admin__Password=too-short\n"
            "Bootstrap__Password=ValidBootstrap1!\n"
        )

        result = self.run_preflight("compose")

        self.assertEqual(0, result.returncode, result.stderr)

    def test_multiple_independent_failures_are_reported_together(self):
        self.set_dotnet_version("8.0.419")
        self.set_node_version("v20.19.5")

        result = self.run_preflight("full")

        self.assertNotEqual(0, result.returncode)
        self.assertIn(".NET SDK 10 or newer", result.stderr)
        self.assertIn("Node 24.x", result.stderr)

    def test_compose_scope_ignores_host_dotnet_and_node_versions(self):
        self.write_local_env_files()
        self.write_executable("dotnet", "#!/usr/bin/env bash\nexit 99\n")
        self.write_executable("node", "#!/usr/bin/env bash\nexit 99\n")

        result = self.run_preflight("compose")

        self.assertEqual(0, result.returncode, result.stderr)

    def test_missing_playwright_managed_chromium_is_actionable(self):
        result = subprocess.run(
            [
                str(SCRIPT),
                "full",
                str(self.repository),
                "--require-browser",
            ],
            text=True,
            capture_output=True,
            env={**os.environ, "PATH": f"{self.fake_bin}:/usr/bin:/bin"},
            check=False,
        )

        self.assertNotEqual(0, result.returncode)
        self.assertIn("Playwright's managed Chromium", result.stderr)
        self.assertIn("npx playwright install chromium", result.stderr)


if __name__ == "__main__":
    unittest.main()
