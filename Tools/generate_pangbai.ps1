$MIMO_API_KEY = "sk-cnq7o5mmf98vyryocob7z6ili3foa5spe3vi7kmz2svie67e"
$MIMO_TTS_URL = "https://api.xiaomimimo.com/v1/chat/completions"
$OUTPUT_PATH = "d:\U3D\project\2DGame(unnamed)\pangbai.wav"
$TEXT = "乱世多孤魂，有人舍米救人，有人只求被记一生。"
$STYLE = "低沉女声，旁白风格，庄重严肃"

$body = @{
    model = "mimo-v2-tts"
    messages = @(@{role="assistant"; content="[$STYLE] $TEXT"})
    audio = @{format="pcm16"; voice="default_zh"}
    stream = $false
} | ConvertTo-Json -Depth 10 -Compress

$headers = @{"Content-Type"="application/json"; "api-key"=$MIMO_API_KEY}

Write-Host "生成旁白: $TEXT"
Write-Host "风格: $STYLE"

$response = Invoke-WebRequest -Uri $MIMO_TTS_URL -Method POST -Headers $headers -Body $body -TimeoutSec 120
$json = $response.Content | ConvertFrom-Json
$audioData = $json.choices[0].message.audio.data
$bytes = [Convert]::FromBase64String($audioData)

$file = [System.IO.File]::Create($OUTPUT_PATH)
$writer = New-Object System.IO.BinaryWriter($file)
$writer.Write([byte[]]@(0x52,0x49,0x46,0x46))
$writer.Write([BitConverter]::GetBytes(36 + $bytes.Length))
$writer.Write([byte[]]@(0x57,0x41,0x56,0x45))
$writer.Write([byte[]]@(0x66,0x6D,0x74,0x20))
$writer.Write([BitConverter]::GetBytes(16))
$writer.Write([BitConverter]::GetBytes([int16]1))
$writer.Write([BitConverter]::GetBytes([int16]1))
$writer.Write([BitConverter]::GetBytes(24000))
$writer.Write([BitConverter]::GetBytes(48000))
$writer.Write([BitConverter]::GetBytes([int16]2))
$writer.Write([BitConverter]::GetBytes([int16]16))
$writer.Write([byte[]]@(0x64,0x61,0x74,0x61))
$writer.Write([BitConverter]::GetBytes($bytes.Length))
$writer.Write($bytes)
$writer.Close()
$file.Close()

Write-Host "`n已保存: $OUTPUT_PATH"
Write-Host "文件大小: $((Get-Item $OUTPUT_PATH).Length) bytes"
