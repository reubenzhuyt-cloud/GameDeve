"""
千问 TTS：声音设计（qwen-voice-design）+ 语音合成（qwen3-tts-vd-2026-01-26）。
与 MiMo 的「style 标签」分离：角色音色由 voice_prompt 定义，合成时只传台词正文。

需：pip install dashscope requests（见 Tools/requirements-tts.txt）
环境变量：DASHSCOPE_API_KEY（北京地域 Key）
音色 ID 缓存在 Tools/qwen_vd_voices.json，避免重复付费创建。
"""
from __future__ import annotations

import fnmatch
import json
import os
import sys
import time
import urllib.error
import urllib.request
from pathlib import Path
from typing import Any

from mimo_tts import (
    ACTOR_NAMES,
    AUDIO_OUTPUT_DIR,
    BOSS_AUDIO_DIR,
    BOSS_TTS_FIXED_LINES,
    DIALOGUE_DIR,
    PROJECT_ROOT,
)

# 与声音设计文档一致：target_model 须与合成 model 一致
TARGET_MODEL_VD = "qwen3-tts-vd-2026-01-26"
CUSTOMIZATION_URL_CN = "https://dashscope.aliyuncs.com/api/v1/services/audio/tts/customization"
VOICE_CACHE_PATH = Path(__file__).resolve().parent / "qwen_vd_voices.json"


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

DASHSCOPE_API_KEY = os.getenv("DASHSCOPE_API_KEY", "")


# 每个键：preferred_name(≤16, 字母数字下划线), voice_prompt(客观多维描述), preview_text(试听句)
# 遵循文档：具体、多维、客观；不模仿名人。
VOICE_DESIGN_PRESETS: dict[str, tuple[str, str, str]] = {
    "0": (
        "vd_plr_main",
        "青年女性，普通话清晰，音调中等，语速中等偏稳，语气平和自然并略带好奇感，吐字清楚，适合古风冒险类主角对白。",
        "前面路还长，我想先问清楚再往前走。",
    ),
    "1": (
        "vd_mengpo_mc",
        "老年女性，语速偏慢，音色温和略带回声感，语气慈祥而神秘，句尾略拖，适合神怪题材中孟婆一类长者角色。",
        "过路的，喝下这碗汤，前尘旧事便都淡了。",
    ),
    "2": (
        "vd_weak_soul",
        "青年女性，声线虚弱飘渺，语速慢，气息不足带轻微叹息，音量偏小，适合濒死或残魂类角色。",
        "谢谢……这光……我已经……很久没见过了。",
    ),
    "3": (
        "vd_narrator",
        "中性声线，无明显性别色彩，语速平稳，吐字清晰，情绪克制，适合第三人称旁白或路人叙述。",
        "风从河面上掠过，远处传来隐约的钟声。",
    ),
    "5": (
        "vd_tenant_mc",
        "中年男性，普通话带轻微乡土质感，语速偏慢，语气憨厚、絮叨，略带喜感，适合佃户、村民类 NPC。",
        "小师傅你今儿来，可是为了粮仓上那张符？",
    ),
    "6": (
        "vd_roubao_mc",
        "青年女性，音色偏单薄轻柔，语速偏慢，语气怯懦压抑，句间略有停顿，气声略多，适合悲情向少女鬼魂对白。",
        "三年了……你是第一个能看见我的人。",
    ),
    "boss": (
        "vd_boss_fem",
        "青年女性，普通话标准，音域中等略偏低，情绪起伏时可带压抑、哽咽与低声怒意，也能收至极轻气声，适合玄幻游戏中人形 BOSS 的战斗与濒死台词。",
        "我不想害人……可我更不想被人害。",
    ),
    "default": (
        "vd_npc_default",
        "青年说话人，普通话标准，语速中等，语气自然，吐字清晰，适合一般 NPC 对白。",
        "你好，有什么事需要帮忙吗？",
    ),
}


def _actor_cache_key(actor_id: int) -> str:
    s = str(actor_id)
    return s if s in VOICE_DESIGN_PRESETS else "default"


def _load_cache() -> dict[str, Any]:
    if not VOICE_CACHE_PATH.is_file():
        return {"target_model": TARGET_MODEL_VD, "voices": {}}
    try:
        data = json.loads(VOICE_CACHE_PATH.read_text(encoding="utf-8"))
        if "voices" not in data:
            data["voices"] = {}
        data.setdefault("target_model", TARGET_MODEL_VD)
        return data
    except Exception:
        return {"target_model": TARGET_MODEL_VD, "voices": {}}


