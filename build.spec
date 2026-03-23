# -*- mode: python ; coding: utf-8 -*-
"""
PyInstaller 打包配置

使用方法:
    pyinstaller build.spec
"""

import sys
from pathlib import Path

block_cipher = None

a = Analysis(
    ['src/steamwatch/main.py'],
    pathex=[],
    binaries=[],
    datas=[
        ('assets', 'assets'),
    ],
    hiddenimports=[
        'steamwatch',
        'steamwatch.core',
        'steamwatch.core.steam_monitor',
        'steamwatch.core.cache_reader',
        'steamwatch.core.time_tracker',
        'steamwatch.core.reminder_manager',
        'steamwatch.ui',
        'steamwatch.ui.tray',
        'steamwatch.ui.main_window',
        'steamwatch.models',
        'steamwatch.models.game',
        'steamwatch.models.config',
        'steamwatch.utils',
        'steamwatch.utils.notification',
        'steamwatch.utils.storage',
        'steamwatch.utils.logger',
        'steamwatch.utils.autostart',
        'steamwatch.config',
        'steamwatch.config.settings',
        'pystray._win32',
        'PIL',
        'PIL._imaging',
        'PIL._imagingft',
        'matplotlib',
        'matplotlib.pyplot',
        'matplotlib.backends',
        'matplotlib.backends.backend_tkagg',
        'matplotlib.backends._backend_tk',
        'matplotlib.figure',
        'matplotlib.font_manager',
        'matplotlib.image',
        'matplotlib.path',
        'matplotlib.patches',
        'matplotlib.lines',
        'matplotlib.collections',
        'matplotlib.transforms',
        'matplotlib._path',
        'matplotlib._image',
        'matplotlib._png',
        'numpy',
        'numpy.core',
        'numpy.core._multiarray_umath',
        'kiwisolver',
        'packaging',
        'dateutil',
    ],
    hookspath=[],
    hooksconfig={},
    runtime_hooks=[],
    excludes=[
        'tkinter.test',
        'unittest',
        'pytest',
        'black',
        'flake8',
        'mypy',
        'isort',
    ],
    win_no_prefer_redirects=False,
    win_private_assemblies=False,
    cipher=block_cipher,
    noarchive=False,
)

pyz = PYZ(a.pure, a.zipped_data, cipher=block_cipher)

exe = EXE(
    pyz,
    a.scripts,
    a.binaries,
    a.zipfiles,
    a.datas,
    [],
    name='SteamWatch',
    debug=False,
    bootloader_ignore_signals=False,
    strip=False,
    upx=True,
    upx_exclude=[],
    runtime_tmpdir=None,
    console=False,
    disable_windowed_traceback=False,
    argv_emulation=False,
    target_arch=None,
    codesign_identity=None,
    entitlements_file=None,
    icon='assets/icon.ico' if Path('assets/icon.ico').exists() else None,
)