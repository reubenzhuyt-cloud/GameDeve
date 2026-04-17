"""
Microsoft Edge 在线 TTS（无密钥）：token + SSML，用于 Boss mp3 备用。
入口见 generate_tts.py edge-boss。
"""
from __future__ import annotations

import base64
import datetime as dt
import hashlib
import hmac
import json
import time
import urllib.parse
import urllib.request
import uuid
from pathlib import Path

PROJECT_ROOT = Path(__file__).resolve().parent.parent
BOSS_AUDIO_DIR = PROJECT_ROOT / "Assets" / "Resources" / "Audio" / "BossBattle"

EDGE_UA = (
    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 "
    "(KHTML, like Gecko) Chrome/127.0.0.0 Safari/537.36 Edg/127.0.0.0"
)
SIGNING_SECRET_B64 = (
    "oik6PdDdMnOXemTbwvMn9de/h9lFnfBaCWbGMMZqqoSaQaqUOqjVGm5NqsmjcBI1"
    "x+sS9ugjB55HEJWRiFXYFw=="
)

# 与 MiMo 统一：轻微愤怒 + 哭腔感；SSML 压低音量（prosody volume）
BOSS_EDGE_STYLE = "serious"
BOSS_EDGE_RATE = "+4%"
BOSS_EDGE_VOLUME = "-14%"
BOSS_EDGE_LINES: list[tuple[str, str, str, str]] = [
    ("boss_battle_start", "我不想害人。可我更不想被人害。", BOSS_EDGE_STYLE, BOSS_EDGE_RATE),
    ("boss_battle_cast_skill_1", "你们……都有道理！", BOSS_EDGE_STYLE, BOSS_EDGE_RATE),
    ("boss_battle_cast_skill_2", "那我呢？那我呢！", BOSS_EDGE_STYLE, BOSS_EDGE_RATE),
    ("boss_battle_hurt", "我不疼……我不疼……", BOSS_EDGE_STYLE, BOSS_EDGE_RATE),
    ("boss_battle_phase2", "我死过一次了。我不怕。", BOSS_EDGE_STYLE, BOSS_EDGE_RATE),
    ("boss_battle_near_death", "天……快亮了……", BOSS_EDGE_STYLE, BOSS_EDGE_RATE),
]

VOICE = "zh-CN-XiaoqiuNeural"


def date_format() -> str:
    return (
        dt.datetime.now(dt.timezone.utc)
        .strftime("%a, %d %b %Y %H:%M:%S GMT")
        .lower()
    )


def sign(url: str) -> str:
    url_no_scheme = url.split("://", 1)[1]
    encoded_url = urllib.parse.quote(url_no_scheme, safe="")
    trace_id = uuid.uuid4().hex
    formatted_date = date_format()
    payload = f"MSTranslatorAndroidApp{encoded_url}{formatted_date}{trace_id}".lower()
    secret = base64.b64decode(SIGNING_SECRET_B64)
    digest = hmac.new(secret, payload.encode("utf-8"), hashlib.sha256).digest()
    sig_b64 = base64.b64encode(digest).decode("utf-8")
    return f"MSTranslatorAndroidApp::{sig_b64}::{formatted_date}::{trace_id}"


def get_endpoint() -> dict:
    endpoint_url = "https://dev.microsofttranslator.com/apps/endpoint?api-version=1.0"
    headers = {
        "Accept-Language": "zh-Hans",
        "X-ClientVersion": "4.0.530a 5fe1dc6c",
        "X-UserId": "0f04d16a175c411e",
        "X-HomeGeographicRegion": "zh-Hans-CN",
        "X-ClientTraceId": uuid.uuid4().hex,
        "X-MT-Signature": sign(endpoint_url),
        "User-Agent": EDGE_UA,
        "Content-Type": "application/json; charset=utf-8",
        "Content-Length": "0",
        "Accept-Encoding": "gzip",
    }
    req = urllib.request.Request(endpoint_url, method="POST", headers=headers)
    with urllib.request.urlopen(req, timeout=20) as resp:
        return json.loads(resp.read().decode("utf-8"))


