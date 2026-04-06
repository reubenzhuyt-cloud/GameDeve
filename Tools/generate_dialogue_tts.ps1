$MIMO_API_KEY = "sk-cnq7o5mmf98vyryocob7z6ili3foa5spe3vi7kmz2svie67e"
$MIMO_TTS_URL = "https://api.xiaomimimo.com/v1/chat/completions"
$DIALOGUE_DIR = "d:\U3D\project\2DGame(unnamed)\Assets\Resources\Dialogue"
$OUTPUT_DIR = "d:\U3D\project\2DGame(unnamed)\Assets\Resources\Audio\Dialogue"

$ACTOR_STYLES = @{
    [int]0 = "年轻男声，语气平和，略带好奇"
    [int]1 = "老年女性，声音沙哑低沉，慈祥温和"
    [int]2 = "虚弱的声音，飘渺空灵，带有叹息感"
    [int]3 = "中性声音，平静客观"
}

$ACTOR_NAMES = @{
    [int]0 = "Player"
    [int]1 = "MengPo"
    [int]2 = "WeakSoul"
    [int]3 = "Narrator"
}

function Create-WavFile {
    param([byte[]]$pcmData, [string]$outputPath)
    
    $file = [System.IO.File]::Create($outputPath)
    $writer = New-Object System.IO.BinaryWriter($file)
    
    # RIFF header
    $writer.Write([byte[]]@(0x52, 0x49, 0x46, 0x46))  # "RIFF"
    $writer.Write([BitConverter]::GetBytes(36 + $pcmData.Length))
    $writer.Write([byte[]]@(0x57, 0x41, 0x56, 0x45))  # "WAVE"
    
    # fmt chunk
    $writer.Write([byte[]]@(0x66, 0x6D, 0x74, 0x20))  # "fmt "
    $writer.Write([BitConverter]::GetBytes(16))       # chunk size
    $writer.Write([BitConverter]::GetBytes([int16]1)) # audio format (PCM)
    $writer.Write([BitConverter]::GetBytes([int16]1)) # channels
    $writer.Write([BitConverter]::GetBytes(24000))    # sample rate
    $writer.Write([BitConverter]::GetBytes(48000))    # byte rate
    $writer.Write([BitConverter]::GetBytes([int16]2)) # block align
    $writer.Write([BitConverter]::GetBytes([int16]16))# bits per sample
    
    # data chunk
    $writer.Write([byte[]]@(0x64, 0x61, 0x74, 0x61))  # "data"
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
        
        if ($json.error) {
            Write-Host "    API Error: $($json.error.message)" -ForegroundColor Red
        }
        
        return $null
    }
    catch {
        Write-Host "    Error: $_" -ForegroundColor Red
        return $null
    }
}

# Main script
Write-Host "=" * 60
Write-Host "MIMO TTS 对话配音生成器 (PowerShell)"
Write-Host "=" * 60

# Ensure output directory exists
if (-not (Test-Path $OUTPUT_DIR)) {
    New-Item -ItemType Directory -Path $OUTPUT_DIR -Force | Out-Null
}

# Get all dialogue files
$dialogueFiles = Get-ChildItem -Path $DIALOGUE_DIR -Filter "*.json"
Write-Host "`n找到 $($dialogueFiles.Count) 个对话文件"

$allResults = @()

foreach ($dialogueFile in $dialogueFiles) {
    # Skip test files
    if ($dialogueFile.Name.StartsWith("test")) {
        Write-Host "`n跳过测试文件: $($dialogueFile.Name)"
        continue
    }
    
    $dialogueData = Get-Content $dialogueFile.FullName | ConvertFrom-Json
    $dialogueName = $dialogueData.DialogueName
    $nodes = $dialogueData.nodes
    
    Write-Host "`n处理对话: $dialogueName ($($dialogueFile.Name))"
    
    $audioFiles = @()
    
    foreach ($node in $nodes) {
        $nodeId = $node.nodeId
        $actorId = [int]$node.actorId
        $dialogueText = $node.dialogueText
        $isChoiceNode = $node.isChoiceNode
        
        # Skip empty text or choice nodes
        if ([string]::IsNullOrWhiteSpace($dialogueText) -or $isChoiceNode) {
            continue
        }
        
        $actorName = $ACTOR_NAMES[$actorId]
        if (-not $actorName) { $actorName = "Actor_$actorId" }
        
        $style = $ACTOR_STYLES[$actorId]
        if (-not $style) { $style = "自然、流畅的中文语音" }
        
        $outputFilename = "$($dialogueFile.BaseName)_node$nodeId.wav"
        $outputPath = Join-Path $OUTPUT_DIR $outputFilename
        
        $displayText = $dialogueText
        if ($displayText.Length -gt 30) {
            $displayText = $displayText.Substring(0, 30) + "..."
        }
        
        Write-Host "  节点 $nodeId (角色: $actorName, 风格: $style): $displayText"
        
        $pcmData = Call-MimoTTS -text $dialogueText -style $style
        
        if ($pcmData) {
            Create-WavFile -pcmData $pcmData -outputPath $outputPath
            Write-Host "    -> 已保存: $outputFilename ($($pcmData.Length) bytes)" -ForegroundColor Green
            
            $audioFiles += @{
                node_id = $nodeId
                actor_id = $actorId
                actor_name = $actorName
                filename = $outputFilename
                text = $dialogueText
            }
        }
        else {
            Write-Host "    -> 生成失败!" -ForegroundColor Red
        }
        
        Start-Sleep -Milliseconds 300
    }
    
    $allResults += @{
        dialogue_name = $dialogueName
        file_name = $dialogueFile.BaseName
        audio_files = $audioFiles
    }
}

# Save manifest
$manifestPath = Join-Path $OUTPUT_DIR "dialogue_audio_manifest.json"
$allResults | ConvertTo-Json -Depth 10 | Out-File -FilePath $manifestPath -Encoding UTF8

Write-Host "`n" + ("=" * 60)
Write-Host "配音生成完成!"
Write-Host "清单文件: $manifestPath"
Write-Host "=" * 60