def _save_cache(data: dict[str, Any]) -> None:
    VOICE_CACHE_PATH.parent.mkdir(parents=True, exist_ok=True)
    VOICE_CACHE_PATH.write_text(json.dumps(data, ensure_ascii=False, indent=2), encoding="utf-8")


def create_designed_voice(
    preferred_name: str,
    voice_prompt: str,
    preview_text: str,
) -> str:
    """调用声音设计接口，返回可用于合成的 voice 名称。"""
    if not DASHSCOPE_API_KEY:
        raise RuntimeError("未设置 DASHSCOPE_API_KEY")

    payload = {
        "model": "qwen-voice-design",
        "input": {
            "action": "create",
            "target_model": TARGET_MODEL_VD,
            "voice_prompt": voice_prompt,
            "preview_text": preview_text,
            "preferred_name": preferred_name,
            "language": "zh",
        },
        "parameters": {"sample_rate": 24000, "response_format": "wav"},
    }
    req = urllib.request.Request(
        CUSTOMIZATION_URL_CN,
        data=json.dumps(payload).encode("utf-8"),
        headers={
            "Authorization": f"Bearer {DASHSCOPE_API_KEY}",
            "Content-Type": "application/json",
        },
        method="POST",
    )
    try:
        with urllib.request.urlopen(req, timeout=120) as r:
            raw = r.read().decode("utf-8")
    except urllib.error.HTTPError as e:
        err_body = e.read().decode("utf-8", errors="replace") if e.fp else ""
        raise RuntimeError(f"声音设计失败 HTTP {e.code}: {err_body[:1200]}") from e

    body = json.loads(raw)
    out = body.get("output") or {}
    voice = out.get("voice")
    if not voice:
        raise RuntimeError(f"声音设计响应无 voice 字段: {body!r}")
    return str(voice)


def ensure_voice_for_key(cache_key: str, *, force: bool = False) -> str:
    """保证缓存里已有 cache_key 对应音色；必要时创建。"""
    cache = _load_cache()
    voices: dict[str, Any] = cache["voices"]

    if not force and cache_key in voices and voices[cache_key].get("voice"):
        return str(voices[cache_key]["voice"])

    preset = VOICE_DESIGN_PRESETS.get(cache_key) or VOICE_DESIGN_PRESETS["default"]
    preferred_name, voice_prompt, preview_text = preset
    print(f"  [声音设计] {cache_key} -> preferred_name={preferred_name}")
    sys.stdout.flush()
    voice_id = create_designed_voice(preferred_name, voice_prompt, preview_text)
    voices[cache_key] = {
        "voice": voice_id,
        "preferred_name": preferred_name,
        "voice_prompt": voice_prompt,
    }
    cache["voices"] = voices
    _save_cache(cache)
    print(f"  [完成] voice={voice_id}")
    sys.stdout.flush()
    return voice_id


def run_voice_design(*, redesign: bool = False) -> None:
    if not DASHSCOPE_API_KEY:
        print("错误: 未设置 DASHSCOPE_API_KEY")
        sys.exit(1)

    keys = ["0", "1", "2", "3", "5", "6", "boss"]
    print("=" * 60)
    print("千问 声音设计（qwen-voice-design -> qwen3-tts-vd-2026-01-26）")
    print("=" * 60)

    if redesign:
        cache = _load_cache()
        cache["voices"] = {}
        _save_cache(cache)
        print("已清空本地音色缓存（将重新创建）。")

    for k in keys:
        ensure_voice_for_key(k, force=redesign)
        time.sleep(0.5)

    print(f"\n缓存文件: {VOICE_CACHE_PATH}")
    print("完成。")


def _extract_audio_url(resp: Any) -> str | None:
    if resp is None:
        return None
    if isinstance(resp, dict):
        out = resp.get("output") or {}
        aud = out.get("audio") if isinstance(out, dict) else None
        if isinstance(aud, dict):
            return aud.get("url")
        return None
    out = getattr(resp, "output", None)
    if out is None:
        return None
    aud = getattr(out, "audio", None)
    if aud is None and isinstance(out, dict):
        aud = out.get("audio")
    if isinstance(aud, dict):
        return aud.get("url")
    if aud is not None:
        return getattr(aud, "url", None)
    return None


