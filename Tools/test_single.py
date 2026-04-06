import json
import base64
import wave
import urllib.request
import urllib.error
import os
from pathlib import Path

try:
    from dotenv import load_dotenv
    # Load .env file from project root
    project_root = Path(__file__).parent.parent
    load_dotenv(project_root / '.env')
except ImportError:
    print("Warning: python-dotenv not installed. Using environment variables.")

MIMO_API_KEY = os.getenv("MIMO_API_KEY", "")
MIMO_TTS_URL = os.getenv("MIMO_TTS_URL", "https://api.xiaomimimo.com/v1/chat/completions")

def test_tts():
    headers = {
        "Content-Type": "application/json",
        "api-key": MIMO_API_KEY
    }
    
    payload = {
        "model": "mimo-v2-tts",
        "messages": [
            {
                "role": "assistant",
                "content": "你好，这是一个测试。"
            }
        ],
        "audio": {
            "format": "pcm16",
            "voice": "default_zh"
        },
        "stream": False
    }
    
    print("Testing MIMO TTS API...")
    print(f"URL: {MIMO_TTS_URL}")
    print(f"Payload: {json.dumps(payload, ensure_ascii=False)}")
    
    try:
        req = urllib.request.Request(
            MIMO_TTS_URL,
            data=json.dumps(payload).encode('utf-8'),
            headers=headers,
            method='POST'
        )
        
        print("\nSending request...")
        with urllib.request.urlopen(req, timeout=120) as response:
            print(f"Response Status: {response.status}")
            print(f"Response Length: {len(response.read())} bytes")
            
            response.seek(0) if hasattr(response, 'seek') else None
            
    except urllib.error.HTTPError as e:
        print(f"HTTP Error: {e.code} - {e.reason}")
        error_body = e.read().decode('utf-8')
        print(f"Error body: {error_body}")
    except urllib.error.URLError as e:
        print(f"URL Error: {e}")
    except Exception as e:
        print(f"Error: {type(e).__name__}: {e}")

if __name__ == "__main__":
    test_tts()
