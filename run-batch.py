#!/usr/bin/env python3

"""Run deterministic OpenHV simulations from a resumable batch manifest."""

from __future__ import annotations

import argparse
import concurrent.futures
import copy
import datetime as dt
import hashlib
import json
import os
import platform
import re
import shutil
import signal
import subprocess
import sys
import threading
import time
import uuid
from collections import Counter
from pathlib import Path
from typing import Any

try:
    from jsonschema import Draft202012Validator, FormatChecker
except ImportError as exc:  # pragma: no cover - exercised by the CLI environment
    raise SystemExit(
        "run-batch.py requires the Python 'jsonschema' package."
    ) from exc


SCHEMA_VERSION = 1
DEFAULT_OPTIONS = {
    "bots": ["aggressor", "economist", "technologist", "fortress"],
    "headless": True,
    "gameSpeed": "fastest",
    "maxWorldTicks": 1500,
    "watchdogSeconds": 120,
    "telemetryIntervalTicks": 0,
    "civilizationProfile": "balanced",
    "tradeEnabled": True,
}
DEFAULT_RUNNER = {
    "workers": 1,
    "maxInfrastructureRetries": 1,
    "processTimeoutGraceSeconds": 60,
    "successfulReplaySampleEvery": 0,
}
OPTION_KEYS = frozenset(DEFAULT_OPTIONS)
INFRASTRUCTURE_FAILURES = frozenset(
    {
        "crash",
        "desync",
        "invalid-result",
        "missing-result",
        "nonzero-exit",
        "runner-timeout",
        "watchdog-timeout",
    }
)
TERMINAL_FAILURES = frozenset({"failed", "invalid-configuration"})
ID_PATTERN = re.compile(r"^[A-Za-z0-9][A-Za-z0-9._-]{0,127}$")


def utc_now() -> str:
    return dt.datetime.now(dt.timezone.utc).isoformat().replace("+00:00", "Z")


def canonical_json(value: Any) -> bytes:
    return json.dumps(
        value, sort_keys=True, separators=(",", ":"), ensure_ascii=False
    ).encode("utf-8")


def sha256(value: Any) -> str:
    return hashlib.sha256(canonical_json(value)).hexdigest()


def read_json(path: Path) -> Any:
    with path.open(encoding="utf-8") as stream:
        return json.load(stream)


def write_json_atomic(path: Path, value: Any) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_name(
        f".{path.name}.{os.getpid()}.{threading.get_ident()}.tmp"
    )
    with temporary.open("w", encoding="utf-8") as stream:
        json.dump(value, stream, indent=2, ensure_ascii=False)
        stream.write("\n")
        stream.flush()
        os.fsync(stream.fileno())
    os.replace(temporary, path)


def load_validator(path: Path) -> Draft202012Validator:
    schema = read_json(path)
    Draft202012Validator.check_schema(schema)
    return Draft202012Validator(schema, format_checker=FormatChecker())


def validate_document(
    validator: Draft202012Validator, document: Any, label: str
) -> None:
    errors = sorted(validator.iter_errors(document), key=lambda error: list(error.path))
    if not errors:
        return

    rendered = []
    for error in errors[:20]:
        location = ".".join(str(part) for part in error.absolute_path) or "<root>"
        rendered.append(f"{location}: {error.message}")
    if len(errors) > 20:
        rendered.append(f"... and {len(errors) - 20} more errors")
    raise ValueError(f"{label} is invalid:\n" + "\n".join(rendered))


