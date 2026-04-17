# 对话配音：请使用项目根目录 .env 中的 MIMO_API_KEY，勿在脚本里写密钥。
# 用法: .\Tools\generate_dialogue_tts.ps1
# 传参会转发给: python Tools/generate_tts.py dialogue @args
$ProjectRoot = Split-Path $PSScriptRoot -Parent
Set-Location $ProjectRoot
python Tools/generate_tts.py dialogue @args
