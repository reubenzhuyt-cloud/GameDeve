import os
import sys
import json
import base64
import wave
import urllib.request
import urllib.error
import time
from pathlib import Path

try:
    from dotenv import load_dotenv
    # Load .env file from project root
    project_root = Path(__file__).parent.parent
    load_dotenv(project_root / '.env')
except ImportError:
    print("Warning: python-dotenv not installed. Using environment variables.")

MIMO_API_KEY = os.getenv("MIMO_API_KEY", "")
MIMO_BASE_URL = "https://api.xiaomimimo.com/v1"
MIMO_TTS_URL = os.getenv("MIMO_TTS_URL", f"{MIMO_BASE_URL}/chat/completions")

PROJECT_ROOT = Path(__file__).parent.parent
DIALOGUE_DIR = PROJECT_ROOT / "Assets" / "Resources" / "Dialogue"
AUDIO_OUTPUT_DIR = PROJECT_ROOT / "Assets" / "Resources" / "Audio" / "Dialogue"

ACTOR_STYLES = {
    0: "年轻男声，语气平和，略带好奇",
    1: "老年女声，慈祥温和，语速较慢，带有神秘感",
    2: "虚弱的声音，飘渺空灵，带有叹息感",
    3: "中性声音，平静客观"
}

ACTOR_NAMES = {
    0: "Player",
    1: "MengPo",
    2: "WeakSoul",
    3: "Narrator"
}

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
    
    try:
        req = urllib.request.Request(
            MIMO_TTS_URL,
            data=json.dumps(payload).encode('utf-8'),
            headers=headers,
            method='POST'
        )
        
        with urllib.request.urlopen(req, timeout=120) as response:
            status = response.status
            content = response.read()
            
            if status != 200:
                print(f"    Error: API returned status {status}")
                return b""
            
            try:
                data = json.loads(content.decode('utf-8'))
            except json.JSONDecodeError as e:
                print(f"    JSON decode error: {e}")
                return b""
            
            if "error" in data:
                print(f"    API Error: {data['error']}")
                return b""
            
            if "choices" in data and len(data["choices"]) > 0:
                message = data["choices"][0].get("message", {})
                audio = message.get("audio", {})
                audio_data = audio.get("data", "")
                
                if audio_data:
                    try:
                        return base64.b64decode(audio_data)
                    except Exception as e:
                        print(f"    Base64 decode error: {e}")
                        return b""
                else:
                    print(f"    No audio data in response")
            else:
                print(f"    No choices in response")
            
            return b""
        
    except urllib.error.URLError as e:
        print(f"    URL error: {e}")
        return b""
    except urllib.error.HTTPError as e:
        print(f"    HTTP error: {e.code} - {e.reason}")
        try:
            error_body = e.read().decode('utf-8')
            print(f"    Error body: {error_body[:500]}")
        except:
            pass
        return b""
    except Exception as e:
        print(f"    Request error: {type(e).__name__}: {e}")
        return b""

def process_dialogue_file(dialogue_path: Path) -> dict:
    with open(dialogue_path, 'r', encoding='utf-8') as f:
        dialogue_data = json.load(f)
    
    dialogue_name = dialogue_data.get("DialogueName", dialogue_path.stem)
    nodes = dialogue_data.get("nodes", [])
    
    results = {
        "dialogue_name": dialogue_name,
        "file_name": dialogue_path.stem,
        "audio_files": []
    }
    
    print(f"\n处理对话: {dialogue_name} ({dialogue_path.name})")
    
    for node in nodes:
        node_id = node.get("nodeId", -1)
        actor_id = node.get("actorId", 0)
        dialogue_text = node.get("dialogueText", "")
        is_choice_node = node.get("isChoiceNode", False)
        
        if not dialogue_text.strip() or is_choice_node:
            continue
        
        actor_name = ACTOR_NAMES.get(actor_id, f"Actor_{actor_id}")
        style = ACTOR_STYLES.get(actor_id, "自然、流畅的中文语音")
        
        output_filename = f"{dialogue_path.stem}_node{node_id}.wav"
        output_path = AUDIO_OUTPUT_DIR / output_filename
        
        print(f"  节点 {node_id} (角色: {actor_name}): {dialogue_text[:30]}...")
        sys.stdout.flush()
        
        pcm_data = call_mimo_tts(dialogue_text, style)
        
        if pcm_data:
            create_wav_file(pcm_data, str(output_path))
            print(f"    -> 已保存: {output_filename} ({len(pcm_data)} bytes)")
            sys.stdout.flush()
            results["audio_files"].append({
                "node_id": node_id,
                "actor_id": actor_id,
                "actor_name": actor_name,
                "filename": output_filename,
                "text": dialogue_text
            })
        else:
            print(f"    -> 生成失败!")
            sys.stdout.flush()
        
        time.sleep(0.5)
    
    return results

def main():
    print("=" * 60)
    print("MIMO TTS 对话配音生成器")
    print("=" * 60)
    sys.stdout.flush()
    
    AUDIO_OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    print(f"\n音频输出目录: {AUDIO_OUTPUT_DIR}")
    sys.stdout.flush()
    
    dialogue_files = list(DIALOGUE_DIR.glob("*.json"))
    print(f"找到 {len(dialogue_files)} 个对话文件")
    sys.stdout.flush()
    
    all_results = []
    
    for dialogue_file in dialogue_files:
        if dialogue_file.name.startswith("test"):
            print(f"\n跳过测试文件: {dialogue_file.name}")
            sys.stdout.flush()
            continue
            
        result = process_dialogue_file(dialogue_file)
        all_results.append(result)
    
    manifest_path = AUDIO_OUTPUT_DIR / "dialogue_audio_manifest.json"
    with open(manifest_path, 'w', encoding='utf-8') as f:
        json.dump(all_results, f, ensure_ascii=False, indent=2)
    
    print("\n" + "=" * 60)
    print("配音生成完成!")
    print(f"清单文件: {manifest_path}")
    print("=" * 60)
    sys.stdout.flush()

if __name__ == "__main__":
    main()
