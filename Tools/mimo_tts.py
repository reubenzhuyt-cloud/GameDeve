"""
MiMo-V2-TTS：环境加载、请求体、对话/Boss 配置与批量生成逻辑。
命令行入口请使用同目录下的 generate_tts.py。
"""
from __future__ import annotations

import base64
import fnmatch
import json
import os
import sys
import time
import urllib.error
import urllib.request
import wave
from pathlib import Path

PROJECT_ROOT = Path(__file__).resolve().parent.parent


def _load_env_file() -> None:
    env_path = PROJECT_ROOT / ".env"
    if not env_path.is_file():
        return
    for raw in env_path.read_text(encoding="utf-8").splitlines():
        line = raw.strip()
        if not line or line.startswith("#") or "=" not in line:
            continue
        key, _, val = line.partition("=")
        key = key.strip()
        val = val.strip().strip('"').strip("'")
        if key and key not in os.environ:
            os.environ[key] = val


try:
    from dotenv import load_dotenv

    load_dotenv(PROJECT_ROOT / ".env")
except ImportError:
    _load_env_file()

MIMO_API_KEY = os.getenv("MIMO_API_KEY", "")
MIMO_BASE_URL = "https://api.xiaomimimo.com/v1"
MIMO_TTS_URL = os.getenv("MIMO_TTS_URL", f"{MIMO_BASE_URL}/chat/completions")
MIMO_TTS_LEGACY_BRACKET = os.getenv("MIMO_TTS_LEGACY_BRACKET", "").strip() in ("1", "true", "yes")

DIALOGUE_DIR = PROJECT_ROOT / "Assets" / "Resources" / "Dialogue"
AUDIO_OUTPUT_DIR = PROJECT_ROOT / "Assets" / "Resources" / "Audio" / "Dialogue"
BOSS_AUDIO_DIR = PROJECT_ROOT / "Assets" / "Resources" / "Audio" / "BossBattle"

# 短词 + 空格；与 MiMo 文档示例一致
ACTOR_STYLES: dict[int, str] = {
    0: "年轻女 平和 好奇",
    1: "老年女 缓慢 慈祥 神秘",
    2: "虚弱 飘渺 叹息",
    3: "中性 平静",
}

ACTOR_NAMES: dict[int, str] = {
    0: "Player",
    1: "MengPo",
    2: "WeakSoul",
    3: "Narrator",
}

# Boss 配音：正文只用「界面纯台词」（与 BossBattleDialogueBox 常量一致），语气走 style，
# 由 call_mimo_tts 拼成 <style>...</style>+正文，避免把（括号说明）念进语音。
BOSS_TTS_FIXED_LINES: list[tuple[str, str, str]] = [
    (
        "boss_battle_start",
        "我不想害人。可我更不想被人害。",
        "哭腔 压抑 愤怒 低声",
    ),
    (
        "boss_battle_cast_skill_1",
        "你们……都有道理！",
        "哭腔 气笑 呛人",
    ),
    (
        "boss_battle_cast_skill_2",
        "别想带我走",
        "哭腔 压低 喊",
    ),
    (
        "boss_battle_hurt",
        "我不疼……我不疼……",
        "忍痛 发抖 嘴硬 轻声",
    ),
    (
        "boss_battle_phase2",
        "我死过一次了。我不怕。",
        "冷淡 鼻音 坚定",
    ),
    (
        "boss_battle_near_death",
        "天……快亮了……",
        "气若游丝 极轻",
    ),
]

SAMPLE_RATE = 24000


def _escape_style_tag_inner(s: str) -> str:
    return (
        s.replace("&", "&amp;")
        .replace("<", "&lt;")
        .replace(">", "&gt;")
    )


def _normalize_style_text(s: str) -> str:
    for ch in "，、。；：！？":
        s = s.replace(ch, " ")
    return " ".join(s.split())


def create_wav_file(pcm_data: bytes, output_path: str | Path, sample_rate: int = SAMPLE_RATE) -> None:
    output_path = Path(output_path)
    with wave.open(str(output_path), "wb") as wav_file:
        wav_file.setnchannels(1)
        wav_file.setsampwidth(2)
        wav_file.setframerate(sample_rate)
        wav_file.writeframes(pcm_data)