def resolve_manifest(manifest: dict[str, Any]) -> dict[str, Any]:
    defaults = copy.deepcopy(DEFAULT_OPTIONS)
    defaults.update(manifest.get("defaults", {}))
    unexpected_defaults = set(defaults) - OPTION_KEYS
    if unexpected_defaults:
        raise ValueError(
            "Unsupported default option(s): " + ", ".join(sorted(unexpected_defaults))
        )

    runner = copy.deepcopy(DEFAULT_RUNNER)
    runner.update(manifest.get("runner", {}))
    matches = []
    seen_ids: set[str] = set()
    for index, requested in enumerate(manifest["matches"]):
        resolved = copy.deepcopy(defaults)
        resolved.update(
            {key: value for key, value in requested.items() if key in OPTION_KEYS}
        )
        resolved["map"] = requested["map"]
        resolved["seed"] = requested["seed"]
        identity_source = {"index": index, **resolved}
        match_id = requested.get("id")
        if not match_id:
            match_id = f"match-{index + 1:04d}-{sha256(identity_source)[:10]}"
        if not ID_PATTERN.fullmatch(match_id):
            raise ValueError(f"Invalid match id: {match_id!r}")
        if match_id in seen_ids:
            raise ValueError(f"Duplicate match id: {match_id}")
        seen_ids.add(match_id)

        resolved["id"] = match_id
        resolved["index"] = index
        resolved["configFingerprint"] = sha256(
            {key: value for key, value in resolved.items() if key != "index"}
        )
        matches.append(resolved)

    schedule = {
        "schemaVersion": SCHEMA_VERSION,
        "runId": manifest["runId"],
        "description": manifest.get("description", ""),
        "runner": runner,
        "matches": matches,
    }
    schedule["scheduleHash"] = sha256(
        {
            "schemaVersion": schedule["schemaVersion"],
            "runId": schedule["runId"],
            "matches": schedule["matches"],
        }
    )
    return schedule


def git_metadata(project_dir: Path) -> dict[str, Any]:
    def run(*arguments: str) -> str:
        completed = subprocess.run(
            ["git", "-C", str(project_dir), *arguments],
            check=False,
            capture_output=True,
            text=True,
        )
        return completed.stdout.strip() if completed.returncode == 0 else "unknown"

    return {
        "commit": run("rev-parse", "HEAD"),
        "dirty": bool(run("status", "--porcelain"))
    }


