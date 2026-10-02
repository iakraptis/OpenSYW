"""OpenSY test suite runner: build, YAML check, then every automated test map in sequence.

Usage (from the repository root):
  python run_tests.py                 # build + check-yaml + all maps
  python run_tests.py --only start-test,combat-test
  python run_tests.py --no-build --no-check

Each map is launched with Launch.Map=<map>. A map PASSES when its expected number of "... TEST PASS" lines has
appeared (C# harness tests print to stdout, Lua tests to %APPDATA%/OpenRA/Logs/lua.log). It FAILS on a
"TEST FAIL"/"FAIL:" line, an exception on stderr, a fatal Lua error, a new exception log, or a timeout.
Logs and summary.md are written to test-results/. Run only at major milestones. The maps need the game assets
generated from an original copy of Seven Years War.
The suite (2026-09-28): start, build, combat, spell, naval-air, capture-victory and bot tests; they share
testlib.lua and the SywTest Lua helpers (OpenRA.Mods.Syw/Tests/SywTestGlobal.cs).
"""
import argparse
import os
import re
import subprocess
import sys
import time
from pathlib import Path

SDK = Path(__file__).resolve().parent
ENGINE = SDK / 'engine'
MAPS = SDK / 'mods' / 'syw' / 'maps'
RESULTS = SDK / 'test-results'
LOGS = Path(os.environ.get('APPDATA', '')) / 'OpenRA' / 'Logs'

# Maps that are not automated tests.
MANUAL = set()

# Number of "TEST PASS" lines a map prints when everything passed (default 1).
EXPECTED_PASSES = {}

DEFAULT_TIMEOUT = 180  # seconds of wall time, including game start-up
TIMEOUTS = {'bot-test': 900, 'naval-air-test': 300, 'combat-test': 300, 'start-test': 300, 'build-test': 300}

FAIL_PATTERN = re.compile(r'TEST FAIL|FAIL:|Fatal Lua Error')


def read(path: Path) -> str:
    try:
        return path.read_text(encoding='utf-8', errors='replace')
    except OSError:
        return ''


def kill_openra():
    subprocess.run(['taskkill', '/F', '/IM', 'OpenRA.exe'], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)


def build():
    print('Building OpenRA.Mods.Syw ...', flush=True)
    r = subprocess.run(['dotnet', 'build', str(SDK / 'OpenRA.Mods.Syw'), '-c', 'Debug', '--nologo', '-v', 'q'],
                       capture_output=True, text=True)
    errors = sorted({line.strip() for line in r.stdout.splitlines() if ' error ' in line})
    if r.returncode != 0 or errors:
        print('\n'.join(errors) or r.stdout[-2000:])
        return False
    return True


def check_yaml():
    print('Checking YAML ...', flush=True)
    r = subprocess.run(['cmd', '/c', str(SDK / 'make.cmd'), 'test'], cwd=SDK, capture_output=True, text=True,
                       stdin=subprocess.DEVNULL)
    errors = [line for line in r.stdout.splitlines() if re.search(r'error|exception', line, re.I)]
    for line in errors:
        print('  ' + line)
    return not errors


def run_map(name: str):
    RESULTS.mkdir(exist_ok=True)
    out_path, err_path = RESULTS / f'{name}-stdout.txt', RESULTS / f'{name}-stderr.txt'
    lua_log = LOGS / 'lua.log'
    lua_log.unlink(missing_ok=True)
    started = time.time()
    expected = EXPECTED_PASSES.get(name, 1)
    timeout = TIMEOUTS.get(name, DEFAULT_TIMEOUT)

    with open(out_path, 'w') as out, open(err_path, 'w') as err:
        proc = subprocess.Popen([str(ENGINE / 'bin' / 'OpenRA.exe'), 'Game.Mod=syw', 'Engine.EngineDir=..',
                                 'Engine.ModSearchPaths=../mods,./mods', f'Launch.Map={name}'],
                                cwd=ENGINE, stdout=out, stderr=err)
        verdict, detail = None, ''
        while verdict is None:
            time.sleep(1)
            text = read(out_path) + '\n' + read(lua_log)
            stderr = read(err_path)
            crash_logs = [p for p in LOGS.glob('exception-*.log') if p.stat().st_mtime >= started]
            fail = FAIL_PATTERN.search(text)
            if 'Exception' in stderr:
                verdict, detail = 'FAIL', next((l for l in stderr.splitlines() if 'Exception' in l), '')
            elif fail:
                verdict, detail = 'FAIL', next(l for l in text.splitlines() if FAIL_PATTERN.search(l))
            elif crash_logs:
                verdict, detail = 'FAIL', f'crash log {crash_logs[0].name}: ' + next(
                    (l for l in read(crash_logs[0]).splitlines() if 'Exception' in l), '')
            elif sum('TEST PASS' in l for l in text.splitlines()) >= expected:
                verdict = 'PASS'
            elif proc.poll() is not None:
                verdict, detail = 'FAIL', f'game exited (code {proc.returncode}) before passing'
            elif time.time() - started > timeout:
                verdict, detail = 'FAIL', f'timeout after {timeout}s; last output: ' + (text.strip().splitlines() or [''])[-1]
        proc.kill()
        proc.wait()
        # lua.log is only flushed when the game closes; re-check it for errors the live read missed.
        late = read(lua_log)
        if verdict == 'PASS' and FAIL_PATTERN.search(late):
            verdict, detail = 'FAIL', next(l for l in late.splitlines() if FAIL_PATTERN.search(l))
        elif verdict == 'FAIL' and 'Fatal Lua Error' in late and 'Lua' not in detail:
            detail += ' | ' + next(l for l in late.splitlines() if 'Fatal Lua Error' in l)

    (RESULTS / f'{name}-lua.txt').write_text(read(lua_log), encoding='utf-8')
    return verdict, detail.strip()[:300], time.time() - started


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument('--only', help='comma-separated map names')
    parser.add_argument('--no-build', action='store_true')
    parser.add_argument('--no-check', action='store_true')
    args = parser.parse_args()

    kill_openra()
    if not args.no_build and not build():
        sys.exit('Build failed')
    yaml_ok = True if args.no_check else check_yaml()

    maps = args.only.split(',') if args.only else sorted(
        p.name for p in MAPS.iterdir() if p.name.endswith('-test') and p.name not in MANUAL)
    results = []
    for name in maps:
        print(f'{name:22} ...', end=' ', flush=True)
        verdict, detail, seconds = run_map(name)
        print(f'{verdict} ({seconds:.0f}s) {detail}', flush=True)
        results.append((name, verdict, seconds, detail))

    passed = sum(v == 'PASS' for _, v, _, _ in results)
    lines = ['# Test suite results', '', f'YAML check: {"PASS" if yaml_ok else "FAIL"}', '',
             f'{passed}/{len(results)} maps passed', '', '| Map | Result | Time | Detail |', '|---|---|---:|---|']
    lines += [f'| {n} | {v} | {s:.0f}s | {d.replace("|", "/")} |' for n, v, s, d in results]
    (RESULTS / 'summary.md').write_text('\n'.join(lines) + '\n', encoding='utf-8')
    print(f'\n{passed}/{len(results)} passed, YAML check {"PASS" if yaml_ok else "FAIL"}; see {RESULTS / "summary.md"}')
    sys.exit(0 if passed == len(results) and yaml_ok else 1)


if __name__ == '__main__':
    main()