def _escape_xml(text: str) -> str:
    return (
        text.replace("&", "&amp;")
        .replace("<", "&lt;")
        .replace(">", "&gt;")
        .replace('"', "&quot;")
        .replace("'", "&apos;")
    )


def build_ssml(text: str, style: str, rate: str, volume: str | None = None) -> str:
    safe = _escape_xml(text)
    vol = f" volume='{volume}'" if volume else ""
    return (
        f"<speak version='1.0' xmlns='http://www.w3.org/2001/10/synthesis' "
        f"xmlns:mstts='http://www.w3.org/2001/mstts' xml:lang='zh-CN'>\n"
        f"  <voice name='{VOICE}'>\n"
        f"    <prosody rate='{rate}'{vol}>\n"
        f"      <mstts:express-as style='{style}'>\n"
        f"        {safe}\n"
        f"      </mstts:express-as>\n"
        f"    </prosody>\n"
        f"  </voice>\n"
        f"</speak>"
    )


def synthesize_ssml_to_mp3(ssml: str, out_path: Path, endpoint: dict) -> None:
    region = endpoint["r"]
    token = endpoint["t"]
    url = f"https://{region}.tts.speech.microsoft.com/cognitiveservices/v1"
    body = ssml.encode("utf-8")
    headers = {
        "Authorization": token,
        "Content-Type": "application/ssml+xml",
        "User-Agent": EDGE_UA,
        "X-Microsoft-OutputFormat": "audio-24khz-48kbitrate-mono-mp3",
    }
    req = urllib.request.Request(url, data=body, method="POST", headers=headers)
    with urllib.request.urlopen(req, timeout=45) as resp:
        audio = resp.read()
    out_path.parent.mkdir(parents=True, exist_ok=True)
    out_path.write_bytes(audio)


def synthesize_line_to_mp3(text: str, out_file: str | Path, voice: str = "zh-CN-XiaoxiaoNeural") -> str:
    """简单单句 mp3（冒烟测试）。"""
    endpoint = get_endpoint()
    region = endpoint["r"]
    token = endpoint["t"]
    url = f"https://{region}.tts.speech.microsoft.com/cognitiveservices/v1"
    ssml = f"""<speak version='1.0' xml:lang='zh-CN'>
  <voice name='{voice}'>{_escape_xml(text)}</voice>
</speak>""".encode("utf-8")
    headers = {
        "Authorization": token,
        "Content-Type": "application/ssml+xml",
        "User-Agent": EDGE_UA,
        "X-Microsoft-OutputFormat": "audio-24khz-48kbitrate-mono-mp3",
    }
    req = urllib.request.Request(url, data=ssml, method="POST", headers=headers)
    with urllib.request.urlopen(req, timeout=30) as resp:
        audio = resp.read()
    out_path = Path(out_file)
    out_path.write_bytes(audio)
    return str(out_path)


def run_edge_boss_batch(*, stems: list[str] | None = None, delay: float = 0.35) -> None:
    BOSS_AUDIO_DIR.mkdir(parents=True, exist_ok=True)
    lines = BOSS_EDGE_LINES
    if stems:
        want = set(stems)
        lines = [t for t in lines if t[0] in want]
    print(f"Edge Boss -> {BOSS_AUDIO_DIR}  (voice={VOICE})")
    endpoint = get_endpoint()
    for stem, line, style, rate in lines:
        out = BOSS_AUDIO_DIR / f"{stem}.mp3"
        print(f"  -> {out.name}  style={style} rate={rate} vol={BOSS_EDGE_VOLUME}")
        synthesize_ssml_to_mp3(
            build_ssml(line, style, rate, BOSS_EDGE_VOLUME), out, endpoint
        )
        time.sleep(delay)
    print("完成。")