def call_mimo_tts(text: str, style: str = "") -> bytes:
    """单条合成；风格走 `<style>...</style>` + assistant（见官方 speech-synthesis 文档）。"""
    headers = {
        "Content-Type": "application/json",
        "api-key": MIMO_API_KEY,
    }

    if MIMO_TTS_LEGACY_BRACKET:
        content = f"[{_normalize_style_text(style)}] {text}" if style else text
    elif style:
        st = _escape_style_tag_inner(_normalize_style_text(style.strip()))
        content = f"<style>{st}</style>{text}"
    else:
        content = text
    messages = [{"role": "assistant", "content": content}]

    payload = {
        "model": "mimo-v2-tts",
        "messages": messages,
        "audio": {"format": "pcm16", "voice": "default_zh"},
        "stream": False,
    }

    try:
        req = urllib.request.Request(
            MIMO_TTS_URL,
            data=json.dumps(payload).encode("utf-8"),
            headers=headers,
            method="POST",
        )
        with urllib.request.urlopen(req, timeout=120) as response:
            status = response.status
            raw = response.read()
            if status != 200:
                print(f"    Error: API returned status {status}")
                return b""
            data = json.loads(raw.decode("utf-8"))
            if "error" in data:
                print(f"    API Error: {data['error']}")
                return b""
            if "choices" in data and len(data["choices"]) > 0:
                message = data["choices"][0].get("message", {})
                audio = message.get("audio", {})
                audio_data = audio.get("data", "")
                if audio_data:
                    return base64.b64decode(audio_data)
                print("    No audio data in response")
            else:
                print("    No choices in response")
            return b""
    except urllib.error.HTTPError as e:
        print(f"    HTTP error: {e.code} - {e.reason}")
        try:
            print(f"    Error body: {e.read().decode('utf-8')[:800]}")
        except Exception:
            pass
        return b""
    except urllib.error.URLError as e:
        print(f"    URL error: {e}")
        return b""
    except Exception as e:
        print(f"    Request error: {type(e).__name__}: {e}")
        return b""


def process_dialogue_file(
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
        style = ACTOR_STYLES.get(actor_id, "自然 流畅")

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

        pcm_data = call_mimo_tts(dialogue_text, style)

        if pcm_data:
            create_wav_file(pcm_data, output_path)
            print(f"    -> 已保存: {output_filename} ({len(pcm_data)} bytes)")
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
        else:
            print("    -> 生成失败!")
            sys.stdout.flush()

        time.sleep(delay)

    return results


def run_dialogue_batch(
    *,
    file_pattern: str | None = None,
    include_tests: bool = False,
    delay: float = 0.5,
    skip_existing: bool = False,
) -> None:
    if not MIMO_API_KEY:
        print("错误: 未设置 MIMO_API_KEY（.env 或环境变量）")
        sys.exit(1)

    print("=" * 60)
    print("MIMO TTS — 对话批量")
    print("=" * 60)
    AUDIO_OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    print(f"音频输出目录: {AUDIO_OUTPUT_DIR}")

    dialogue_files = sorted(DIALOGUE_DIR.glob("*.json"))
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
        result = process_dialogue_file(
            dialogue_file,
            skip_existing=skip_existing,
            delay=delay,
        )
        all_results.append(result)

    manifest_path = AUDIO_OUTPUT_DIR / "dialogue_audio_manifest.json"
    with open(manifest_path, "w", encoding="utf-8") as f:
        json.dump(all_results, f, ensure_ascii=False, indent=2)
    print("\n" + "=" * 60)
    print("配音生成完成!")
    print(f"清单: {manifest_path}")
    print("=" * 60)


def run_boss_mimo_batch(
    *,
    stems: list[str] | None = None,
    delay: float = 0.35,
) -> None:
    if not MIMO_API_KEY:
        print("错误: 未设置 MIMO_API_KEY")
        sys.exit(1)

    BOSS_AUDIO_DIR.mkdir(parents=True, exist_ok=True)
    lines = BOSS_TTS_FIXED_LINES
    if stems:
        want = set(stems)
        lines = [t for t in lines if t[0] in want]
        missing = want - {t[0] for t in lines}
        if missing:
            print(f"警告: 未识别的 stem: {missing}")

    print(f"Boss MiMo -> {BOSS_AUDIO_DIR}")
    for stem, text, style in lines:
        out = BOSS_AUDIO_DIR / f"{stem}.wav"
        print(f"  -> {out.name}")
        sys.stdout.flush()
        pcm = call_mimo_tts(text, style)
        if not pcm:
            print("     失败")
            continue
        create_wav_file(pcm, out)
        print(f"     已保存 ({len(pcm)} bytes)")
        for ext in (".mp3", ".mp3.meta"):
            legacy = BOSS_AUDIO_DIR / f"{stem}{ext}"
            if legacy.is_file():
                legacy.unlink()
        time.sleep(delay)
    print("完成。")
