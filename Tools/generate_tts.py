#!/usr/bin/env python3
"""
统一 TTS 生成入口：阿里云千问「声音设计 + qwen3-tts-vd」合成（对话 + Boss）。
（MiMo 仍保留在 mimo_tts.py，需时可直接调用。）

环境变量：DASHSCOPE_API_KEY（北京地域）
依赖：pip install -r Tools/requirements-tts.txt

项目根目录执行示例：
  python Tools/generate_tts.py design-voices
  python Tools/generate_tts.py design-voices --redesign
  python Tools/generate_tts.py dialogue
  python Tools/generate_tts.py dialogue --file "MengPo*.json" --skip-existing
  python Tools/generate_tts.py dialogue --no-ensure-voices
  python Tools/generate_tts.py boss
  python Tools/generate_tts.py boss --only boss_battle_start boss_battle_hurt
"""
from __future__ import annotations

import argparse
import sys
from pathlib import Path

_TOOLS = Path(__file__).resolve().parent
if str(_TOOLS) not in sys.path:
    sys.path.insert(0, str(_TOOLS))


def _cmd_dialogue(ns: argparse.Namespace) -> None:
    from qwen_vd_tts import run_dialogue_batch_vd

    run_dialogue_batch_vd(
        file_pattern=ns.file,
        include_tests=ns.include_tests,
        delay=ns.delay,
        skip_existing=ns.skip_existing,
        ensure_voices=not ns.no_ensure_voices,
    )


def _cmd_boss(ns: argparse.Namespace) -> None:
    from qwen_vd_tts import run_boss_batch_vd

    run_boss_batch_vd(stems=ns.only, delay=ns.delay)


def _cmd_design_voices(ns: argparse.Namespace) -> None:
    from qwen_vd_tts import run_voice_design

    run_voice_design(redesign=ns.redesign)


def build_parser() -> argparse.ArgumentParser:
    p = argparse.ArgumentParser(
        description="对话 / Boss 语音批量生成（千问声音设计 + qwen3-tts-vd）",
    )
    sub = p.add_subparsers(dest="command", required=True)

    v = sub.add_parser(
        "design-voices",
        help="仅为各角色创建声音设计音色（写入 Tools/qwen_vd_voices.json）",
    )
    v.add_argument(
        "--redesign",
        action="store_true",
        help="清空缓存并重新创建全部音色（会再次按次计费）",
    )
    v.set_defaults(_run=_cmd_design_voices)

    d = sub.add_parser("dialogue", help="递归扫描 Dialogue 下 *.json，按角色专属音色生成 WAV")
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
    d.add_argument(
        "--no-ensure-voices",
        action="store_true",
        help="不自动检查/创建缺失的角色音色（需已运行 design-voices）",
    )
    d.set_defaults(_run=_cmd_dialogue)

    b = sub.add_parser("boss", help="Boss 战六句 WAV（Resources/Audio/BossBattle，专属 Boss 设计音色）")
    b.add_argument(
        "--only",
        nargs="+",
        metavar="STEM",
        help="只生成指定 stem，如 boss_battle_start",
    )
    b.add_argument("--delay", type=float, default=0.35, help="每条间隔秒数")
    b.set_defaults(_run=_cmd_boss)

    return p


def main(args: list[str] | None = None) -> None:
    parser = build_parser()
    ns = parser.parse_args(args)
    ns._run(ns)


if __name__ == "__main__":
    main()
