import base64
import datetime as dt
import hashlib
import hmac
import json
import urllib.parse
import urllib.request
import uuid


EDGE_UA = (
    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 "
    "(KHTML, like Gecko) Chrome/127.0.0.0 Safari/537.36 Edg/127.0.0.0"
)
SIGNING_SECRET_B64 = (
    "oik6PdDdMnOXemTbwvMn9de/h9lFnfBaCWbGMMZqqoSaQaqUOqjVGm5NqsmjcBI1"
    "x+sS9ugjB55HEJWRiFXYFw=="
)


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


def get_ssml(text: str, voice: str) -> str:
    return f"""
<speak version='1.0' xml:lang='zh-CN'>
  <voice name='{voice}'>{text}</voice>
</speak>
""".strip()


def synthesize(text: str, out_file: str = "edge_test.mp3") -> str:
    endpoint = get_endpoint()
    region = endpoint["r"]
    token = endpoint["t"]
    url = f"https://{region}.tts.speech.microsoft.com/cognitiveservices/v1"

    ssml = get_ssml(text, "zh-CN-XiaoxiaoNeural").encode("utf-8")
    headers = {
        "Authorization": token,
        "Content-Type": "application/ssml+xml",
        "User-Agent": EDGE_UA,
        "X-Microsoft-OutputFormat": "audio-24khz-48kbitrate-mono-mp3",
    }
    req = urllib.request.Request(url, data=ssml, method="POST", headers=headers)
    with urllib.request.urlopen(req, timeout=30) as resp:
        audio = resp.read()

    with open(out_file, "wb") as f:
        f.write(audio)
    return out_file


if __name__ == "__main__":
    text = "你好，这是 Python 直接调用微软通道生成的测试语音。"
    output = synthesize(text, "edge_test.mp3")
    print(f"OK: {output}")
