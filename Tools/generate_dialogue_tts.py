"""已合并至 generate_tts.py；保留此文件以兼容旧命令。"""
from __future__ import annotations

import sys
from pathlib import Path

_TOOLS = Path(__file__).resolve().parent
if str(_TOOLS) not in sys.path:
    sys.path.insert(0, str(_TOOLS))

from generate_tts import main  # noqa: E402

if __name__ == "__main__":
    main(["dialogue"] + sys.argv[1:])