class BatchRunner:
    def __init__(
        self,
        *,
        project_dir: Path,
        run_dir: Path,
        schedule: dict[str, Any],
        result_validator: Draft202012Validator,
        simulation_command: Path,
        resume: bool,
        retry_failures: bool,
    ) -> None:
        self.project_dir = project_dir
        self.run_dir = run_dir
        self.schedule = schedule
        self.result_validator = result_validator
        self.simulation_command = simulation_command
        self.resume = resume
        self.retry_failures = retry_failures
        self.cancel_requested = threading.Event()
        self.lock = threading.Lock()
        self.active_processes: dict[str, subprocess.Popen[str]] = {}
        self.started_monotonic = time.monotonic()
        self.started_utc = utc_now()
        self.runner_log = run_dir / "runner.log"
        self.session_path: Path | None = None

    def log(self, message: str) -> None:
        line = f"[{utc_now()}] {message}"
        with self.lock:
            print(line, flush=True)
            with self.runner_log.open("a", encoding="utf-8") as stream:
                stream.write(line + "\n")

    def request_cancel(self, signum: int, _frame: Any) -> None:
        if self.cancel_requested.is_set():
            return
        self.cancel_requested.set()
        self.log(f"Cancellation requested by signal {signum}.")
        with self.lock:
            active = list(self.active_processes.items())
        for match_id, process in active:
            self.log(f"Terminating active match {match_id}.")
            self.terminate_process(process)

    @staticmethod
    def terminate_process(process: subprocess.Popen[str]) -> None:
        if process.poll() is not None:
            return
        try:
            if os.name == "posix":
                os.killpg(process.pid, signal.SIGTERM)
            else:  # pragma: no cover - exercised on Windows
                process.terminate()
            process.wait(timeout=5)
        except (ProcessLookupError, subprocess.TimeoutExpired):
            if process.poll() is None:
                if os.name == "posix":
                    os.killpg(process.pid, signal.SIGKILL)
                else:  # pragma: no cover - exercised on Windows
                    process.kill()

    def prepare(self, manifest_path: Path) -> None:
        resolved_path = self.run_dir / "resolved-manifest.json"
        if self.run_dir.exists():
            if not self.resume:
                raise ValueError(
                    f"Run directory already exists: {self.run_dir}. "
                    "Use --resume to continue it."
                )
            if not resolved_path.is_file():
                raise ValueError(
                    f"Cannot resume {self.run_dir}: resolved-manifest.json is missing."
                )
            prior = read_json(resolved_path)
            if prior.get("scheduleHash") != self.schedule["scheduleHash"]:
                raise ValueError(
                    "The requested manifest does not match the existing resolved schedule."
                )
        else:
            self.run_dir.mkdir(parents=True)
            write_json_atomic(resolved_path, self.schedule)
            shutil.copyfile(manifest_path, self.run_dir / "manifest.json")
            engine_version_path = self.project_dir / "engine" / "VERSION"
            runtime = {
                "schemaVersion": SCHEMA_VERSION,
                "createdUtc": utc_now(),
                "platform": platform.platform(),
                "machine": platform.machine(),
                "processor": platform.processor(),
                "logicalCpuCount": os.cpu_count(),
                "pythonVersion": platform.python_version(),
                "engineVersion": (
                    engine_version_path.read_text(encoding="utf-8").strip()
                    if engine_version_path.is_file()
                    else "unknown"
                ),
                "git": git_metadata(self.project_dir),
            }
            write_json_atomic(self.run_dir / "runtime.json", runtime)

        session_dir = self.run_dir / "sessions"
        session_dir.mkdir(exist_ok=True)
        session_id = (
            dt.datetime.now(dt.timezone.utc).strftime("%Y%m%dT%H%M%SZ")
            + "-"
            + uuid.uuid4().hex[:8]
        )
        self.session_path = session_dir / f"{session_id}.json"
        write_json_atomic(
            self.session_path,
            {
                "schemaVersion": SCHEMA_VERSION,
                "sessionId": session_id,
                "startedUtc": self.started_utc,
                "resume": self.resume,
                "retryFailures": self.retry_failures,
                "workers": self.schedule["runner"]["workers"],
                "maxInfrastructureRetries": self.schedule["runner"][
                    "maxInfrastructureRetries"
                ],
                "processTimeoutGraceSeconds": self.schedule["runner"][
                    "processTimeoutGraceSeconds"
                ],
                "successfulReplaySampleEvery": self.schedule["runner"][
                    "successfulReplaySampleEvery"
                ],
                "git": git_metadata(self.project_dir),
            },
        )
        (self.run_dir / "matches").mkdir(exist_ok=True)
        self.log(
            f"Batch {self.schedule['runId']} prepared with "
            f"{len(self.schedule['matches'])} matches."
        )

    def finalize_session(self, exit_code: int | None) -> None:
        if self.session_path is None or not self.session_path.is_file():
            return
        session = read_json(self.session_path)
        session.update(
            {
                "endedUtc": utc_now(),
                "elapsedWallSeconds": round(
                    time.monotonic() - self.started_monotonic, 3
                ),
                "exitCode": exit_code,
                "interrupted": self.cancel_requested.is_set(),
            }
        )
        write_json_atomic(self.session_path, session)

    def result_errors(
        self, result_path: Path, match: dict[str, Any]
    ) -> tuple[dict[str, Any] | None, list[str]]:
        if not result_path.is_file():
            return None, ["result.json is missing"]
        try:
            result = read_json(result_path)
        except (OSError, json.JSONDecodeError) as exc:
            return None, [f"result.json cannot be read: {exc}"]

        errors = sorted(
            self.result_validator.iter_errors(result),
            key=lambda error: list(error.path),
        )
        messages = [
            (
                ".".join(str(part) for part in error.absolute_path) or "<root>"
            )
            + ": "
            + error.message
            for error in errors[:20]
        ]
        config = result.get("config", {}) if isinstance(result, dict) else {}
        expected = {
            "matchId": match["id"],
            "mapRequest": match["map"],
            "botTypes": match["bots"],
            "headless": match["headless"],
            "gameSpeed": match["gameSpeed"],
            "requestedRandomSeed": match["seed"],
            "maxWorldTicks": match["maxWorldTicks"],
            "watchdogSeconds": match["watchdogSeconds"],
            "telemetryIntervalTicks": match["telemetryIntervalTicks"],
            "civilizationProfile": match["civilizationProfile"],
            "tradeEnabled": match["tradeEnabled"],
        }
        for key, expected_value in expected.items():
            if config.get(key) != expected_value:
                messages.append(
                    f"config.{key}: expected {expected_value!r}, "
                    f"got {config.get(key)!r}"
                )
        return result, messages

    def existing_disposition(self, match: dict[str, Any]) -> str:
        match_dir = self.run_dir / "matches" / match["id"]
        status_path = match_dir / "status.json"
        if not status_path.is_file():
            return "run"
        try:
            status = read_json(status_path)
        except (OSError, json.JSONDecodeError):
            return "run"
        if status.get("configFingerprint") != match["configFingerprint"]:
            raise ValueError(f"Configuration drift for match {match['id']}.")

        if status.get("status") == "completed":
            _, errors = self.result_errors(match_dir / "result.json", match)
            return "skip-completed" if not errors else "run"
        if (
            status.get("status") in TERMINAL_FAILURES
            and not self.retry_failures
        ):
            return "skip-failed"
        return "run"

    @staticmethod
    def attempt_number(match_dir: Path) -> int:
        numbers = []
        for path in match_dir.glob("attempt-*"):
            match = re.fullmatch(r"attempt-(\d+)(?:[.-].*)?", path.name)
            if match:
                numbers.append(int(match.group(1)))
        return max(numbers, default=0) + 1

    def preserve_prior_artifacts(self, match_dir: Path, attempt: int) -> None:
        for artifact_name in ("result.json", "telemetry.jsonl", "events.jsonl"):
            artifact_path = match_dir / artifact_name
            if artifact_path.exists():
                os.replace(
                    artifact_path,
                    match_dir / f"attempt-{attempt}-prior-{artifact_name}",
                )

    def classify_attempt(
        self,
        *,
        return_code: int,
        timed_out: bool,
        canceled: bool,
        result: dict[str, Any] | None,
        result_errors: list[str],
        stdout_path: Path,
        stderr_path: Path,
    ) -> tuple[str, str]:
        if canceled:
            return "interrupted", "external-cancel"
        if timed_out:
            return "failed", "runner-timeout"
        if return_code != 0:
            combined = ""
            for path in (stdout_path, stderr_path):
                try:
                    combined += path.read_text(encoding="utf-8", errors="replace")
                except OSError:
                    pass
            invalid_markers = (
                "ArgumentException",
                "Could not find simulation map",
                "Unknown simulation bot type",
                "must specify",
            )
            if any(marker in combined for marker in invalid_markers):
                return "invalid-configuration", "invalid-configuration"
            return "failed", "nonzero-exit"
        if result is None:
            return "failed", "missing-result"
        if result_errors:
            return "failed", "invalid-result"
        end_reason = result["endReason"]
        if end_reason in INFRASTRUCTURE_FAILURES:
            return "failed", end_reason
        return "completed", end_reason

    def run_attempt(
        self, match: dict[str, Any], match_dir: Path, attempt: int
    ) -> dict[str, Any]:
        stdout_path = match_dir / f"attempt-{attempt}.stdout.log"
        stderr_path = match_dir / f"attempt-{attempt}.stderr.log"
        result_path = match_dir / "result.json"
        support_dir = match_dir / f"attempt-{attempt}-support"
        self.preserve_prior_artifacts(match_dir, attempt)

        env = os.environ.copy()
        env.update(
            {
                "SIMULATION_BOTS": ",".join(match["bots"]),
                "SIMULATION_HEADLESS": str(match["headless"]).lower(),
                "SIMULATION_SPEED": match["gameSpeed"],
                "SIMULATION_SEED": str(match["seed"]),
                "SIMULATION_MAX_TICKS": str(match["maxWorldTicks"]),
                "SIMULATION_WATCHDOG_SECONDS": str(match["watchdogSeconds"]),
                "SIMULATION_TELEMETRY_INTERVAL_TICKS": str(
                    match["telemetryIntervalTicks"]
                ),
                "SIMULATION_CIVILIZATION_PROFILE": match["civilizationProfile"],
                "SIMULATION_TRADE_ENABLED": str(match["tradeEnabled"]).lower(),
                "SIMULATION_MATCH_ID": match["id"],
                "SIMULATION_RESULT": str(result_path),
                "OPENHV_SUPPORT_DIR": str(support_dir),
            }
        )
        command = [str(self.simulation_command), match["map"]]
        started_utc = utc_now()
        started = time.monotonic()
        timed_out = False
        canceled = False
        return_code = -1
        self.log(f"Starting {match['id']} attempt {attempt}.")

        with stdout_path.open("w", encoding="utf-8") as stdout, stderr_path.open(
            "w", encoding="utf-8"
        ) as stderr:
            process = subprocess.Popen(
                command,
                cwd=self.project_dir,
                env=env,
                stdout=stdout,
                stderr=stderr,
                text=True,
                start_new_session=(os.name == "posix"),
                creationflags=(
                    subprocess.CREATE_NEW_PROCESS_GROUP
                    if os.name == "nt"  # pragma: no cover - Windows only
                    else 0
                ),
            )
            with self.lock:
                self.active_processes[match["id"]] = process
            # A signal can arrive after run_match checks the cancellation flag
            # but before this process is registered. Re-check after registration
            # so the new process cannot escape the signal handler's active set.
            if self.cancel_requested.is_set():
                self.terminate_process(process)
            hard_timeout = None
            if match["watchdogSeconds"] > 0:
                hard_timeout = (
                    match["watchdogSeconds"]
                    + self.schedule["runner"]["processTimeoutGraceSeconds"]
                )
            try:
                return_code = process.wait(timeout=hard_timeout)
            except subprocess.TimeoutExpired:
                timed_out = True
                self.terminate_process(process)
                return_code = process.returncode if process.returncode is not None else -1
            finally:
                with self.lock:
                    self.active_processes.pop(match["id"], None)
            canceled = self.cancel_requested.is_set()

        result, errors = self.result_errors(result_path, match)
        status, classification = self.classify_attempt(
            return_code=return_code,
            timed_out=timed_out,
            canceled=canceled,
            result=result,
            result_errors=errors,
            stdout_path=stdout_path,
            stderr_path=stderr_path,
        )
        replays, retained_support, artifact_errors = self.harvest_attempt_artifacts(
            match=match,
            attempt=attempt,
            status=status,
            support_dir=support_dir,
            match_dir=match_dir,
        )
        metadata = {
            "schemaVersion": SCHEMA_VERSION,
            "matchId": match["id"],
            "attempt": attempt,
            "startedUtc": started_utc,
            "endedUtc": utc_now(),
            "wallSeconds": round(time.monotonic() - started, 3),
            "returnCode": return_code,
            "status": status,
            "classification": classification,
            "timedOut": timed_out,
            "resultValid": result is not None and not errors,
            "resultErrors": errors,
            "stdout": stdout_path.name,
            "stderr": stderr_path.name,
            "command": [self.simulation_command.name, match["map"]],
            "endReason": result.get("endReason") if result else None,
            "replays": replays,
            "retainedSupport": retained_support,
            "artifactErrors": artifact_errors,
        }
        write_json_atomic(match_dir / f"attempt-{attempt}.json", metadata)
        self.log(
            f"Finished {match['id']} attempt {attempt}: "
            f"{status}/{classification} in {metadata['wallSeconds']:.3f}s."
        )
        return metadata

    def harvest_attempt_artifacts(
        self,
        *,
        match: dict[str, Any],
        attempt: int,
        status: str,
        support_dir: Path,
        match_dir: Path,
    ) -> tuple[list[str], str | None, list[str]]:
        if not support_dir.exists():
            return [], None, []

        sample_every = self.schedule["runner"]["successfulReplaySampleEvery"]
        preserve_replays = status != "completed" or (
            sample_every > 0 and match["index"] % sample_every == 0
        )
        preserved: list[str] = []
        errors: list[str] = []
        replay_paths = sorted(support_dir.rglob("*.orarep"))
        if preserve_replays:
            for replay_index, replay_path in enumerate(replay_paths, start=1):
                suffix = "" if len(replay_paths) == 1 else f"-{replay_index}"
                destination = match_dir / f"attempt-{attempt}{suffix}.orarep"
                try:
                    os.replace(replay_path, destination)
                    preserved.append(destination.name)
                except OSError as exc:
                    errors.append(
                        f"Could not preserve replay {replay_path.name}: {exc}"
                    )

        if status == "completed":
            try:
                shutil.rmtree(support_dir)
            except OSError as exc:
                errors.append(f"Could not remove support directory: {exc}")

        retained_support = support_dir.name if support_dir.exists() else None
        return preserved, retained_support, errors

    def run_match(self, match: dict[str, Any]) -> dict[str, Any]:
        match_dir = self.run_dir / "matches" / match["id"]
        match_dir.mkdir(parents=True, exist_ok=True)
        config_path = match_dir / "config.json"
        if config_path.exists():
            if read_json(config_path) != match:
                raise ValueError(f"Configuration drift for match {match['id']}.")
        else:
            write_json_atomic(config_path, match)

        disposition = self.existing_disposition(match)
        if disposition != "run":
            self.log(f"Skipping {match['id']}: {disposition}.")
            return read_json(match_dir / "status.json")

        max_attempts = 1 + self.schedule["runner"]["maxInfrastructureRetries"]
        first_attempt = self.attempt_number(match_dir)
        attempts = []
        final = None
        for offset in range(max_attempts):
            if self.cancel_requested.is_set():
                final = {
                    "status": "interrupted",
                    "classification": "external-cancel",
                }
                break
            attempt = first_attempt + offset
            final = self.run_attempt(match, match_dir, attempt)
            attempts.append(attempt)
            if final["status"] == "completed":
                break
            if final["classification"] not in INFRASTRUCTURE_FAILURES:
                break
            if offset + 1 < max_attempts:
                result_path = match_dir / "result.json"
                if result_path.exists():
                    os.replace(
                        result_path,
                        match_dir / f"attempt-{attempt}-result.json",
                    )
                self.log(
                    f"Retrying infrastructure failure for {match['id']}: "
                    f"{final['classification']}."
                )

        assert final is not None
        status = {
            "schemaVersion": SCHEMA_VERSION,
            "matchId": match["id"],
            "configFingerprint": match["configFingerprint"],
            "status": final["status"],
            "classification": final["classification"],
            "attemptsThisSession": attempts,
            "lastAttempt": max(attempts, default=first_attempt - 1),
            "updatedUtc": utc_now(),
            "endReason": final.get("endReason"),
            "wallSeconds": final.get("wallSeconds"),
        }
        write_json_atomic(match_dir / "status.json", status)
        return status

    def build_summary(self) -> dict[str, Any]:
        rows = []
        end_reasons: Counter[str] = Counter()
        natural_wins: Counter[str] = Counter()
        score_leads: Counter[str] = Counter()
        for match in self.schedule["matches"]:
            match_dir = self.run_dir / "matches" / match["id"]
            status_path = match_dir / "status.json"
            if status_path.is_file():
                status = read_json(status_path)
            else:
                status = {
                    "matchId": match["id"],
                    "status": "pending",
                    "classification": "pending",
                }
            rows.append(status)
            if status.get("status") != "completed":
                continue
            result, errors = self.result_errors(match_dir / "result.json", match)
            if errors or result is None:
                continue
            end_reasons[result["endReason"]] += 1
            natural_wins.update(
                winner["botType"] for winner in result["naturalWinners"]
            )
            if result["scoreLeader"]:
                score_leads[result["scoreLeader"]["botType"]] += 1

        counts = Counter(row["status"] for row in rows)
        current_session_seconds = time.monotonic() - self.started_monotonic
        prior_session_seconds = 0.0
        for session_path in (self.run_dir / "sessions").glob("*.json"):
            if session_path == self.session_path:
                continue
            try:
                elapsed = read_json(session_path).get("elapsedWallSeconds")
            except (OSError, json.JSONDecodeError):
                continue
            if isinstance(elapsed, (int, float)) and elapsed >= 0:
                prior_session_seconds += elapsed
        runtime_path = self.run_dir / "runtime.json"
        runtime = read_json(runtime_path) if runtime_path.is_file() else {}
        summary = {
            "schemaVersion": SCHEMA_VERSION,
            "runId": self.schedule["runId"],
            "scheduleHash": self.schedule["scheduleHash"],
            "startedUtc": runtime.get("createdUtc", self.started_utc),
            "updatedUtc": utc_now(),
            "elapsedWallSeconds": round(
                prior_session_seconds + current_session_seconds, 3
            ),
            "currentSessionElapsedWallSeconds": round(
                current_session_seconds, 3
            ),
            "matchCount": len(rows),
            "counts": dict(sorted(counts.items())),
            "endReasons": dict(sorted(end_reasons.items())),
            "naturalWins": dict(sorted(natural_wins.items())),
            "scoreLeads": dict(sorted(score_leads.items())),
            "matches": rows,
        }
        write_json_atomic(self.run_dir / "summary.json", summary)
        return summary

    def run(self) -> int:
        runnable = []
        for match in self.schedule["matches"]:
            disposition = self.existing_disposition(match)
            if disposition == "run":
                runnable.append(match)
            else:
                self.log(f"Preflight skip {match['id']}: {disposition}.")
        self.build_summary()

        workers = self.schedule["runner"]["workers"]
        with concurrent.futures.ThreadPoolExecutor(max_workers=workers) as executor:
            futures = {
                executor.submit(self.run_match, match): match for match in runnable
            }
            for future in concurrent.futures.as_completed(futures):
                match = futures[future]
                try:
                    future.result()
                except Exception as exc:
                    self.log(f"Runner error for {match['id']}: {exc}")
                    match_dir = self.run_dir / "matches" / match["id"]
                    match_dir.mkdir(parents=True, exist_ok=True)
                    write_json_atomic(
                        match_dir / "status.json",
                        {
                            "schemaVersion": SCHEMA_VERSION,
                            "matchId": match["id"],
                            "configFingerprint": match["configFingerprint"],
                            "status": "failed",
                            "classification": "runner-error",
                            "updatedUtc": utc_now(),
                            "detail": str(exc),
                        },
                    )
                self.build_summary()

        summary = self.build_summary()
        if self.cancel_requested.is_set():
            self.log("Batch interrupted.")
            return 130
        failed = sum(
            count
            for status, count in summary["counts"].items()
            if status != "completed"
        )
        if failed:
            self.log(f"Batch completed with {failed} non-completed matches.")
            return 2
        self.log("Batch completed successfully.")
        return 0


