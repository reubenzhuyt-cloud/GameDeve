"""
千问 TTS：qwen3-tts-flash + 系统音色（无 instructions，账号未开通 Instruct 时也可用）。
若需指令控制可改用 qwen3-tts-instruct-flash（需在百炼开通对应模型）。

环境变量：DASHSCOPE_API_KEY
"""
from __future__ import annotations

import fnmatch
import json
import sys
import time
import urllib.request
from pathlib import Path

from mimo_tts import (
    ACTOR_NAMES,
    AUDIO_OUTPUT_DIR,
    BOSS_AUDIO_DIR,
    BOSS_TTS_FIXED_LINES,
    DIALOGUE_DIR,
    merge_dialogue_manifest_updates,
)

from qwen_vd_tts import DASHSCOPE_API_KEY, _extract_audio_url

MODEL_FLASH = "qwen3-tts-flash"

# 系统音色（与阿里云文档一致）；按角色区分声线
# actor 1 孟婆：Bellona（燕铮莺），千问3-TTS 角色音色。
ACTOR_VOICES: dict[int, str] = {
    0: "Cherry",
    1: "Bellona",
    2: "Seren",
    # 3=渔夫：与 5 佃户同款男声（Arthur）
    3: "Arthur",
    5: "Arthur",
    6: "Mia",
}

BOSS_VOICE = "Serena"


def synthesize_flash(text: str, voice: str) -> bytes:
    if not DASHSCOPE_API_KEY:
        raise RuntimeError("未设置 DASHSCOPE_API_KEY")

    try:
        import dashscope
    except ImportError as e:
        raise RuntimeError("请安装 dashscope: pip install dashscope>=1.24.6") from e

    dashscope.base_http_api_url = "https://dashscope.aliyuncs.com/api/v1"

    resp = dashscope.MultiModalConversation.call(
        model=MODEL_FLASH,
        api_key=DASHSCOPE_API_KEY,
        text=text,
        voice=voice,
        language_type="Chinese",
        stream=False,
    )

    status = getattr(resp, "status_code", None)
    if status is not None and status != 200:
        msg = getattr(resp, "message", None) or str(resp)
        raise RuntimeError(f"合成失败 status={status}: {msg}")

    url = _extract_audio_url(resp)
    if not url and hasattr(resp, "dict"):
        try:
            url = _extract_audio_url(resp.dict() if callable(resp.dict) else {})
        except Exception:
            pass
    if not url:
        raise RuntimeError(f"合成响应无音频 URL: {resp!r}")

    req = urllib.request.Request(url, headers={"User-Agent": "Mozilla/5.0"})
    with urllib.request.urlopen(req, timeout=120) as r:
        return r.read()


def process_dialogue_file_instruct(
    dialogue_path: Path,
    *,
    skip_existing: bool = False,
    delay: float = 0.5,
) -> dict:
    with open(dialogue_path, encoding="utf-8") as f:
        dialogue_data = json.load(f)

    dialogue_name = dialogue_data.get("DialogueName", dialogue_path.stem)
    nodes = dialogue_data.get("nodes", [])

    results: dict = {
        "dialogue_name": dialogue_name,
        "file_name": dialogue_path.stem,
        "audio_files": [],
        "tts_backend": MODEL_FLASH,
    }

    print(f"\n处理对话: {dialogue_name} ({dialogue_path.name})")

    for node in nodes:
        node_id = node.get("nodeId", -1)
        actor_id = int(node.get("actorId", 0))
        dialogue_text = node.get("dialogueText", "")
        is_choice_node = node.get("isChoiceNode", False)

        if not dialogue_text.strip() or is_choice_node:
            continue

        actor_name = ACTOR_NAMES.get(actor_id, f"Actor_{actor_id}")
        voice = ACTOR_VOICES.get(actor_id, "Cherry")

        output_filename = f"{dialogue_path.stem}_node{node_id}.wav"
        output_path = AUDIO_OUTPUT_DIR / output_filename

        if skip_existing and output_path.is_file():
            print(f"  节点 {node_id} (角色: {actor_name}): 跳过，已存在 {output_filename}")
            sys.stdout.flush()
            time.sleep(delay)
            continue

        preview = dialogue_text[:30] + ("..." if len(dialogue_text) > 30 else "")
        print(f"  节点 {node_id} (角色: {actor_name}): {preview}")
        sys.stdout.flush()

        try:
            audio_bytes = synthesize_flash(dialogue_text, voice)
            output_path.write_bytes(audio_bytes)
            print(f"    -> 已保存: {output_filename} ({len(audio_bytes)} bytes)")
            sys.stdout.flush()
            results["audio_files"].append(
                {
                    "node_id": node_id,
                    "actor_id": actor_id,
                    "actor_name": actor_name,
                    "filename": output_filename,
                    "text": dialogue_text,
                }
            )
        except Exception as e:
            print(f"    -> 失败: {e}")
            sys.stdout.flush()

        time.sleep(delay)

    return results


