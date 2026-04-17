#!/usr/bin/env python3
"""
统一 TTS 生成入口（MiMo 对话 / Boss，Edge Boss 备用）。

项目根目录执行示例：
  python Tools/generate_tts.py dialogue
  python Tools/generate_tts.py dialogue --file "MengPo*.json" --skip-existing
  python Tools/generate_tts.py dialogue --delay 0.3 --include-tests
  python Tools/generate_tts.py boss
  python Tools/generate_tts.py boss --only boss_battle_start boss_battle_hurt
  python Tools/generate_tts.py edge-boss
  python Tools/generate_tts.py edge-smoke
"""
from __future__ import annotations

import argparse
import sys
from pathlib import Path

_TOOLS = Path(__file__).resolve().parent
if str(_TOOLS) not in sys.path:
    sys.path.insert(0, str(_TOOLS))


def _cmd_dialogue(ns: argparse.Namespace) -> None:
    from mimo_tts import run_dialogue_batch

    run_dialogue_batch(
        file_pattern=ns.file,
        include_tests=ns.include_tests,
        delay=ns.delay,
        skip_existing=ns.skip_existing,
    )


def _cmd_boss(ns: argparse.Namespace) -> None:
    from mimo_tts import run_boss_mimo_batch

    run_boss_mimo_batch(stems=ns.only, delay=ns.delay)


def _cmd_edge_boss(ns: argparse.Namespace) -> None:
    from edge_tts import run_edge_boss_batch

    run_edge_boss_batch(stems=ns.only, delay=ns.delay)


def _cmd_edge_smoke(_ns: argparse.Namespace) -> None:
    from edge_tts import synthesize_line_to_mp3

    out = _TOOLS / "edge_test.mp3"
    p = synthesize_line_to_mp3("你好，这是 Edge 通道冒烟测试。", out)
    print(f"OK: {p}")


def build_parser() -> argparse.ArgumentParser:
    p = argparse.ArgumentParser(
        description="对话 / Boss 语音批量生成（MiMo 或 Edge）",
    )
    sub = p.add_subparsers(dest="command", required=True)

    d = sub.add_parser("dialogue", help="扫描 Dialogue/*.json，按 actor 风格生成 WAV")
    d.add_argument(
        "--file",
        metavar="GLOB",
        help="只处理匹配的文件名，如 MengPo*.json",
    )
    d.add_argument(
        "--include-tests",
        action="store_true",
        help="包含 test 开头的 JSON（默认跳过）",
    )
    d.add_argument(
        "--delay",
        type=float,
        default=0.5,
        help="每条请求间隔秒数，防限流（默认 0.5）",
    )
    d.add_argument(
        "--skip-existing",
        action="store_true",
        help="目标 WAV 已存在则跳过该节点",
    )
    d.set_defaults(_run=_cmd_dialogue)

    b = sub.add_parser("boss", help="Boss 战六句 MiMo WAV（Resources/Audio/BossBattle）")
    b.add_argument(
        "--only",
        nargs="+",
        metavar="STEM",
        help="只生成指定 stem，如 boss_battle_start",
    )
    b.add_argument("--delay", type=float, default=0.35, help="每条间隔秒数")
    b.set_defaults(_run=_cmd_boss)

    e = sub.add_parser("edge-boss", help="Boss 六句 Edge MP3 备用（无 MiMo key 时）")
    e.add_argument("--only", nargs="+", metavar="STEM", help="只生成指定 stem")
    e.add_argument("--delay", type=float, default=0.35)
    e.set_defaults(_run=_cmd_edge_boss)

    s = sub.add_parser("edge-smoke", help="生成 Tools/edge_test.mp3 测 Edge 通道")
    s.set_defaults(_run=_cmd_edge_smoke)

    return p


def main(args: list[str] | None = None) -> None:
    parser = build_parser()
    ns = parser.parse_args(args)
    ns._run(ns)


if __name__ == "__main__":
    main()
