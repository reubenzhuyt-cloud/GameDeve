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

def test_and_save():
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
    
    try:
        req = urllib.request.Request(
            MIMO_TTS_URL,
            data=json.dumps(payload).encode('utf-8'),
            headers=headers,
            method='POST'
        )
        
        print("Sending request...")
        with urllib.request.urlopen(req, timeout=120) as response:
            print(f"Response Status: {response.status}")
            
            content = response.read()
            print(f"Response Length: {len(content)} bytes")
            
            data = json.loads(content.decode('utf-8'))
            
            if "error" in data:
                print(f"API Error: {data['error']}")
                return
            
            if "choices" in data and len(data["choices"]) > 0:
                message = data["choices"][0].get("message", {})
                audio = message.get("audio", {})
                audio_data = audio.get("data", "")
                
                print(f"Audio data found: {len(audio_data)} chars")
                
                if audio_data:
                    pcm_data = base64.b64decode(audio_data)
                    print(f"PCM data: {len(pcm_data)} bytes")
                    
                    output_path = "test_output.wav"
                    with wave.open(output_path, 'wb') as wav_file:
                        wav_file.setnchannels(1)
                        wav_file.setsampwidth(2)
                        wav_file.setframerate(24000)
                        wav_file.writeframes(pcm_data)
                    
                    print(f"Saved to: {output_path}")
                else:
                    print("No audio data!")
            else:
                print("No choices in response!")
        
    except Exception as e:
        print(f"Error: {type(e).__name__}: {e}")
        import traceback
        traceback.print_exc()

if __name__ == "__main__":
    test_and_save()
