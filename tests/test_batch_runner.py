#!/usr/bin/env python3

from __future__ import annotations

import json
import os
import signal
import subprocess
import sys
import tempfile
import time
import unittest
from pathlib import Path


PROJECT_DIR = Path(__file__).resolve().parents[1]
RUNNER = PROJECT_DIR / "run-batch.py"
FAKE_SIMULATION = PROJECT_DIR / "tests" / "fake-simulation.py"


class BatchRunnerIntegrationTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temporary = tempfile.TemporaryDirectory(
            prefix="universe-batch-runner-test."
        )
        self.root = Path(self.temporary.name)
        self.results = self.root / "results"
        self.manifest_path = self.root / "manifest.json"
        self.manifest = {
            "schemaVersion": 1,
            "runId": "integration",
            "defaults": {
                "bots": ["aggressor", "economist"],
                "headless": True,
                "gameSpeed": "fastest",
                "maxWorldTicks": 50,
                "watchdogSeconds": 10,
                "telemetryIntervalTicks": 0,
            },
            "runner": {
                "workers": 2,
                "maxInfrastructureRetries": 1,
                "processTimeoutGraceSeconds": 5,
            },
            "matches": [
                {"map": "valid", "seed": 10},
                {"id": "flaky-match", "map": "flaky", "seed": 11},
                {
                    "id": "invalid-match",
                    "map": "invalid",
                    "seed": 12,
                    "bots": ["does-not-exist"],
                },
            ],
        }
        self.write_manifest()

    def tearDown(self) -> None:
        self.temporary.cleanup()

    def write_manifest(self) -> None:
        with self.manifest_path.open("w", encoding="utf-8") as stream:
            json.dump(self.manifest, stream, indent=2)

    def run_batch(self, *extra: str) -> subprocess.CompletedProcess[str]:
        return subprocess.run(
            self.batch_command(*extra),
            cwd=PROJECT_DIR,
            text=True,
            capture_output=True,
            check=False,
        )

    def batch_command(self, *extra: str) -> list[str]:
        return [
            sys.executable,
            str(RUNNER),
            str(self.manifest_path),
            "--results-root",
            str(self.results),
            "--simulation-command",
            str(FAKE_SIMULATION),
            *extra,
        ]

    def summary(self) -> dict:
        with (
            self.results / "integration" / "summary.json"
        ).open(encoding="utf-8") as stream:
            return json.load(stream)

    def test_retry_failure_isolation_and_resume(self) -> None:
        first = self.run_batch()
        self.assertEqual(first.returncode, 2, first.stdout + first.stderr)
        summary = self.summary()
        initial_started_utc = summary["startedUtc"]
        initial_elapsed = summary["elapsedWallSeconds"]
        self.assertEqual(summary["counts"], {
            "completed": 2,
            "invalid-configuration": 1,
        })

        run_dir = self.results / "integration"
        derived = [
            match for match in summary["matches"]
            if match["matchId"].startswith("match-0001-")
        ]
        self.assertEqual(len(derived), 1)
        self.assertTrue(
            (run_dir / "matches" / derived[0]["matchId"] / "result.json").is_file()
        )
        flaky_dir = run_dir / "matches" / "flaky-match"
        self.assertTrue((flaky_dir / "attempt-1.json").is_file())
        self.assertTrue((flaky_dir / "attempt-2.json").is_file())
        invalid_dir = run_dir / "matches" / "invalid-match"
        self.assertTrue((invalid_dir / "attempt-1.json").is_file())
        self.assertFalse((invalid_dir / "attempt-2.json").exists())

        attempts_before = sorted(run_dir.glob("matches/*/attempt-*.json"))
        resumed = self.run_batch(
            "--resume",
            "--workers",
            "1",
            "--max-infrastructure-retries",
            "0",
        )
        self.assertEqual(resumed.returncode, 2, resumed.stdout + resumed.stderr)
        attempts_after = sorted(run_dir.glob("matches/*/attempt-*.json"))
        self.assertEqual(attempts_before, attempts_after)
        self.assertIn("skip-completed", resumed.stdout)
        self.assertIn("skip-failed", resumed.stdout)
        resumed_summary = self.summary()
        self.assertEqual(resumed_summary["startedUtc"], initial_started_utc)
        self.assertGreaterEqual(
            resumed_summary["elapsedWallSeconds"], initial_elapsed
        )
        sessions = sorted((run_dir / "sessions").glob("*.json"))
        self.assertEqual(len(sessions), 2)
        for session_path in sessions:
            with session_path.open(encoding="utf-8") as stream:
                session = json.load(stream)
            self.assertIn("endedUtc", session)
            self.assertGreaterEqual(session["elapsedWallSeconds"], 0)

    def test_resume_rejects_schedule_drift(self) -> None:
        first = self.run_batch()
        self.assertEqual(first.returncode, 2, first.stdout + first.stderr)
        self.manifest["matches"][0]["seed"] = 999
        self.write_manifest()

        resumed = self.run_batch("--resume")
        self.assertEqual(resumed.returncode, 2)
        self.assertIn("does not match", resumed.stderr)

    def test_manifest_rejects_disabled_batch_watchdog(self) -> None:
        self.manifest["defaults"]["watchdogSeconds"] = 0
        self.write_manifest()

        rejected = self.run_batch()
        self.assertEqual(rejected.returncode, 2)
        self.assertIn("minimum of 1", rejected.stderr)
        self.assertFalse((self.results / "integration").exists())

    @unittest.skipUnless(os.name == "posix", "POSIX signal semantics required")
    def test_interrupt_and_resume_only_unfinished_match(self) -> None:
        self.manifest["runId"] = "interrupt"
        self.manifest["runner"]["workers"] = 1
        self.manifest["runner"]["maxInfrastructureRetries"] = 0
        self.manifest["matches"] = [
            {"id": "interrupt-me", "map": "interrupt-once", "seed": 20}
        ]
        self.write_manifest()

        process = subprocess.Popen(
            self.batch_command(),
            cwd=PROJECT_DIR,
            text=True,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
        )
        assert process.stdout is not None
        output = []
        while True:
            line = process.stdout.readline()
            output.append(line)
            if "Starting interrupt-me attempt 1." in line:
                break
            self.assertTrue(line, "Runner exited before starting the match.")
        marker = (
            self.results
            / "interrupt"
            / "matches"
            / "interrupt-me"
            / ".fake-interrupt-started"
        )
        deadline = time.monotonic() + 5
        while not marker.is_file() and time.monotonic() < deadline:
            time.sleep(0.01)
        self.assertTrue(marker.is_file(), "Fake child did not reach its wait state.")
        process.send_signal(signal.SIGINT)
        stdout, stderr = process.communicate(timeout=10)
        output.append(stdout)
        self.assertEqual(process.returncode, 130, "".join(output) + stderr)

        match_dir = self.results / "interrupt" / "matches" / "interrupt-me"
        with (match_dir / "status.json").open(encoding="utf-8") as stream:
            interrupted = json.load(stream)
        self.assertEqual(interrupted["status"], "interrupted")
        self.assertEqual(interrupted["lastAttempt"], 1)

        resumed = self.run_batch("--resume")
        self.assertEqual(resumed.returncode, 0, resumed.stdout + resumed.stderr)
        with (match_dir / "status.json").open(encoding="utf-8") as stream:
            completed = json.load(stream)
        self.assertEqual(completed["status"], "completed")
        self.assertEqual(completed["lastAttempt"], 2)
        self.assertTrue((match_dir / "attempt-1.json").is_file())
        self.assertTrue((match_dir / "attempt-2.json").is_file())

    @unittest.skipUnless(os.name == "posix", "POSIX signal semantics required")
    def test_immediate_interrupt_cannot_escape_process_registration(self) -> None:
        self.manifest["runId"] = "immediate-interrupt"
        self.manifest["runner"]["workers"] = 1
        self.manifest["runner"]["maxInfrastructureRetries"] = 0
        self.manifest["matches"] = [
            {"id": "race-window", "map": "interrupt-once", "seed": 21}
        ]
        self.write_manifest()

        process = subprocess.Popen(
            self.batch_command(),
            cwd=PROJECT_DIR,
            text=True,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
        )
        assert process.stdout is not None
        output = []
        while True:
            line = process.stdout.readline()
            output.append(line)
            if "Starting race-window attempt 1." in line:
                break
            self.assertTrue(line, "Runner exited before starting the match.")
        process.send_signal(signal.SIGINT)
        try:
            stdout, stderr = process.communicate(timeout=5)
        except subprocess.TimeoutExpired:
            process.kill()
            process.communicate()
            pid_path = (
                self.results
                / "immediate-interrupt"
                / "matches"
                / "race-window"
                / ".fake-child-pid"
            )
            if pid_path.is_file():
                try:
                    os.killpg(int(pid_path.read_text(encoding="utf-8")), signal.SIGKILL)
                except ProcessLookupError:
                    pass
            self.fail("A child process escaped cancellation during registration.")
        output.append(stdout)
        self.assertEqual(process.returncode, 130, "".join(output) + stderr)

    def test_hard_timeout_is_classified_without_retry(self) -> None:
        self.manifest["runId"] = "timeout"
        self.manifest["defaults"]["watchdogSeconds"] = 1
        self.manifest["runner"]["maxInfrastructureRetries"] = 0
        self.manifest["runner"]["processTimeoutGraceSeconds"] = 5
        self.manifest["matches"] = [
            {"id": "hang", "map": "hang", "seed": 30}
        ]
        self.write_manifest()

        timed_out = self.run_batch()
        self.assertEqual(timed_out.returncode, 2, timed_out.stdout + timed_out.stderr)
        summary = self.summary_for("timeout")
        self.assertEqual(summary["counts"], {"failed": 1})
        self.assertEqual(summary["matches"][0]["classification"], "runner-timeout")
        self.assertEqual(summary["matches"][0]["lastAttempt"], 1)

    def summary_for(self, run_id: str) -> dict:
        with (self.results / run_id / "summary.json").open(
            encoding="utf-8"
        ) as stream:
            return json.load(stream)


if __name__ == "__main__":
    unittest.main()
