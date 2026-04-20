$MIMO_API_KEY = "sk-cnq7o5mmf98vyryocob7z6ili3foa5spe3vi7kmz2svie67e"
$MIMO_TTS_URL = "https://api.xiaomimimo.com/v1/chat/completions"
$OUTPUT_DIR = "d:\U3D\project\2DGame(unnamed)\NewDialogues_Variations"

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
    
    try {
        $response = Invoke-WebRequest -Uri $MIMO_TTS_URL -Method POST -Headers $headers -Body $body -TimeoutSec 120
        $json = $response.Content | ConvertFrom-Json
        if ($json.choices.Count -gt 0) {
            $audioData = $json.choices[0].message.audio.data
            if ($audioData) { return [Convert]::FromBase64String($audioData) }
        }
        return $null
    }
    catch {
        Write-Host "Error: $_" -ForegroundColor Red
        return $null
    }
}

New-Item -ItemType Directory -Path $OUTPUT_DIR -Force | Out-Null
Write-Host "输出目录: $OUTPUT_DIR`n"

$dialogues = @(
    @{name="孟婆台词1"; text="一时心软，谁料魂珠失窃，地府大乱。"},
    @{name="孟婆台词2"; text="这寻珠的路，便托付于你了。"},
    @{name="孟忘台词1"; text="我名孟忘。必寻回那窃走魂珠之鬼。"},
    @{name="孟忘台词2"; text="我渡的不是亡魂，是人间未平的遗憾。"}
)

$styles = @(
    @{id="1"; name="慈祥温和"; desc="老年女性，声音沙哑低沉，慈祥温和"},
    @{id="2"; name="严肃庄重"; desc="老年女性，声音沙哑，语气严肃庄重"},
    @{id="3"; name="哀伤感叹"; desc="老年女性，声音沙哑，带有哀伤和感叹"},
    @{id="4"; name="威严郑重"; desc="老年女性，声音低沉，威严郑重"},
    @{id="5"; name="温和叹息"; desc="老年女性，语气温和，略带叹息"}
)

foreach ($d in $dialogues) {
    Write-Host "处理: $($d.name)"
    foreach ($s in $styles) {
        $filename = "$($d.name)_v$($s.id)_$($s.name).wav"
        $path = Join-Path $OUTPUT_DIR $filename
        Write-Host "  版本$($s.id)($($s.name))..." -NoNewline
        
        $pcm = Call-MimoTTS -text $d.text -style $s.desc
        if ($pcm) {
            Create-WavFile -pcmData $pcm -outputPath $path
            Write-Host " 已保存" -ForegroundColor Green
        } else {
            Write-Host " 失败" -ForegroundColor Red
        }
        Start-Sleep -Milliseconds 200
    }
}

Write-Host "`n完成！输出目录: $OUTPUT_DIR"