def run_dialogue_batch_instruct(
    *,
    file_pattern: str | None = None,
    include_tests: bool = False,
    delay: float = 0.5,
    skip_existing: bool = False,
) -> None:
    if not DASHSCOPE_API_KEY:
        print("错误: 未设置 DASHSCOPE_API_KEY（.env 或环境变量）")
        sys.exit(1)

    print("=" * 60)
    print(f"{MODEL_FLASH} — 对话批量")
    print("=" * 60)
    AUDIO_OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    print(f"音频输出目录: {AUDIO_OUTPUT_DIR}")

    dialogue_files = sorted(DIALOGUE_DIR.rglob("*.json"))
    if file_pattern:
        dialogue_files = [p for p in dialogue_files if fnmatch.fnmatch(p.name, file_pattern)]

    if not dialogue_files:
        print("没有匹配的对话 JSON（检查 --file 或 Dialogue 目录）。")
        return

    print(f"待处理对话文件: {len(dialogue_files)} 个")
    all_results: list = []

    for dialogue_file in dialogue_files:
        if not include_tests and dialogue_file.name.lower().startswith("test"):
            print(f"\n跳过测试文件: {dialogue_file.name}")
            continue
        result = process_dialogue_file_instruct(
            dialogue_file,
            skip_existing=skip_existing,
            delay=delay,
        )
        all_results.append(result)

    manifest_path = AUDIO_OUTPUT_DIR / "dialogue_audio_manifest.json"
    if file_pattern:
        all_results = merge_dialogue_manifest_updates(manifest_path, all_results)
    with open(manifest_path, "w", encoding="utf-8") as f:
        json.dump(all_results, f, ensure_ascii=False, indent=2)
    print("\n" + "=" * 60)
    print("配音生成完成!")
    print(f"清单: {manifest_path}")
    print("=" * 60)


def run_boss_batch_instruct(
    *,
    stems: list[str] | None = None,
    delay: float = 0.35,
) -> None:
    if not DASHSCOPE_API_KEY:
        print("错误: 未设置 DASHSCOPE_API_KEY")
        sys.exit(1)

    BOSS_AUDIO_DIR.mkdir(parents=True, exist_ok=True)
    lines = BOSS_TTS_FIXED_LINES
    if stems:
        want = set(stems)
        lines = [t for t in lines if t[0] in want]
        missing = want - {t[0] for t in lines}
        if missing:
            print(f"警告: 未识别的 stem: {missing}")

    print(f"Boss {MODEL_FLASH} -> {BOSS_AUDIO_DIR} (voice={BOSS_VOICE})")

    for stem, text, _style in lines:
        out = BOSS_AUDIO_DIR / f"{stem}.wav"
        print(f"  -> {out.name}")
        sys.stdout.flush()
        try:
            audio_bytes = synthesize_flash(text, BOSS_VOICE)
            out.write_bytes(audio_bytes)
            print(f"     已保存 ({len(audio_bytes)} bytes)")
        except Exception as e:
            print(f"     失败: {e}")
        time.sleep(delay)
    print("完成。")
