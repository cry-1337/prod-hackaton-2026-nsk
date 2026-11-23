import base64
import hashlib
import os
from pathlib import Path
import secrets
import subprocess
import sys
import tempfile
import time
import traceback
import unittest
from contextlib import contextmanager
from urllib.error import URLError
from urllib.request import urlopen
from xml.etree import ElementTree

ROOT = Path(__file__).resolve().parents[1]
API = ROOT / "src/Backend.Api"
UNIT = ROOT / "tests/Backend.UnitTests"
TESTS = ROOT / "tests"


def run(*args, **options):
    return subprocess.run(args, cwd=ROOT, check=True, **options)


def prepare_upgrade(directory):
    schema = directory / "previous.sql"
    run("dotnet", "tool", "restore")
    run("dotnet", "ef", "migrations", "script", "0", "AddGroupChat", "--project", "src/Backend.Infrastructure",
        "--startup-project", "src/Backend.Api", "--configuration", "Release", "--no-build", "--output", schema)
    salt = secrets.token_bytes(16)
    password = hashlib.pbkdf2_hmac("sha1", b"upgrade-test-password", salt, 1000, 32)
    password_hash = base64.b64encode(bytes(1) + salt + password).decode()
    with schema.open() as sql:
        run("psql", "-X", "-q", "-v", "ON_ERROR_STOP=1", stdin=sql)
    with (TESTS / "seed_upgrade.sql").open() as sql:
        run("psql", "-X", "-q", "--single-transaction", "-v", "ON_ERROR_STOP=1", "-v",
            "password_hash=" + password_hash, "-f", "-", stdin=sql)


@contextmanager
def api_server(directory):
    log = directory / "api.log"
    with log.open("w") as output:
        server = subprocess.Popen(["dotnet", "bin/Release/net10.0/Backend.Api.dll"], cwd=API, stdout=output,
                                  stderr=subprocess.STDOUT)
    try:
        for _ in range(120):
            if server.poll() is not None:
                raise RuntimeError("API exited during startup")
            try:
                with urlopen(os.environ["API_BASE_URL"] + "/health", timeout=2) as response:
                    if response.status == 200:
                        break
            except URLError:
                time.sleep(0.5)
        else:
            raise RuntimeError("API readiness timeout")
        yield
    except Exception:
        print(log.read_text()[-4000:], file=sys.stderr)
        raise
    finally:
        server.terminate()
        try:
            server.wait(timeout=10)
        except subprocess.TimeoutExpired:
            server.kill()
            server.wait()


def main():
    unit_summary = e2e_summary = "не запускались"
    try:
        with tempfile.TemporaryDirectory() as tmp:
            directory = Path(tmp)
            run("dotnet", "build", "Backend.slnx", "--configuration", "Release")
            report_dir = os.environ.get("TEST_REPORT_DIR")
            loggers = ["--logger", "trx;LogFileName=unit.trx"]
            if report_dir:
                report_dir = (ROOT / report_dir).resolve()
                report_dir.mkdir(parents=True, exist_ok=True)
                loggers += ["--logger", f"junit;LogFilePath={report_dir / 'unit.xml'};MethodFormat=Full"]
            unit = subprocess.run(["dotnet", "test", UNIT, "-c", "Release", "--no-build", *loggers,
                                   "--results-directory", directory], cwd=ROOT)
            unit_summary = "ошибка запуска"
            counters = ElementTree.parse(directory / "unit.trx").find(".//{*}Counters")
            passed, total = int(counters.get("passed")), int(counters.get("total"))
            unit_summary = f"{passed}/{total} прошли"
            unit_ok = unit.returncode == 0 and total > 0 and passed == total

            os.environ.setdefault("Jwt__SigningKey", base64.b64encode(secrets.token_bytes(32)).decode())
            os.environ["API_BASE_URL"] = os.environ["ASPNETCORE_URLS"]
            e2e_summary = "ошибка подготовки БД или запуска API"
            prepare_upgrade(directory)
            executed = errors = skipped = 0
            e2e_ok = True
            for name, modules in (
                ("auth", ("auth_smoke", "resource_metrics_smoke")),
                ("checkout", ("checkout_metrics_smoke", "group_purchase_smoke", "upgrade_e2e"))
            ):
                with api_server(directory):
                    if report_dir:
                        report = report_dir / f"e2e-{name}.xml"
                        run_result = subprocess.run([sys.executable, "-m", "pytest", "-v", "--tb=short",
                            "-o", f"junit_suite_name=e2e-{name}", f"--junitxml={report}",
                            *(TESTS / f"{module}.py" for module in modules)], cwd=ROOT)
                        suites = ElementTree.parse(report).findall(".//testsuite")
                        executed += sum(int(suite.get("tests")) for suite in suites)
                        errors += sum(int(suite.get("errors")) + int(suite.get("failures")) for suite in suites)
                        skipped += sum(int(suite.get("skipped")) for suite in suites)
                        e2e_ok = e2e_ok and run_result.returncode == 0
                    else:
                        suite = unittest.defaultTestLoader.loadTestsFromNames(modules)
                        result = unittest.TextTestRunner(verbosity=2).run(suite)
                        executed += result.testsRun
                        errors += len(result.errors) + len(result.failures)
                        skipped += len(result.skipped)
            e2e_summary = f"выполнено {executed}, ошибок {errors}, пропущено {skipped}"
            return 0 if unit_ok and e2e_ok and executed > 0 and not errors and not skipped else 1
    except (OSError, RuntimeError, subprocess.CalledProcessError, ElementTree.ParseError):
        traceback.print_exc()
        return 1
    finally:
        print(f"Итог тестов\n  Unit — {unit_summary}\n  E2E — {e2e_summary}", flush=True)


if __name__ == "__main__":
    sys.exit(main())
