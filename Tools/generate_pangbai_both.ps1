$MIMO_API_KEY = "sk-cnq7o5mmf98vyryocob7z6ili3foa5spe3vi7kmz2svie67e"
$MIMO_TTS_URL = "https://api.xiaomimimo.com/v1/chat/completions"
$BASE_PATH = "d:\U3D\project\2DGame(unnamed)"
$STYLE = "低沉女声，旁白风格，庄重严肃，声音沉稳有磁性"

$dialogues = @(
    @{name="pangbai"; text="乱世多孤魂，有人舍米救人，有人只求被记一生。"},
    @{name="pangbai2"; text="善与憾，生与死，皆由你渡。"}
)

function Create-WavFile {
    param([byte[]]$pcmData, [string]$outputPath)
    $file = [System.IO.File]::Create($outputPath)
    $writer = New-Object System.IO.BinaryWriter($file)
    $writer.Write([byte[]]@(0x52,0x49,0x46,0x46))
    $writer.Write([BitConverter]::GetBytes(36 + $pcmData.Length))
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
    $writer.Write([BitConverter]::GetBytes($pcmData.Length))
    $writer.Write($pcmData)
    $writer.Close()
    $file.Close()
}

function Call-MimoTTS {
    param([string]$text, [string]$style)
    $body = @{
        model = "mimo-v2-tts"
        messages = @(@{role="assistant"; content="[$style] $text"})
        audio = @{format="pcm16"; voice="default_zh"}
        stream = $false
    } | ConvertTo-Json -Depth 10 -Compress
    
    $headers = @{"Content-Type"="application/json"; "api-key"=$MIMO_API_KEY}
    $response = Invoke-WebRequest -Uri $MIMO_TTS_URL -Method POST -Headers $headers -Body $body -TimeoutSec 120
    $json = $response.Content | ConvertFrom-Json
    $audioData = $json.choices[0].message.audio.data
    return [Convert]::FromBase64String($audioData)
}

Write-Host "=" * 60
Write-Host "重新生成旁白 (统一音色)"
Write-Host "=" * 60
Write-Host "风格: $STYLE`n"

foreach ($d in $dialogues) {
    $filename = "$($d.name).wav"
    $path = Join-Path $BASE_PATH $filename
    
    Write-Host "生成 ${filename}:"
    Write-Host "  文本: $($d.text)"
    
    try {
        $pcm = Call-MimoTTS -text $d.text -style $STYLE
        Create-WavFile -pcmData $pcm -outputPath $path
        Write-Host "  -> 已保存 ($($pcm.Length) bytes)" -ForegroundColor Green
    }
    catch {
        Write-Host "  -> 失败: $_" -ForegroundColor Red
    }
    
    Start-Sleep -Milliseconds 200
}

Write-Host "`n完成！所有旁白已使用统一音色重新生成。"