def parse_args(argv: list[str]) -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Run or resume a deterministic OpenHV simulation batch."
    )
    parser.add_argument("manifest", type=Path)
    parser.add_argument(
        "--results-root",
        type=Path,
        default=None,
        help="Root directory for run artifacts (default: ../simulation-runs).",
    )
    parser.add_argument(
        "--workers", type=int, help="Override manifest runner.workers."
    )
    parser.add_argument(
        "--max-infrastructure-retries",
        type=int,
        help="Override manifest runner.maxInfrastructureRetries.",
    )
    parser.add_argument(
        "--resume", action="store_true", help="Resume an existing run directory."
    )
    parser.add_argument(
        "--retry-failures",
        action="store_true",
        help="With --resume, retry terminal failed matches as new attempts.",
    )
    parser.add_argument(
        "--simulation-command",
        type=Path,
        default=None,
        help=argparse.SUPPRESS,
    )
    return parser.parse_args(argv)


def main(argv: list[str] | None = None) -> int:
    args = parse_args(argv or sys.argv[1:])
    project_dir = Path(__file__).resolve().parent
    manifest_path = args.manifest.expanduser().resolve()
    manifest_schema = project_dir / "schemas" / "simulation-batch-manifest-v1.schema.json"
    result_schema = project_dir / "schemas" / "simulation-result-v1.schema.json"
    manifest = read_json(manifest_path)
    validate_document(load_validator(manifest_schema), manifest, str(manifest_path))
    schedule = resolve_manifest(manifest)

    if args.workers is not None:
        if not 1 <= args.workers <= 64:
            raise ValueError("--workers must be between 1 and 64.")
        schedule["runner"]["workers"] = args.workers
    if args.max_infrastructure_retries is not None:
        if not 0 <= args.max_infrastructure_retries <= 5:
            raise ValueError("--max-infrastructure-retries must be between 0 and 5.")
        schedule["runner"][
            "maxInfrastructureRetries"
        ] = args.max_infrastructure_retries

    # Worker and retry overrides are execution controls, not synchronized
    # schedule inputs. Resume may safely change them.
    results_root = (
        args.results_root.expanduser().resolve()
        if args.results_root
        else (project_dir.parent / "simulation-runs").resolve()
    )
    run_dir = results_root / schedule["runId"]
    simulation_command = (
        args.simulation_command.expanduser().resolve()
        if args.simulation_command
        else project_dir / "run-simulation.sh"
    )
    if not simulation_command.is_file():
        raise ValueError(f"Simulation command does not exist: {simulation_command}")
    if os.name == "posix" and not os.access(simulation_command, os.X_OK):
        raise ValueError(f"Simulation command is not executable: {simulation_command}")

    runner = BatchRunner(
        project_dir=project_dir,
        run_dir=run_dir,
        schedule=schedule,
        result_validator=load_validator(result_schema),
        simulation_command=simulation_command,
        resume=args.resume,
        retry_failures=args.retry_failures,
    )
    runner.prepare(manifest_path)
    previous_handlers = {}
    for handled_signal in (signal.SIGINT, signal.SIGTERM):
        previous_handlers[handled_signal] = signal.signal(
            handled_signal, runner.request_cancel
        )
    exit_code = None
    try:
        exit_code = runner.run()
        return exit_code
    finally:
        runner.finalize_session(exit_code)
        for handled_signal, handler in previous_handlers.items():
            signal.signal(handled_signal, handler)


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (OSError, ValueError, json.JSONDecodeError) as exc:
        print(f"Batch runner error: {exc}", file=sys.stderr)
        raise SystemExit(2) from exc
