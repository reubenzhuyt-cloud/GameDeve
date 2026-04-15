import os
import sys
import json
import base64
import wave
import urllib.request
import urllib.error
from pathlib import Path

MIMO_API_KEY = "sk-cnq7o5mmf98vyryocob7z6ili3foa5spe3vi7kmz2svie67e"
MIMO_TTS_URL = "https://api.xiaomimimo.com/v1/chat/completions"

SAMPLE_RATE = 24000

def create_wav_file(pcm_data: bytes, output_path: str, sample_rate: int = SAMPLE_RATE):
    with wave.open(output_path, 'wb') as wav_file:
        wav_file.setnchannels(1)
        wav_file.setsampwidth(2)
        wav_file.setframerate(sample_rate)
        wav_file.writeframes(pcm_data)

def call_mimo_tts(text: str, style: str = "") -> bytes:
    headers = {
        "Content-Type": "application/json",
        "api-key": MIMO_API_KEY
    }
    
    content_with_style = text
    if style:
        content_with_style = f"[{style}] {text}"
    
    payload = {
        "model": "mimo-v2-tts",
        "messages": [
            {
                "role": "assistant",
                "content": content_with_style
            }
        ],
        "audio": {
            "format": "pcm16",
            "voice": "default_zh"
        },
        "stream": False
    }
    
    print(f"发送请求: {content_with_style[:50]}...")
    
    req = urllib.request.Request(
        MIMO_TTS_URL,
        data=json.dumps(payload).encode('utf-8'),
        headers=headers,
        method='POST'
    )
    
    with urllib.request.urlopen(req, timeout=120) as response:
        data = json.loads(response.read().decode('utf-8'))
        
        if "choices" in data and len(data["choices"]) > 0:
            audio_data = data["choices"][0]["message"]["audio"]["data"]
            if audio_data:
                return base64.b64decode(audio_data)
    
    return b""

def main():
    print("=" * 50)
    print("测试男声 TTS")
    print("=" * 50)
    
    test_cases = [
        ("年轻男声，清朗有力，充满朝气", "年轻人，前方的路还很长，你要小心行事。"),
        ("中年男声，沉稳厚重，带有威严", "我是这里的守护者，已经守候了千年之久。"),
        ("老年男声，苍老沙哑，饱经沧桑", "岁月如流水，转眼间，我已不记得自己是谁了。"),
    ]
    
    output_dir = Path(__file__).parent.parent / "Assets" / "Resources" / "Audio" / "Dialogue"
    output_dir.mkdir(parents=True, exist_ok=True)
    
    for i, (style, text) in enumerate(test_cases):
        print(f"\n测试 {i+1}: {style}")
        print(f"文本: {text}")
        
        pcm_data = call_mimo_tts(text, style)
        
        if pcm_data:
            output_path = output_dir / f"test_male_voice_{i+1}.wav"
            create_wav_file(pcm_data, str(output_path))
            print(f"✓ 已保存: {output_path.name} ({len(pcm_data)} bytes)")
        else:
            print("✗ 生成失败")
    
    print("\n" + "=" * 50)
    print("测试完成!")
    print("=" * 50)

if __name__ == "__main__":
    main()
