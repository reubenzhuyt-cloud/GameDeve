import json
import base64
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

def test_tts_with_model(model_name):
    headers = {
        "Content-Type": "application/json",
        "api-key": MIMO_API_KEY
    }
    
    payload = {
        "model": model_name,
        "messages": [
            {
                "role": "assistant",
                "content": "你好"
            }
        ],
        "audio": {
            "format": "pcm16",
            "voice": "default_zh"
        },
        "stream": True
    }
    
    print(f"\nTesting with model: {model_name}")
    
    try:
        req = urllib.request.Request(
            MIMO_TTS_URL,
            data=json.dumps(payload).encode('utf-8'),
            headers=headers,
            method='POST'
        )
        
        with urllib.request.urlopen(req, timeout=30) as response:
            print(f"  Status: {response.status}")
            content = response.read()
            print(f"  Length: {len(content)} bytes")
            
            if b'"error"' in content:
                print(f"  Error: {content[:500].decode('utf-8', errors='replace')}")
            else:
                print(f"  First 200 chars: {content[:200].decode('utf-8', errors='replace')}")
        
    except urllib.error.HTTPError as e:
        error_body = e.read().decode('utf-8')
        print(f"  HTTP Error: {e.code} - {e.reason}")
        print(f"  Body: {error_body[:500]}")
    except Exception as e:
        print(f"  Error: {e}")

if __name__ == "__main__":
    models_to_test = [
        "mimo-v2-tts",
        "xiaomi/mimo-v2-tts",
        "MiMo-V2-TTS",
        "mimo-tts",
        "mimo-v2-flash",
    ]
    
    for model in models_to_test:
        test_tts_with_model(model)
