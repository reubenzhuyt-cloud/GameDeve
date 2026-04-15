$MIMO_API_KEY = "sk-cnq7o5mmf98vyryocob7z6ili3foa5spe3vi7kmz2svie67e"
$MIMO_TTS_URL = "https://api.xiaomimimo.com/v1/chat/completions"
$OUTPUT_DIR = "d:\U3D\project\2DGame(unnamed)\NewDialogues"

$CHARACTER_STYLES = @{
    "MengPo" = "老年女性，声音沙哑低沉，慈祥温和"
    "MengWang" = "年轻女性，坚定有力，略带沧桑感"
}

function Create-WavFile {
    param([byte[]]$pcmData, [string]$outputPath)
    
    $file = [System.IO.File]::Create($outputPath)
    $writer = New-Object System.IO.BinaryWriter($file)
    
    $writer.Write([byte[]]@(0x52, 0x49, 0x46, 0x46))
    $writer.Write([BitConverter]::GetBytes(36 + $pcmData.Length))
    $writer.Write([byte[]]@(0x57, 0x41, 0x56, 0x45))
    
    $writer.Write([byte[]]@(0x66, 0x6D, 0x74, 0x20))
    $writer.Write([BitConverter]::GetBytes(16))
    $writer.Write([BitConverter]::GetBytes([int16]1))
    $writer.Write([BitConverter]::GetBytes([int16]1))
    $writer.Write([BitConverter]::GetBytes(24000))
    $writer.Write([BitConverter]::GetBytes(48000))
    $writer.Write([BitConverter]::GetBytes([int16]2))
    $writer.Write([BitConverter]::GetBytes([int16]16))
    
    $writer.Write([byte[]]@(0x64, 0x61, 0x74, 0x61))
    $writer.Write([BitConverter]::GetBytes($pcmData.Length))
    $writer.Write($pcmData)
    
    $writer.Close()
    $file.Close()
}

function Call-MimoTTS {
    param([string]$text, [string]$style = "")
    
    $contentWithStyle = $text
    if ($style) {
        $contentWithStyle = "[$style] $text"
    }
    
    $body = @{
        model = "mimo-v2-tts"
        messages = @(
            @{
                role = "assistant"
                content = $contentWithStyle
            }
        )
        audio = @{
            format = "pcm16"
            voice = "default_zh"
        }
        stream = $false
    } | ConvertTo-Json -Depth 10 -Compress
    
    $headers = @{
        "Content-Type" = "application/json"
        "api-key" = $MIMO_API_KEY
    }
    
    try {
        $response = Invoke-WebRequest -Uri $MIMO_TTS_URL -Method POST -Headers $headers -Body $body -TimeoutSec 120
        $json = $response.Content | ConvertFrom-Json
        
        if ($json.choices -and $json.choices.Count -gt 0) {
            $audioData = $json.choices[0].message.audio.data
            if ($audioData) {
                return [Convert]::FromBase64String($audioData)
            }
        }
        
        return $null
    }
    catch {
        Write-Host "    Error: $_" -ForegroundColor Red
        return $null
    }
}

Write-Host "=" * 60
Write-Host "新台词TTS生成器"
Write-Host "=" * 60

if (-not (Test-Path $OUTPUT_DIR)) {
    New-Item -ItemType Directory -Path $OUTPUT_DIR -Force | Out-Null
}
Write-Host "`n输出目录: $OUTPUT_DIR"

$dialogues = @(
    @{ character = "MengPo"; label = "孟婆台词1"; text = "一时心软，谁料魂珠失窃，地府大乱。" },
    @{ character = "MengPo"; label = "孟婆台词2"; text = "这寻珠的路，便托付于你了。" },
    @{ character = "MengWang"; label = "孟忘台词1"; text = "我名孟忘。必寻回那窃走魂珠之鬼。" },
    @{ character = "MengWang"; label = "孟忘台词2"; text = "我渡的不是亡魂，是人间未平的遗憾。" }
)

$results = @()

foreach ($dialogue in $dialogues) {
    $character = $dialogue.character
    $label = $dialogue.label
    $text = $dialogue.text
    
    $style = $CHARACTER_STYLES[$character]
    if (-not $style) { $style = "自然、流畅的中文语音" }
    
    $outputFilename = "$label.wav"
    $outputPath = Join-Path $OUTPUT_DIR $outputFilename
    
    Write-Host "`n处理: $label"
    Write-Host "  角色: $character (风格: $style)"
    Write-Host "  文本: $text"
    
    $pcmData = Call-MimoTTS -text $text -style $style
    
    if ($pcmData) {
        Create-WavFile -pcmData $pcmData -outputPath $outputPath
        Write-Host "    -> 已保存: $outputFilename ($($pcmData.Length) bytes)" -ForegroundColor Green
        
        $results += @{
            label = $label
            character = $character
            text = $text
            filename = $outputFilename
        }
    }
    else {
        Write-Host "    -> 生成失败!" -ForegroundColor Red
    }
    
    Start-Sleep -Milliseconds 300
}

$manifestPath = Join-Path $OUTPUT_DIR "manifest.json"
$results | ConvertTo-Json -Depth 10 | Out-File -FilePath $manifestPath -Encoding UTF8

Write-Host "`n" + ("=" * 60)
Write-Host "新台词TTS生成完成!"
Write-Host "输出目录: $OUTPUT_DIR"
Write-Host "清单文件: $manifestPath"
Write-Host "=" * 60