def synthesize_vd(text: str, voice: str) -> bytes:
    """使用 qwen3-tts-vd-2026-01-26 非流式合成，返回音频字节（与 URL 内容一致，一般为 WAV）。"""
    if not DASHSCOPE_API_KEY:
        raise RuntimeError("未设置 DASHSCOPE_API_KEY")

    try:
        import dashscope
    except ImportError as e:
        raise RuntimeError("请安装 dashscope: pip install dashscope>=1.24.6") from e

    dashscope.base_http_api_url = "https://dashscope.aliyuncs.com/api/v1"

    resp = dashscope.MultiModalConversation.call(
        model=TARGET_MODEL_VD,
        api_key=DASHSCOPE_API_KEY,
        text=text,
        voice=voice,
        stream=False,
    )

    status = getattr(resp, "status_code", None)
    if status is not None and status != 200:
        msg = getattr(resp, "message", None) or str(resp)
        raise RuntimeError(f"合成失败 status={status}: {msg}")

    url = _extract_audio_url(resp)
    if not url:
        # 部分版本返回 dict
        if hasattr(resp, "dict"):
            try:
                url = _extract_audio_url(resp.dict() if callable(resp.dict) else {})
            except Exception:
                pass
        if not url:
            raise RuntimeError(f"合成响应无音频 URL: {resp!r}")

    req = urllib.request.Request(url, headers={"User-Agent": "Mozilla/5.0"})
    with urllib.request.urlopen(req, timeout=120) as r:
        return r.read()


def _voice_id_for_actor(actor_id: int) -> str:
    cache = _load_cache()
    key = _actor_cache_key(actor_id)
    entry = cache["voices"].get(key)
    if entry and entry.get("voice"):
        return str(entry["voice"])
    return ensure_voice_for_key(key, force=False)


def _voice_id_for_boss() -> str:
    cache = _load_cache()
    entry = cache["voices"].get("boss")
    if entry and entry.get("voice"):
        return str(entry["voice"])
    return ensure_voice_for_key("boss", force=False)


def process_dialogue_file_vd(
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
        "tts_backend": "qwen3-tts-vd + voice-design",
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
            vid = _voice_id_for_actor(actor_id)
            audio_bytes = synthesize_vd(dialogue_text, vid)
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


def run_dialogue_batch_vd(
    *,
    file_pattern: str | None = None,
    include_tests: bool = False,
    delay: float = 0.5,
    skip_existing: bool = False,
    ensure_voices: bool = True,
) -> None:
    if not DASHSCOPE_API_KEY:
        print("错误: 未设置 DASHSCOPE_API_KEY（.env 或环境变量）")
        sys.exit(1)

    if ensure_voices:
        print("检查/创建声音设计音色（首次会按量计费创建）…")
        for k in ["0", "1", "2", "3", "5", "6"]:
            ensure_voice_for_key(k, force=False)
            time.sleep(0.35)

    print("=" * 60)
    print("千问 VD TTS — 对话批量")
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
        result = process_dialogue_file_vd(
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


def run_boss_batch_vd(
    *,
    stems: list[str] | None = None,
    delay: float = 0.35,
) -> None:
    if not DASHSCOPE_API_KEY:
        print("错误: 未设置 DASHSCOPE_API_KEY")
        sys.exit(1)

    print("确保 Boss 声音设计音色存在…")
    _voice_id_for_boss()
    time.sleep(0.35)

    BOSS_AUDIO_DIR.mkdir(parents=True, exist_ok=True)
    lines = BOSS_TTS_FIXED_LINES
    if stems:
        want = set(stems)
        lines = [t for t in lines if t[0] in want]
        missing = want - {t[0] for t in lines}
        if missing:
            print(f"警告: 未识别的 stem: {missing}")

    vid = _voice_id_for_boss()
    print(f"Boss 千问 VD -> {BOSS_AUDIO_DIR} (voice={vid})")

    for stem, text, _style in lines:
        out = BOSS_AUDIO_DIR / f"{stem}.wav"
        print(f"  -> {out.name}")
        sys.stdout.flush()
        try:
            audio_bytes = synthesize_vd(text, vid)
            out.write_bytes(audio_bytes)
            print(f"     已保存 ({len(audio_bytes)} bytes)")
        except Exception as e:
            print(f"     失败: {e}")
        time.sleep(delay)
    print("完成。")
