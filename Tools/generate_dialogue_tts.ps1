# 对话配音：请在项目根目录 .env 中配置 DASHSCOPE_API_KEY（北京地域），勿在脚本里写密钥。
# 用法: .\Tools\generate_dialogue_tts.ps1
# 传参会转发给: python Tools/generate_tts.py dialogue @args
$ProjectRoot = Split-Path $PSScriptRoot -Parent
Set-Location $ProjectRoot
python Tools/generate_tts.py dialogue @args
